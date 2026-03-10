#!/usr/bin/env python3
"""
Unity Bridge MCP Server — stdio transport, zero external dependencies.

Wraps the file-based IPC bridge as a standard MCP server so that any
MCP-compatible client (Cursor, Copilot, Windsurf, Claude Desktop, etc.)
can control Unity Editor.

Usage (in MCP client config):
    {
        "mcpServers": {
            "unity-bridge": {
                "command": "python3",
                "args": [".claude/skills/unity-bridge/mcp_server.py"],
                "cwd": "/path/to/unity-project"
            }
        }
    }
"""

import json
import os
import sys
import time
import uuid
import glob

# ─────────────────────────────────────────
# Constants
# ─────────────────────────────────────────

JSONRPC_VERSION = "2.0"
MCP_PROTOCOL_VERSION = "2024-11-05"
SERVER_NAME = "unity-bridge"
SERVER_VERSION = "1.0.0"

IPC_POLL_INTERVAL = 0.1
IPC_TIMEOUT = 30
HEARTBEAT_MAX_AGE = 10


# ─────────────────────────────────────────
# MCP stdio transport
# ─────────────────────────────────────────

def read_message():
    """Read one JSON-RPC message from stdin (newline-delimited)."""
    line = sys.stdin.readline()
    if not line:
        return None
    line = line.strip()
    if not line:
        return None
    return json.loads(line)


def write_message(msg):
    """Write one JSON-RPC message to stdout."""
    sys.stdout.write(json.dumps(msg, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def respond(msg_id, result):
    write_message({"jsonrpc": JSONRPC_VERSION, "id": msg_id, "result": result})


def respond_error(msg_id, code, message):
    write_message({
        "jsonrpc": JSONRPC_VERSION,
        "id": msg_id,
        "error": {"code": code, "message": message}
    })


def send_notification(method, params=None):
    msg = {"jsonrpc": JSONRPC_VERSION, "method": method}
    if params is not None:
        msg["params"] = params
    write_message(msg)


# ─────────────────────────────────────────
# Tool catalog — from params/*.json
# ─────────────────────────────────────────

def find_params_dir():
    """Locate the params/ directory relative to this script."""
    script_dir = os.path.dirname(os.path.abspath(__file__))
    # Sibling params/ (same directory as mcp_server.py)
    candidate = os.path.join(script_dir, "params")
    if os.path.isdir(candidate):
        return os.path.abspath(candidate)
    return None


def load_tools():
    """Load tool definitions from params/ and convert to MCP format."""
    params_dir = find_params_dir()
    if not params_dir:
        return []

    tools = []
    for filepath in sorted(glob.glob(os.path.join(params_dir, "*.json"))):
        basename = os.path.basename(filepath)
        if basename.startswith("_"):
            continue
        try:
            with open(filepath, "r", encoding="utf-8") as f:
                tool_def = json.load(f)
            tools.append(convert_to_mcp_tool(tool_def))
        except Exception:
            pass
    return tools


def convert_to_mcp_tool(tool_def):
    """Convert params/*.json format → MCP tool schema."""
    properties = {}
    required = []

    for p in tool_def.get("parameters", []):
        prop = {}
        ptype = p.get("type", "string")
        prop["type"] = ptype
        if "description" in p:
            prop["description"] = p["description"]
        if "default" in p:
            prop["default"] = p["default"]
        if "enum" in p:
            prop["enum"] = [v.strip() for v in p["enum"].split(",")]
        properties[p["name"]] = prop
        if p.get("required") == "true":
            required.append(p["name"])

    input_schema = {"type": "object", "properties": properties}
    if required:
        input_schema["required"] = required

    return {
        "name": tool_def["name"],
        "description": tool_def.get("description", ""),
        "inputSchema": input_schema
    }


# ─────────────────────────────────────────
# File IPC — reused from bridge.py
# ─────────────────────────────────────────

def get_bridge_dir():
    return os.path.join(os.getcwd(), "Temp", "UnityBridge")


def check_heartbeat(bridge_dir):
    """Check Unity Editor is alive. Returns error string or None."""
    heartbeat_file = os.path.join(bridge_dir, "heartbeat")
    if not os.path.exists(heartbeat_file):
        return "Unity Editor not running (heartbeat not found)"
    try:
        with open(heartbeat_file, "r") as f:
            data = json.load(f)
        heartbeat_ts = int(data["timestamp"]) / 1000.0
        age = time.time() - heartbeat_ts
        if age > HEARTBEAT_MAX_AGE:
            return f"Unity Editor heartbeat stale ({int(age)}s). Editor may be compiling or frozen."
    except (json.JSONDecodeError, KeyError, ValueError) as e:
        return f"Failed to parse heartbeat: {e}"
    return None


def write_atomic(path, content):
    tmp_path = path + ".tmp"
    with open(tmp_path, "w", encoding="utf-8") as f:
        f.write(content)
    os.replace(tmp_path, path)


def call_tool(name, arguments):
    """Execute a Unity Bridge tool via file IPC. Returns MCP content result."""
    bridge_dir = get_bridge_dir()
    commands_dir = os.path.join(bridge_dir, "commands")
    results_dir = os.path.join(bridge_dir, "results")

    # Heartbeat check
    err = check_heartbeat(bridge_dir)
    if err:
        return True, err

    # Write command
    command_id = f"{int(time.time())}-{uuid.uuid4().hex[:8]}"
    command = {"id": command_id, "tool": name, "params": arguments or {}}

    os.makedirs(commands_dir, exist_ok=True)
    os.makedirs(results_dir, exist_ok=True)

    command_file = os.path.join(commands_dir, f"{command_id}.json")
    write_atomic(command_file, json.dumps(command))

    # Poll for result
    result_file = os.path.join(results_dir, f"{command_id}.json")
    elapsed = 0.0

    while not os.path.exists(result_file):
        time.sleep(IPC_POLL_INTERVAL)
        elapsed += IPC_POLL_INTERVAL
        if elapsed >= IPC_TIMEOUT:
            try:
                os.remove(command_file)
            except OSError:
                pass
            return True, f"Timeout after {IPC_TIMEOUT}s waiting for Unity (tool: {name})"

    # Read result
    try:
        with open(result_file, "r", encoding="utf-8") as f:
            result = json.load(f)
    finally:
        try:
            os.remove(result_file)
        except OSError:
            pass

    is_error = result.get("status") != "success"
    message = result.get("message", "")
    return is_error, message


# ─────────────────────────────────────────
# MCP message handler
# ─────────────────────────────────────────

def handle_message(msg, tools_cache):
    method = msg.get("method")
    msg_id = msg.get("id")
    params = msg.get("params", {})

    # ── initialize
    if method == "initialize":
        respond(msg_id, {
            "protocolVersion": MCP_PROTOCOL_VERSION,
            "capabilities": {"tools": {}},
            "serverInfo": {"name": SERVER_NAME, "version": SERVER_VERSION}
        })
        return tools_cache

    # ── initialized (notification, no response)
    if method == "initialized":
        return tools_cache

    # ── ping
    if method == "ping":
        respond(msg_id, {})
        return tools_cache

    # ── tools/list
    if method == "tools/list":
        if tools_cache is None:
            tools_cache = load_tools()
        respond(msg_id, {"tools": tools_cache})
        return tools_cache

    # ── tools/call
    if method == "tools/call":
        name = params.get("name", "")
        arguments = params.get("arguments", {})
        try:
            is_error, message = call_tool(name, arguments)
            result = {"content": [{"type": "text", "text": message}]}
            if is_error:
                result["isError"] = True
            respond(msg_id, result)
        except Exception as e:
            respond(msg_id, {
                "isError": True,
                "content": [{"type": "text", "text": f"Bridge error: {e}"}]
            })
        return tools_cache

    # ── unknown method
    if msg_id is not None:
        respond_error(msg_id, -32601, f"Method not found: {method}")
    return tools_cache


# ─────────────────────────────────────────
# Main loop
# ─────────────────────────────────────────

def main():
    tools_cache = None
    while True:
        try:
            msg = read_message()
            if msg is None:
                break
            tools_cache = handle_message(msg, tools_cache)
        except json.JSONDecodeError:
            continue
        except KeyboardInterrupt:
            break
        except Exception:
            continue


if __name__ == "__main__":
    main()

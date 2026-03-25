#!/usr/bin/env python3
"""
wait_compile: Trigger AssetDatabase.Refresh() and wait for Unity compilation to finish.

Uses bridge.py (file-IPC) to poll Unity Editor state.
Works with any Unity project using unity-ai-bridge.

Usage:
    python3 wait_compile.py [--timeout SECS] [--refresh]

Options:
    --timeout SECS   Max wait time in seconds (default: 120)
    --refresh        Call assets-refresh first to trigger compilation

Exit codes:
    0 = compilation succeeded (or nothing to compile)
    1 = compilation failed (errors found in console)
    2 = timeout
    3 = bridge error

Output (stdout): JSON
    {"ok": true, "elapsed": 32.1, "message": "Compilation succeeded"}
    {"ok": false, "elapsed": 120.0, "message": "Compilation errors found", "errors": [...]}
"""

import json
import os
import sys
import time
import argparse
import subprocess


def get_bridge_py():
    """Locate bridge.py relative to this script."""
    script_dir = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(script_dir, "..", "bridge.py")


def bridge_call(tool, params=None, timeout=60):
    """Call a Unity Bridge tool via bridge.py and return parsed JSON response."""
    bridge = get_bridge_py()
    cmd = [sys.executable, bridge, tool]
    if params:
        cmd.append(json.dumps(params) if isinstance(params, dict) else str(params))

    try:
        r = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout)
        output = r.stdout.strip()
        if output:
            return json.loads(output)
        err = r.stderr.strip()
        if err:
            try:
                return json.loads(err)
            except json.JSONDecodeError:
                return {"status": "error", "message": err}
        return None
    except subprocess.TimeoutExpired:
        return {"status": "error", "message": "bridge call timeout"}
    except Exception as e:
        return {"status": "error", "message": str(e)}


def get_editor_state():
    """Get current editor state via bridge."""
    r = bridge_call("editor-application-get-state")
    if not r or r.get("status") == "error":
        return None
    msg = r.get("message", "{}")
    if isinstance(msg, str):
        try:
            return json.loads(msg)
        except json.JSONDecodeError:
            return None
    return msg


def get_compile_errors():
    """Check console logs for compilation errors."""
    r = bridge_call("console-get-logs")
    if not r or r.get("status") != "success":
        return []

    msg = r.get("message", "[]")
    if isinstance(msg, str):
        try:
            logs = json.loads(msg)
        except json.JSONDecodeError:
            return []
    else:
        logs = msg

    if isinstance(logs, dict):
        logs = logs.get("logs", logs.get("entries", []))
    if not isinstance(logs, list):
        return []

    errors = []
    for entry in logs:
        if not isinstance(entry, dict):
            continue
        log_type = entry.get("logType", entry.get("type", ""))
        if log_type in ("Error", "Exception"):
            text = entry.get("message", "")
            # Filter for compile errors (CS codes) or general errors
            if "CS" in text or "error" in text.lower() or "CompilerError" in text:
                errors.append(text[:300])
    return errors


def output_json(data):
    """Print JSON result to stdout."""
    print(json.dumps(data))


def main():
    parser = argparse.ArgumentParser(
        description="Wait for Unity compilation to finish."
    )
    parser.add_argument("--timeout", type=int, default=120,
                        help="Max wait time in seconds (default: 120)")
    parser.add_argument("--refresh", action="store_true",
                        help="Call assets-refresh first to trigger compilation")
    args = parser.parse_args()

    t0 = time.time()

    # Step 1: optionally trigger AssetDatabase.Refresh()
    if args.refresh:
        sys.stderr.write("[wait-compile] Triggering assets-refresh...\n")
        r = bridge_call("assets-refresh")
        if r and r.get("status") == "error":
            msg = r.get("message", "unknown")
            # assets-refresh may timeout during compilation — that's OK, continue polling
            if "timeout" not in msg.lower():
                output_json({
                    "ok": False,
                    "elapsed": round(time.time() - t0, 1),
                    "message": f"assets-refresh failed: {msg}"
                })
                sys.exit(3)

    # Step 2: initial wait (give Unity time to start compiling)
    initial_wait = min(15, args.timeout // 4)
    sys.stderr.write(f"[wait-compile] Waiting {initial_wait}s for compilation to start...\n")
    time.sleep(initial_wait)

    # Step 3: poll isCompiling / isUpdating
    while time.time() - t0 < args.timeout:
        elapsed = time.time() - t0
        state = get_editor_state()

        if state is None:
            # Bridge unreachable — may be compiling (domain reload kills bridge)
            sys.stderr.write(f"[wait-compile] {elapsed:.0f}s — bridge unreachable (compiling?)\n")
            time.sleep(5)
            continue

        is_compiling = state.get("isCompiling", False)
        is_updating = state.get("isUpdating", False)

        if not is_compiling and not is_updating:
            sys.stderr.write(f"[wait-compile] {elapsed:.0f}s — compilation finished\n")
            break

        sys.stderr.write(f"[wait-compile] {elapsed:.0f}s — still compiling...\n")
        time.sleep(5)
    else:
        output_json({
            "ok": False,
            "elapsed": round(time.time() - t0, 1),
            "message": "Timeout waiting for compilation"
        })
        sys.exit(2)

    # Step 4: check for compilation errors in console
    errors = get_compile_errors()
    elapsed = round(time.time() - t0, 1)

    if errors:
        output_json({
            "ok": False,
            "elapsed": elapsed,
            "message": "Compilation errors found",
            "errors": errors[:10]
        })
        sys.exit(1)
    else:
        output_json({
            "ok": True,
            "elapsed": elapsed,
            "message": "Compilation succeeded"
        })
        sys.exit(0)


if __name__ == "__main__":
    main()

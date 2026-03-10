#!/usr/bin/env python3
"""Tests for bridge.py and mcp_server.py — run with pytest."""

import json
import os
import sys
import tempfile
import time

# Add .claude/skills/unity-bridge/ to path
_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(_root, ".claude", "skills", "unity-bridge"))

import bridge
import mcp_server


# ─────────────────────────────────────────
# bridge.py tests
# ─────────────────────────────────────────

class TestBridge:
    def test_write_atomic(self, tmp_path):
        """Atomic write creates file with correct content."""
        path = str(tmp_path / "test.json")
        bridge.write_atomic(path, '{"key": "value"}')
        with open(path) as f:
            assert json.load(f) == {"key": "value"}
        # .tmp file should not remain
        assert not os.path.exists(path + ".tmp")

    def test_heartbeat_missing(self, tmp_path):
        """Missing heartbeat file raises error."""
        bridge_dir = str(tmp_path / "UnityBridge")
        os.makedirs(bridge_dir)
        try:
            bridge.check_heartbeat(bridge_dir)
            assert False, "Should have called sys.exit"
        except SystemExit:
            pass

    def test_heartbeat_stale(self, tmp_path):
        """Stale heartbeat raises error."""
        bridge_dir = str(tmp_path / "UnityBridge")
        os.makedirs(bridge_dir)
        hb_file = os.path.join(bridge_dir, "heartbeat")
        old_ts = int((time.time() - 60) * 1000)
        with open(hb_file, "w") as f:
            json.dump({"timestamp": str(old_ts)}, f)
        try:
            bridge.check_heartbeat(bridge_dir)
            assert False, "Should have called sys.exit"
        except SystemExit:
            pass

    def test_heartbeat_valid(self, tmp_path):
        """Valid heartbeat passes."""
        bridge_dir = str(tmp_path / "UnityBridge")
        os.makedirs(bridge_dir)
        hb_file = os.path.join(bridge_dir, "heartbeat")
        ts = int(time.time() * 1000)
        with open(hb_file, "w") as f:
            json.dump({"timestamp": str(ts)}, f)
        # Should not raise
        bridge.check_heartbeat(bridge_dir)


# ─────────────────────────────────────────
# mcp_server.py tests
# ─────────────────────────────────────────

class TestMcpServer:
    def test_convert_to_mcp_tool(self):
        """Tool definition converts to MCP schema."""
        tool_def = {
            "name": "test-tool",
            "description": "A test tool",
            "parameters": [
                {"name": "path", "type": "string", "description": "File path", "required": "true"},
                {"name": "count", "type": "integer", "description": "Count", "default": 10}
            ]
        }
        result = mcp_server.convert_to_mcp_tool(tool_def)
        assert result["name"] == "test-tool"
        assert result["description"] == "A test tool"
        assert "path" in result["inputSchema"]["properties"]
        assert "count" in result["inputSchema"]["properties"]
        assert result["inputSchema"]["required"] == ["path"]
        assert result["inputSchema"]["properties"]["count"]["default"] == 10

    def test_convert_enum_tool(self):
        """Enum parameters are split correctly."""
        tool_def = {
            "name": "enum-tool",
            "description": "Tool with enum",
            "parameters": [
                {"name": "mode", "type": "string", "enum": "fast, slow, auto"}
            ]
        }
        result = mcp_server.convert_to_mcp_tool(tool_def)
        assert result["inputSchema"]["properties"]["mode"]["enum"] == ["fast", "slow", "auto"]

    def test_heartbeat_check_no_dir(self, tmp_path):
        """Missing heartbeat returns error string."""
        bridge_dir = str(tmp_path / "nonexistent")
        os.makedirs(bridge_dir)
        err = mcp_server.check_heartbeat(bridge_dir)
        assert err is not None
        assert "not running" in err

    def test_heartbeat_check_valid(self, tmp_path):
        """Valid heartbeat returns None."""
        bridge_dir = str(tmp_path / "UnityBridge")
        os.makedirs(bridge_dir)
        hb_file = os.path.join(bridge_dir, "heartbeat")
        ts = int(time.time() * 1000)
        with open(hb_file, "w") as f:
            json.dump({"timestamp": str(ts)}, f)
        err = mcp_server.check_heartbeat(bridge_dir)
        assert err is None

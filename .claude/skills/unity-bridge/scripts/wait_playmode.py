#!/usr/bin/env python3
"""
wait_playmode: Wait for Unity Play Mode to be fully ready.

Uses bridge.py (file-IPC) to poll Unity Editor state.
Works with any Unity project using unity-ai-bridge — no project-specific dependencies.

Usage:
    python3 wait_playmode.py [--timeout SECS] [--skip-heartbeat-check]

Options:
    --timeout SECS            Max wait time in seconds (default: 120)
    --skip-heartbeat-check    Only wait for isPlaying, skip post-domain-reload heartbeat check

Readiness stages:
    Stage 0: Editor not in Play Mode
    Stage 1: isPlaying=true detected (domain reload may still be in progress)
    Stage 2: Bridge heartbeat responsive (domain reload complete, bridge re-initialized)

Exit codes:
    0 = Play Mode ready (heartbeat responsive)
    1 = Play Mode entered but bridge not responsive (domain reload stuck)
    2 = Timeout waiting for Play Mode
    3 = Bridge error (Unity not running)

Output (stdout): JSON
    {"ok": true, "elapsed": 15.2, "message": "Play Mode ready", "playmode_time": 8.1, "stage": 2}
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


def bridge_call(tool, params=None, timeout=30):
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
        # Check stderr for error JSON
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


def check_heartbeat():
    """Check if the bridge heartbeat is responsive by making a simple call."""
    r = bridge_call("editor-application-get-state")
    if not r:
        return False
    return r.get("status") != "error"


def output_json(data):
    """Print JSON result to stdout."""
    print(json.dumps(data))


def main():
    parser = argparse.ArgumentParser(
        description="Wait for Unity Play Mode to be fully ready."
    )
    parser.add_argument("--timeout", type=int, default=120,
                        help="Max wait time in seconds (default: 120)")
    parser.add_argument("--skip-heartbeat-check", action="store_true",
                        help="Only wait for isPlaying, skip post-domain-reload heartbeat check")
    args = parser.parse_args()

    t0 = time.time()
    playmode_time = None

    # Phase 1: Wait for isPlaying == true
    # Short initial wait — domain reload starts when entering Play Mode
    time.sleep(2)

    while time.time() - t0 < args.timeout:
        elapsed = time.time() - t0
        state = get_editor_state()

        if state and state.get("isPlaying", False):
            if playmode_time is None:
                playmode_time = elapsed
                sys.stderr.write(f"[wait-playmode] Play Mode detected at {elapsed:.1f}s\n")
            break

        sys.stderr.write(f"[wait-playmode] {elapsed:.0f}s — waiting for Play Mode...\n")
        time.sleep(3)
    else:
        output_json({
            "ok": False,
            "elapsed": round(time.time() - t0, 1),
            "message": "Timeout waiting for Play Mode",
            "stage": 0
        })
        sys.exit(2)

    # If skip-heartbeat-check, we're done
    if args.skip_heartbeat_check:
        output_json({
            "ok": True,
            "elapsed": round(playmode_time, 1),
            "message": "Play Mode active (heartbeat check skipped)",
            "playmode_time": round(playmode_time, 1),
            "stage": 1
        })
        sys.exit(0)

    # Phase 2: Wait for bridge to be responsive after domain reload
    # Domain reload temporarily kills the bridge poller, wait for it to reinitialize
    time.sleep(3)

    while time.time() - t0 < args.timeout:
        elapsed = time.time() - t0

        # Check if bridge is responsive
        state = get_editor_state()
        if state and state.get("isPlaying", False):
            sys.stderr.write(f"[wait-playmode] {elapsed:.0f}s — bridge responsive, Play Mode confirmed\n")
            output_json({
                "ok": True,
                "elapsed": round(elapsed, 1),
                "message": "Play Mode ready",
                "playmode_time": round(playmode_time, 1),
                "ready_time": round(elapsed, 1),
                "stage": 2
            })
            sys.exit(0)

        if state is None:
            sys.stderr.write(f"[wait-playmode] {elapsed:.0f}s — bridge not responsive (domain reload?)\n")
        else:
            # Bridge responded but isPlaying is false — Play Mode may have been exited
            sys.stderr.write(f"[wait-playmode] {elapsed:.0f}s — isPlaying=false (Play Mode exited?)\n")
            output_json({
                "ok": False,
                "elapsed": round(elapsed, 1),
                "message": "Play Mode was exited before bridge became ready",
                "playmode_time": round(playmode_time, 1),
                "stage": 1
            })
            sys.exit(1)

        time.sleep(3)

    # Timeout in phase 2
    output_json({
        "ok": False,
        "elapsed": round(time.time() - t0, 1),
        "message": "Play Mode active but bridge not responsive after domain reload",
        "playmode_time": round(playmode_time, 1),
        "stage": 1
    })
    sys.exit(1)


if __name__ == "__main__":
    main()

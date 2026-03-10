#!/usr/bin/env python3
"""
Unity Bridge: 通过文件 IPC 调用 Unity Editor 中的 Bridge 工具。
跨平台 (macOS / Windows / Linux)。

IPC 目录: {projectRoot}/Temp/UnityBridge/

用法:
    python3 bridge.py <tool-name> [json-params]
    python3 bridge.py scene-list-opened
    python3 bridge.py scene-list-opened '{}'
    python3 bridge.py console-get-logs '{"count":10}'
"""

import json
import os
import sys
import time
import uuid

TIMEOUT_SECONDS = 30
POLL_INTERVAL = 0.1
HEARTBEAT_MAX_AGE = 10


def get_project_root():
    """从 cwd 获取项目根目录（工作目录即项目根）"""
    return os.getcwd()


def error_exit(message):
    print(json.dumps({"status": "error", "message": message}), file=sys.stderr)
    sys.exit(1)


def get_bridge_dir(project_root):
    return os.path.join(project_root, "Temp", "UnityBridge")


def check_heartbeat(bridge_dir):
    heartbeat_file = os.path.join(bridge_dir, "heartbeat")
    if not os.path.exists(heartbeat_file):
        error_exit("Unity Editor not running (Temp/UnityBridge/heartbeat not found)")

    try:
        with open(heartbeat_file, "r") as f:
            data = json.load(f)
        heartbeat_ts = int(data["timestamp"]) / 1000.0
        age = time.time() - heartbeat_ts
        if age > HEARTBEAT_MAX_AGE:
            error_exit(f"Unity Editor heartbeat stale ({int(age)}s old). Editor may be compiling or frozen.")
    except (json.JSONDecodeError, KeyError, ValueError) as e:
        error_exit(f"Failed to parse heartbeat: {e}")


def write_atomic(path, content):
    """原子写入：先写 .tmp 再 rename"""
    tmp_path = path + ".tmp"
    with open(tmp_path, "w", encoding="utf-8") as f:
        f.write(content)
    os.replace(tmp_path, path)


# ─────────────────────────────────────────
# 主流程
# ─────────────────────────────────────────

def main():
    if len(sys.argv) < 2:
        error_exit("Usage: bridge.py <tool-name> [json-params]")

    tool_name = sys.argv[1]
    params_str = sys.argv[2] if len(sys.argv) > 2 else "{}"

    # 验证 JSON 参数
    try:
        params = json.loads(params_str)
    except json.JSONDecodeError as e:
        error_exit(f"Invalid JSON params: {e}")

    project_root = get_project_root()
    bridge_dir = get_bridge_dir(project_root)
    commands_dir = os.path.join(bridge_dir, "commands")
    results_dir = os.path.join(bridge_dir, "results")

    # 检查 Unity Editor 在线
    check_heartbeat(bridge_dir)

    # 生成唯一命令 ID
    command_id = f"{int(time.time())}-{uuid.uuid4().hex[:8]}"

    # 构建命令 JSON
    command = {
        "id": command_id,
        "tool": tool_name,
        "params": params
    }

    # 确保目录存在
    os.makedirs(commands_dir, exist_ok=True)
    os.makedirs(results_dir, exist_ok=True)

    # 原子写入命令文件
    command_file = os.path.join(commands_dir, f"{command_id}.json")
    write_atomic(command_file, json.dumps(command))

    # 轮询等待结果
    result_file = os.path.join(results_dir, f"{command_id}.json")
    elapsed = 0.0

    while not os.path.exists(result_file):
        time.sleep(POLL_INTERVAL)
        elapsed += POLL_INTERVAL

        if elapsed >= TIMEOUT_SECONDS:
            # 超时 — 清理命令文件
            try:
                os.remove(command_file)
            except OSError:
                pass
            error_exit(f"Timeout after {TIMEOUT_SECONDS}s waiting for Unity response (tool: {tool_name})")

    # 读取结果
    try:
        with open(result_file, "r", encoding="utf-8") as f:
            result = f.read()
        print(result)
    finally:
        # 清理结果文件
        try:
            os.remove(result_file)
        except OSError:
            pass


if __name__ == "__main__":
    main()

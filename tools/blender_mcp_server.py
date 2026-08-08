#!/usr/bin/env python3
"""블렌더 공식 MCP 애드온의 TCP 소켓을 Claude Code 용 stdio MCP 서버로 감싼다.

왜 있는가
---------
블렌더 5.1+ 공식 애드온(`lab_blender_org/mcp`)이 `localhost:9876` 에 여는 것은
**MCP 가 아니다.** `mcp_to_blender_server.py:304` 가 받는 메시지는
`{"type": "execute"}` 하나뿐이고 JSON-RPC 도 `initialize` 도 `tools/list` 도 없다.
프레이밍도 NUL(`\\0`) 이라 MCP 규격과 다르다.

Claude Code 는 stdio · SSE · HTTP 만 지원하므로 저 소켓에 직접 붙일 수단이 없고,
애드온에는 클라이언트 쪽 stdio 서버가 들어 있지 않다(`cli.py` 는 블렌더를
`--background` 로 띄울 때 쓰는 **블렌더 쪽** 서버다). 그 빈 칸이 이 파일이다.

    Claude Code ──stdio JSON-RPC──▶ 이 파일 ──NUL 프레이밍 TCP──▶ 블렌더

의존성이 없다. MCP stdio 는 줄바꿈으로 구분된 JSON-RPC 2.0 일 뿐이라
표준 라이브러리만으로 충분하다 — `pip install` 이 필요하면 블렌더를 띄운 채
네트워크를 기다리게 되고, 그건 이 저장소가 피하려던 실패다.

⚠ stdout 은 프로토콜 전용이다. 진단 출력은 전부 stderr 로 보낸다.
  print() 한 줄이 섞이면 클라이언트가 세션 전체를 버린다.

등록
----
    claude mcp add blender --scope project -- python3 tools/blender_mcp_server.py

수동 점검(대화형):
    echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | python3 tools/blender_mcp_server.py
"""

from __future__ import annotations

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from blender_bridge import (  # noqa: E402
    DEFAULT_HOST,
    DEFAULT_PORT,
    DEFAULT_TIMEOUT,
    BlenderBridgeError,
    run,
)

SERVER_NAME = "blender"
SERVER_VERSION = "1.0.0"

# 클라이언트가 요구한 버전을 그대로 돌려주되, 모르는 값이면 이걸로 떨어진다.
FALLBACK_PROTOCOL = "2025-06-18"
KNOWN_PROTOCOLS = {"2024-11-05", "2025-03-26", "2025-06-18"}

HOST = os.environ.get("BLENDER_MCP_HOST", DEFAULT_HOST)
PORT = int(os.environ.get("BLENDER_MCP_PORT", DEFAULT_PORT))

TOOLS = [
    {
        "name": "blender_execute",
        "description": (
            "블렌더 안에서 파이썬(bpy)을 실행하고 stdout 과 마지막 표현식 값을 돌려준다. "
            "씬 조회, 오브젝트 생성·변형, 머티리얼 설정, 익스포트 등 블렌더에서 할 수 있는 "
            "모든 작업의 단일 진입점이다. 블렌더가 실행 중이고 MCP 애드온이 Running 이어야 한다."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "code": {
                    "type": "string",
                    "description": "실행할 파이썬 소스. 여러 줄 가능. 결과는 print() 하거나 마지막 줄을 표현식으로 둔다.",
                },
                "strict_json": {
                    "type": "boolean",
                    "description": (
                        "true 면 마지막 표현식 값이 JSON 직렬화 가능해야 한다. "
                        "false(기본)면 블렌더가 repr 로 떨어뜨려 주므로 탐색용으로 편하다."
                    ),
                    "default": False,
                },
                "timeout": {
                    "type": "number",
                    "description": "응답 대기 초. 굽기·시뮬레이션처럼 오래 걸리는 작업은 늘린다.",
                    "default": DEFAULT_TIMEOUT,
                },
            },
            "required": ["code"],
        },
    },
    {
        "name": "blender_status",
        "description": (
            "브리지 연결과 현재 블렌더 상태를 점검한다 — 버전, 열린 .blend 파일, "
            "씬 이름, 오브젝트 수. 무엇이 잘못됐는지 확인할 때 먼저 부른다."
        ),
        "inputSchema": {"type": "object", "properties": {}},
    },
]

STATUS_SNIPPET = """\
import bpy, json
print(json.dumps({
    "blender_version": bpy.app.version_string,
    "blend_file": bpy.data.filepath or "(unsaved)",
    "scene": bpy.context.scene.name if bpy.context.scene else None,
    "object_count": len(bpy.data.objects),
    "mode": bpy.context.mode if bpy.context else None,
}, ensure_ascii=False, indent=2))
"""


def log(message: str) -> None:
    """stdout 은 프로토콜 전용이므로 진단은 전부 이쪽으로."""
    print("[blender-mcp] {:s}".format(message), file=sys.stderr, flush=True)


def _render(response: dict) -> str:
    """블렌더 응답 dict 를 사람이 읽을 텍스트로. 브리지 CLI 와 같은 우선순위를 쓴다."""
    parts: list[str] = []
    for key in ("stdout", "output", "result", "message"):
        value = response.get(key)
        if not value:
            continue
        parts.append(value if isinstance(value, str) else json.dumps(value, ensure_ascii=False, indent=2))
    if not parts:
        # 출력도 값도 없는 성공 — 조용히 끝났다는 사실 자체를 알려 준다.
        return "(출력 없음, status={!r})".format(response.get("status", "ok"))
    return "\n".join(parts)


def _call_tool(name: str, args: dict) -> dict:
    """tools/call 의 result. 도구 오류는 예외가 아니라 isError 로 돌려준다."""
    if name == "blender_status":
        code, strict_json, timeout = STATUS_SNIPPET, False, 30.0
    elif name == "blender_execute":
        code = args.get("code")
        if not isinstance(code, str) or not code.strip():
            return {
                "content": [{"type": "text", "text": "code 가 비어 있다. 실행할 파이썬 소스가 필요하다."}],
                "isError": True,
            }
        strict_json = bool(args.get("strict_json", False))
        timeout = float(args.get("timeout", DEFAULT_TIMEOUT))
    else:
        return {
            "content": [{"type": "text", "text": "알 수 없는 도구: {!r}".format(name)}],
            "isError": True,
        }

    try:
        response = run(code, host=HOST, port=PORT, timeout=timeout, strict_json=strict_json)
    except BlenderBridgeError as ex:
        return {"content": [{"type": "text", "text": str(ex)}], "isError": True}
    except Exception as ex:  # 소켓이 아닌 곳에서 터져도 세션은 살려 둔다.
        return {
            "content": [{"type": "text", "text": "브리지 호출이 실패했다: {!s}".format(ex)}],
            "isError": True,
        }

    is_error = response.get("status") == "error"
    text = _render(response)
    if is_error:
        # 블렌더가 준 트레이스백을 통째로 넘긴다 — 줄 번호가 있어야 고칠 수 있다.
        text = json.dumps(response, ensure_ascii=False, indent=2)
    return {"content": [{"type": "text", "text": text}], "isError": is_error}


def _handle(request: dict) -> dict | None:
    """요청 하나를 처리한다. 알림(id 없음)이면 None 을 돌려 응답을 생략한다."""
    method = request.get("method")
    req_id = request.get("id")
    is_notification = req_id is None

    if method == "initialize":
        requested = (request.get("params") or {}).get("protocolVersion")
        protocol = requested if requested in KNOWN_PROTOCOLS else FALLBACK_PROTOCOL
        result = {
            "protocolVersion": protocol,
            "capabilities": {"tools": {"listChanged": False}},
            "serverInfo": {"name": SERVER_NAME, "version": SERVER_VERSION},
        }
    elif method == "tools/list":
        result = {"tools": TOOLS}
    elif method == "tools/call":
        params = request.get("params") or {}
        result = _call_tool(params.get("name", ""), params.get("arguments") or {})
    elif method == "ping":
        result = {}
    elif method in ("resources/list", "prompts/list"):
        # 광고하지 않은 기능이라도 물어 오는 클라이언트가 있다. 빈 목록이 오류보다 낫다.
        result = {"resources": []} if method == "resources/list" else {"prompts": []}
    elif isinstance(method, str) and method.startswith("notifications/"):
        return None
    else:
        if is_notification:
            return None
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "error": {"code": -32601, "message": "Method not found: {!r}".format(method)},
        }

    if is_notification:
        return None
    return {"jsonrpc": "2.0", "id": req_id, "result": result}


def main() -> int:
    log("stdio 서버 시작. 블렌더 소켓 {:s}:{:d}".format(HOST, PORT))
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            request = json.loads(line)
        except json.JSONDecodeError as ex:
            log("JSON 파스 실패: {!s}".format(ex))
            sys.stdout.write(json.dumps({
                "jsonrpc": "2.0",
                "id": None,
                "error": {"code": -32700, "message": "Parse error: {!s}".format(ex)},
            }) + "\n")
            sys.stdout.flush()
            continue

        # 배치 요청은 클라이언트가 거의 안 쓰지만 규격에는 있다.
        batch = request if isinstance(request, list) else [request]
        responses = []
        for item in batch:
            if not isinstance(item, dict):
                continue
            try:
                response = _handle(item)
            except Exception as ex:  # 어떤 이유로든 서버가 죽지는 않게 한다.
                log("처리 중 예외: {!s}".format(ex))
                response = None
                if item.get("id") is not None:
                    response = {
                        "jsonrpc": "2.0",
                        "id": item.get("id"),
                        "error": {"code": -32603, "message": "Internal error: {!s}".format(ex)},
                    }
            if response is not None:
                responses.append(response)

        for response in responses:
            sys.stdout.write(json.dumps(response, ensure_ascii=False) + "\n")
        if responses:
            sys.stdout.flush()

    log("stdin 이 닫혔다. 종료.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

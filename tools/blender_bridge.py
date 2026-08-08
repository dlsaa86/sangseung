#!/usr/bin/env python3
"""블렌더 공식 MCP 애드온(lab_blender_org/mcp)의 소켓 브리지에 직접 붙는 클라이언트.

왜 있는가
---------
블렌더 5.1+ 의 공식 MCP 애드온은 `localhost:9876` 에 TCP 소켓을 열고
**NUL(`\\0`) 로 프레이밍한 JSON** 을 주고받는다(`mcp_to_blender_server.py:206`).
MCP 프로토콜 자체가 아니라 「블렌더 안에서 파이썬을 실행한다」는 한 가지 일만 한다.

Claude Code 에 MCP 서버로 등록하지 않아도 이 파일만으로 블렌더를 조회·조작할 수 있다.
Unity 쪽 `Unity_RunCommand` 와 같은 위치에 있는 도구다.

⚠ NUL 을 안 붙이면 서버가 요청이 안 끝난 줄 알고 계속 기다린다 — 응답 0바이트로 보인다.
   (2026-08-08 에 이걸로 30분 헤맸다. 커뮤니티 `ahujasid/blender-mcp` 와는 다른 프로토콜이다.)

사용
----
    python3 tools/blender_bridge.py -c "import bpy; print(bpy.app.version_string)"
    python3 tools/blender_bridge.py -f some_script.py
    echo "print(1+1)" | python3 tools/blender_bridge.py

    from tools.blender_bridge import run
    print(run("import bpy; print(len(bpy.data.objects))"))
"""

from __future__ import annotations

import argparse
import json
import socket
import sys

DEFAULT_HOST = "localhost"
DEFAULT_PORT = 9876
DEFAULT_TIMEOUT = 120.0


class BlenderBridgeError(RuntimeError):
    """브리지에 붙지 못했거나 블렌더가 오류를 돌려준 경우."""


def run(
        code: str,
        *,
        host: str = DEFAULT_HOST,
        port: int = DEFAULT_PORT,
        timeout: float = DEFAULT_TIMEOUT,
        strict_json: bool = False,
) -> dict:
    """블렌더 안에서 `code` 를 실행하고 응답 dict 를 돌려준다.

    strict_json=True 면 마지막 표현식의 값이 JSON 직렬화 가능해야 한다.
    False 면 블렌더가 `repr` 로 떨어뜨려 주므로 탐색용으로는 이쪽이 편하다.
    """
    payload = json.dumps({
        "type": "execute",
        "code": code,
        "strict_json": strict_json,
    }).encode("utf-8") + b"\0"

    try:
        conn = socket.create_connection((host, port), timeout=timeout)
    except OSError as ex:
        raise BlenderBridgeError(
            "블렌더 브리지에 붙지 못했다 ({:s}:{:d}): {:s}\n"
            "  · 블렌더가 실행 중인가\n"
            "  · Preferences → Add-ons → MCP 가 켜져 있고 서버가 Running 인가\n"
            "  · Preferences → System → Network → Allow Online Access 가 켜져 있는가"
            .format(host, port, str(ex))
        ) from ex

    try:
        conn.settimeout(timeout)
        conn.sendall(payload)

        # 응답도 NUL 로 끝난다. 그 전까지 모은다.
        buf = bytearray()
        while True:
            try:
                chunk = conn.recv(65536)
            except socket.timeout as ex:
                raise BlenderBridgeError(
                    "{:.0f}초 안에 응답이 없다. 블렌더가 모달 대화상자에 잠겼거나 "
                    "무거운 작업 중일 수 있다 (받은 바이트 {:d})".format(timeout, len(buf))
                ) from ex
            if not chunk:
                raise BlenderBridgeError(
                    "NUL 종결자를 받기 전에 연결이 끊겼다 (받은 바이트 {:d})".format(len(buf))
                )
            buf += chunk
            if buf.endswith(b"\0"):
                break
    finally:
        conn.close()

    return json.loads(buf[:-1].decode("utf-8"))


def _main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="블렌더 MCP 소켓 브리지로 파이썬을 실행한다.")
    src = parser.add_mutually_exclusive_group()
    src.add_argument("-c", "--code", help="실행할 파이썬 코드.")
    src.add_argument("-f", "--file", help="실행할 파이썬 파일. 없으면 stdin 을 읽는다.")
    parser.add_argument("--host", default=DEFAULT_HOST)
    parser.add_argument("--port", type=int, default=DEFAULT_PORT)
    parser.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT)
    parser.add_argument("--strict-json", action="store_true")
    parser.add_argument("--raw", action="store_true", help="응답 JSON 을 그대로 출력한다.")
    args = parser.parse_args(argv)

    if args.code is not None:
        code = args.code
    elif args.file is not None:
        with open(args.file, "r", encoding="utf-8") as fh:
            code = fh.read()
    else:
        code = sys.stdin.read()

    try:
        response = run(
            code,
            host=args.host,
            port=args.port,
            timeout=args.timeout,
            strict_json=args.strict_json,
        )
    except BlenderBridgeError as ex:
        print(str(ex), file=sys.stderr)
        return 2

    if args.raw:
        print(json.dumps(response, indent=2, ensure_ascii=False))
        return 0 if response.get("status") != "error" else 1

    # 사람이 읽기 좋은 형태 — 블렌더가 잡아 준 stdout 을 먼저 보여 준다.
    for key in ("stdout", "output", "result", "message"):
        value = response.get(key)
        if value:
            print(value if isinstance(value, str) else json.dumps(value, indent=2, ensure_ascii=False))
    if response.get("status") == "error":
        print(json.dumps(response, indent=2, ensure_ascii=False), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(_main(sys.argv[1:]))

# -*- coding: utf-8 -*-
"""씬의 MonoBehaviour 블록을 훑어 `fileID: 0` 인 참조 필드를 컴포넌트별로 모은다.

왜: 값 층위 테스트는 「함수가 옳은 문자열을 만드는가」를 증명하지만 「그 문자열이
화면에 닿는가」는 증명하지 못한다. 뷰에 배선하기 전에 그 필드가 씬에서 비어 있는지
먼저 세지 않으면 죽은 경로에 붙이게 된다 — 2026-08-02 에 실제로 그렇게 했다.

빈 배열(`[]`)과 `m_` 로 시작하는 Unity 내장 필드는 세지 않는다. 전자는 「아직 안 채움」과
「필요 없음」이 구분되지 않고, 후자는 우리 배선이 아니다.
"""
import io, re, sys, collections

path = sys.argv[1] if len(sys.argv) > 1 else \
    "B:/PROJECT_NEW_BORN/Upandup_DDD/Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity"

lines = io.open(path, encoding="utf-8", errors="replace").read().split("\n")

comp = None
empties = collections.OrderedDict()
counts = collections.Counter()

for raw in lines:
    line = raw.rstrip("\r")
    if line.startswith("--- !u!"):
        comp = None
        continue
    m = re.match(r"^  m_EditorClassIdentifier:\s*(.+)$", line)
    if m:
        comp = m.group(1).strip().split("::")[-1]
        continue
    if comp is None:
        continue
    m = re.match(r"^  (_[A-Za-z0-9_]+):\s*\{fileID:\s*0\}\s*$", line)
    if m:
        empties.setdefault(comp, []).append(m.group(1))
        counts[comp] += 1

if not empties:
    print("빈 참조 없음")
    sys.exit(0)

total = sum(counts.values())
print("씬의 비어 있는 직렬화 참조 — 컴포넌트 %d 개 · 필드 %d 개" % (len(empties), total))
print("")
for c in sorted(empties, key=lambda k: (-counts[k], k)):
    print("%-38s %2d   %s" % (c, counts[c], ", ".join(empties[c])))

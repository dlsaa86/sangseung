---
name: visual-verify
description: 시각·연출 변경을 캡처 하네스로 검증하는 회귀 루프. 구현 → 캡처 → 하드 게이트 → 블라인드 쌍대비교 → 개선된 경우에만 채택. 사용자가 "검증해줘", "비주얼 확인", "이거 나아졌어?", "회귀 확인"이라고 하거나, 화면에 보이는 것을 바꾸는 작업을 마쳤을 때 사용. 로직 전용 버그 수정에는 쓰지 않는다.
---

# visual-verify — 시각 회귀 검증 루프

## 이 스킬이 막으려는 실패

1. **자기 평가의 아부** — 구현자가 자기 결과를 평가하면 거의 항상 통과시킨다.
2. **조건이 다른 비교** — 해상도·시점·시간이 다른 두 스크린샷 비교는 노이즈다.
3. **점수의 허위 정밀도** — "공포 분위기 7점 vs 8점"은 재현되지 않는다. 필요한 판정은 "나아졌나" 하나뿐이다.
4. **조용한 후퇴** — 한 항목을 고치다 다른 항목이 나빠진 걸 아무도 못 본다.

## 쓰지 않는 경우

- 화면에 안 보이는 로직 버그 → PlayMode 테스트로 끝낸다
- 컴파일 에러 → 그냥 고친다
- 아직 베이스라인이 없는 최초 구현 → 1회 캡처해서 베이스라인만 세운다

캡처 1회는 플레이 모드 진입 포함 수십 초다. 모든 커밋에 돌리지 말 것.

## 사전 준비

```bash
# 프로젝트 루트 탐색 (cwd가 Library/ 하위일 수 있다)
d="$(pwd)"; while [ "$d" != "/" ]; do
  [ -d "$d/Assets" ] && [ -d "$d/ProjectSettings" ] && echo "ROOT=$d" && break
  d="$(dirname "$d")"
done
```

평가 기준은 `$ROOT/.claude/visual-criteria.md`에서 읽는다. **기준을 이 스킬에 하드코딩하지 말 것** — 아트 방향은 프로젝트 것이지 절차의 것이 아니다.

## 1단계 — 구현

변경을 적용한다. 되돌릴 수 있어야 하므로 **작업 전 반드시 커밋되어 있거나 diff를 확보**한다.

```bash
git -C "$ROOT" status --porcelain   # 지저분하면 먼저 정리
```

## 2단계 — 캡처

Unity MCP로 하네스를 돌린다. 대상 CaptureSet은 검증 항목에 맞는 것을 쓴다.

```csharp
// Unity_RunCommand
using UnityEngine; using UnityEditor; using UnityEditor.SceneManagement;
using Ascend.CaptureHarness; using Ascend.CaptureHarness.EditorTools;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.isDirty) { result.LogWarning("씬 미저장 — 중단"); return; }
        var set = AssetDatabase.LoadAssetAtPath<CaptureSet>("Assets/CaptureHarness/<SET>.asset");
        if (set == null) { result.LogError("CaptureSet 없음"); return; }
        CaptureHarnessRunner.Run(set);
        result.Log("capture requested");
    }
}
```

완료 대기 — **폴링하지 말고 백그라운드로 돌린 뒤 알림을 받는다**:

```bash
prev=$(cat "$ROOT/Captures/last-run.txt" 2>/dev/null)
for i in $(seq 1 60); do
  rid=$(cat "$ROOT/Captures/last-run.txt" 2>/dev/null)
  [ "$rid" != "$prev" ] && [ -f "$ROOT/Captures/$rid/manifest.json" ] && { echo "DONE $rid"; break; }
  sleep 2
done
```

## 3단계 — 하드 게이트 (판단 이전에 기계적으로)

주관적 평가 **전에** 통과해야 한다. 하나라도 실패하면 시각 평가로 넘어가지 않는다.

```bash
rid=$(cat "$ROOT/Captures/last-run.txt")
m="$ROOT/Captures/$rid/manifest.json"
python -c "
import json,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
fail=[]
if not d['completed']: fail.append('run did not complete')
if d['errorCount']>0: fail.append(f\"{d['errorCount']} error(s): {d['errors'][:3]}\")
if not d['shots']: fail.append('no shots written')
print('FAIL: '+'; '.join(fail) if fail else 'PASS')
" "$m"
```

| 게이트 | 실패 시 |
|---|---|
| `completed == true` | 런이 멈춘 것. 하네스 문제부터 해결 |
| `errorCount == 0` | **에러가 있는 런은 비교 기준이 될 수 없다.** 먼저 고친다 |
| 기대한 샷이 전부 있음 | CaptureSet과 실제 결과 불일치 |
| `machineFingerprint`가 베이스라인과 동일 | 아래 참조 |

### 기기 지문 대조 (반드시)

```bash
b=$(cat "$ROOT/Captures/baseline.txt" 2>/dev/null)
python -c "
import json,sys
cur=json.load(open(sys.argv[1],encoding='utf-8'))['machineFingerprint']
base=json.load(open(sys.argv[2],encoding='utf-8'))['machineFingerprint']
print('MATCH' if cur==base else f'MISMATCH\n  base: {base}\n  cur:  {cur}')
" "$m" "$ROOT/Captures/$b/manifest.json"
```

**MISMATCH면 비교를 중단한다.** OS·GPU·그래픽 API가 다르면 렌더 결과가 비트 단위로 다르다 — 실제 변경이 없어도 전부 "달라짐"으로 나온다. 이 경우 그 기기에서 베이스라인을 새로 세우고 시작한다.

## 4단계 — 블라인드 쌍대비교

**이 단계의 설계가 전부다. 아래를 어기면 평가는 무의미해진다.**

### 준비

```bash
tmp=$(mktemp -d)
# 동전 던지기로 A/B 배정 — 매 평가마다 새로
if [ $((RANDOM % 2)) -eq 0 ]; then
  cp "$ROOT/Captures/$b/00_shot.png"   "$tmp/A.png"; cp "$ROOT/Captures/$rid/00_shot.png" "$tmp/B.png"; map="A=baseline B=current"
else
  cp "$ROOT/Captures/$rid/00_shot.png" "$tmp/A.png"; cp "$ROOT/Captures/$b/00_shot.png"   "$tmp/B.png"; map="A=current B=baseline"
fi
echo "$map"   # 나만 안다. 평가자에게 절대 넘기지 않는다
```

### 평가자 호출 규칙

별도 에이전트를 새로 띄운다 (`Agent`, `subagent_type: general-purpose`). 이 스킬을 호출한 것 자체가 사용자의 요청이므로 위임이 허용된다.

평가자에게 **주지 않는 것**:
- 어느 쪽이 현재 빌드인지
- 무엇을 바꿨는지, 왜 바꿨는지
- 이전 평가 결과
- "개선했다", "수정했다" 같은 표현 일체

프롬프트 골자:

```
두 이미지를 비교해줘. 같은 게임의 두 가지 렌더 결과다.
- <tmp>/A.png
- <tmp>/B.png

아래 각 기준마다 A와 B 중 어느 쪽이 나은지 하나 고르고, 이유를 한 문장으로 써라.
차이를 못 느끼면 "동일"이라고 해라. 억지로 차이를 만들어내지 마라.

<.claude/visual-criteria.md 의 기준 목록>

마지막에 종합해서 A/B/동일 중 하나와 근거를 써라.
```

**절대 점수(10점 만점)를 요구하지 않는다.** 비전 모델의 절대 점수는 재현되지 않는다. 필요한 건 순위 하나다.

### 선택: 레퍼런스 포함

상용 레퍼런스와의 격차를 보려면 C.png로 추가한다. 단 **채택 판정은 A vs B로만** 한다. 레퍼런스를 이기는 것은 목표가 아니다 — 프로젝트 고유의 방향을 유지하면서 격차가 큰 항목부터 줄인다.

## 5단계 — 판정

| 결과 | 조치 |
|---|---|
| current 우세 | **채택.** `Captures/baseline.txt`를 새 runId로 갱신 |
| baseline 우세 | **되돌린다.** 부분적으로 좋아도 순 후퇴면 채택하지 않는다 |
| 동일 | 시각적 이득 없음. 성능·코드 품질 등 다른 근거가 있을 때만 유지 |

```bash
echo "$rid" > "$ROOT/Captures/baseline.txt"   # 채택할 때만
```

되돌릴 때는 무엇이 왜 후퇴했는지 **구체적 수정 항목**으로 바꿔 기록한다. "별로다"는 다음 반복에 쓸 수 없다.

## 6단계 — 반복 상한

**항목당 3회.** 3회 후에도 기준을 통과하지 못하면 반복을 멈추고 보고한다:

- 무엇을 시도했고 각각 왜 실패했는지
- 추정 근본 원인
- 필요한 구조 변경 (에셋 교체, 셰이더 작성, 씬 재구성 등)

같은 층위에서 네 번째를 시도하지 않는다. 3회 실패는 대개 접근이 틀렸다는 신호지 노력이 부족하다는 신호가 아니다.

## 완료 판정

주관적 "완벽함"이 아니라 아래로 판정한다:

- [ ] Critical/Error 로그 0 (`manifest.errorCount == 0`)
- [ ] 핵심 플레이 루프 완주 가능
- [ ] 입력 불가·진행 불가 버그 0
- [ ] 목표 성능 예산 충족
- [ ] 블라인드 비교에서 직전 승인 빌드 대비 후퇴 없음
- [ ] 캡처와 manifest가 `Captures/`에 남아 있음

## 주의

- `Captures/`는 gitignore 대상이다. 증거는 로컬에만 남는다. 공유가 필요하면 별도로 첨부한다.
- 베이스라인은 기기에 종속된다. 기기를 옮기면 `baseline.txt`를 그 기기에서 다시 세운다.
- 캔버스가 Screen Space Overlay면 하네스가 캡처 중 카메라로 리디렉트한다. UI가 안 보이면 하네스 로그의 `Redirected N canvas(es)`를 먼저 확인한다.

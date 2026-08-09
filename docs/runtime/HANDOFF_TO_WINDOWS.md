# Mac → Windows 인계 (2026-08-09)

**브랜치**: `agent/autonomous-polish-20260809` · `main` 대비 **61 커밋 앞**
**마지막 커밋**: `1303b66` 벽의 딱딱한 원 — 밴드 완화가 16장에서 꺼져 있었다

---

## 0. 가장 먼저 — 이어서 하려면 이것부터

1. **브랜치를 받는다.** `main` 이 아니라 `agent/autonomous-polish-20260809` 다
2. Unity 6000.5.5f1 로 연다. `Library/` 가 없으므로 **첫 임포트에 시간이 걸린다**
3. `Ascend/Run Self Tests` 를 돌려 **626 PASS / 3 FAIL** 이 나오는지 확인한다.
   숫자가 다르면 환경 문제이지 코드 문제가 아니다 — 먼저 그것부터 본다

---

## 1. 끝난 것 (전부 커밋됨)

| 항목 | 근거 |
|---|---|
| 구슬 3종이 실루엣만으로 갈린다 | `SymbolShapeFactory` 관통 구멍. 채움률 1.000 → 0.704 |
| 구슬 팝인/팝아웃 제거 | `SpinBoardView` 스케일 전이. 프레임당 0 B 유지 |
| 레버 외곽선 T자화 | `_meshOutline` 대상별 스위치 |
| 유령 콜라이더 8개 비활성 | 참조 있는 3개는 유지 |
| `PowerTank` 조준을 보이는 게이지로 이전 | 안 쓰는 게 아니라 **안 보이는** 것이었다 |
| 과수확을 빌드 조건 뒤로 | 「전력 **그리고** 열쇠」. 열쇠 2갈래 |
| Duo·Cross 종류 간 패턴 판정 | 2.5× / 5.0× |
| 밸런스 | 626 PASS / 3 FAIL (밤 시작 610 / 4) |
| 벽의 딱딱한 원 제거 | `_BandSoftEnabled` 16장 |

---

## 2. ⚠ 진행 중이라 **아직 커밋되지 않은 것** — 이게 인계의 핵심

### 블렌더 산출물이 맥 로컬 임시 폴더에만 있다

```
<scratchpad>/ELV_Cabin_AD47_fixed.fbx      ← UV 수정 + 게이지 0~100% + SpinGauge + 구슬
```

**이 폴더는 세션이 끝나면 사라진다. 윈도우로 넘어가지 않는다.**
지금 상태로는 **`LIGHT` 가 빠져 있어 넣으면 안 되는 파일**이라 커밋하지 않았다(§3 참조).

**윈도우에서 할 일**: 블렌더에서 **다시 export 한다.** 아래 절차를 그대로 따르면 된다.

### `.blend` 는 iCloud 에 있다 — 윈도우에서도 열린다

```
mac      ~/Library/Mobile Documents/com~apple~CloudDocs/02_Resources/ElevPanel_v10/SM_ElevCab_Panel_AD57.blend
windows  iCloudDrive\02_Resources\ElevPanel_v10\SM_ElevCab_Panel_AD57.blend
```

이 파일에는 **UV 수정 · 게이지 0~100% 연장 · SpinGauge 재배치 · 구슬 3종이 전부 저장돼 있다**
(23,189,186 bytes). 백업도 같은 폴더/스크래치패드에 있다.
⚠ 파일명이 `AD57` 인데 Unity FBX 는 `AD47` 이다 — **확인했고 같은 원본이 맞다.**

---

## 3. 🔴 export 할 때 반드시 지킬 것 — 이걸 몰라서 캐빈을 2.5배 어둡게 만들었다

### `.blend` 안의 텍스트 블록 `AD48_EXPORT` 가 유일한 정답이다

**`tools/blender/build_cabin.py` 의 export 인자를 쓰지 마라.** 그건 현재 에셋을 만든
경로가 아니다. 스크립트가 남아 있다는 것과 그 스크립트가 지금 에셋을 만들었다는 것은
다른 문제다.

`AD48_EXPORT` 가 하는 일:

1. 굽기 UV 는 평소 **슬롯 0 에 있지 않다.** 슬롯 0 에는 저작 UV(`UVMap`/`UVBox`)가 있고
   `active_render` 도 거기 있다 — 이것이 **정상 저장 상태**다
2. export **직전에만** 슬롯 0 좌표를 굽기값으로 바꿔치기한다
3. export
4. 저장해 둔 원본 좌표로 **loop 단위 정확히 복원**한다

이 절차를 건너뛰면 셰이더가 읽는 첫 UV(TEXCOORD0)에 월드 스케일 좌표가 들어가
구운 텍스처가 아틀라스 밖을 샘플링한다 → **캐빈이 통째로 검어진다.**

### `object_types` 는 `{'MESH', 'EMPTY', 'LIGHT'}` 다

셋 다 필요하다. 하나라도 빠지면 조용히 망가진다.

| 빠뜨리면 | 무슨 일이 나나 |
|---|---|
| `EMPTY` | `SOCKET_ElevPanel` 이 사라지고 **그 아래 오버라이드 138개가 통째로 날아간다** |
| `LIGHT` | `LT_CabBulb` · `LT_SoulSpill` 이 껍데기만 남는다 — **게임의 조명 전부다** |

### export 후 검증 — **이름·개수가 아니라 값을 봐야 한다**

```bash
strings <FBX> | grep -c '^Light$'            #  2
strings <FBX> | grep -c 'SOCKET_ElevPanel'   #  2
strings <FBX> | grep -c 'UVBake'             # 57
strings <FBX> | grep -c 'SM_SpinGauge'       #  6
```

그리고 **새 격리 씬에 재import 해서** 표본 메시(`SM_Cab_Ceiling` · `SM_Cab_FloorTrim` ·
`SM_Cab_Wall_Back`)의 **슬롯 0 UV 범위가 0~1 인지** 재라.

> 지난번 실패에서 이름·개수·문자열 존재 검사는 **전부 통과했는데도** 값이 틀렸다.
> **채널이 있는지가 아니라 좌표가 무엇인지를 봐야 한다.**

### Unity 임포트 후 판정

```
프리팹 오버라이드  138   (기준선: docs/runtime/FBX_OVERRIDE_BASELINE.md)
추가 GameObject      4   AD47_PowerBarPivot · ShellCollision · BoardCells · FX_MachineAnchor
```

그리고 **플레이어 눈높이에서 렌더해 평균 밝기를 재라. 0.1028 근처여야 한다.**
0.04 대가 나오면 굽기 UV 가 깨진 것이다 — 넣지 말고 되돌린다.

---

## 4. 임포트가 성공하면 곧바로 할 일

`SM_Gauge_Fill` 이 이제 **0~100% 전체**를 덮는다(오차 0.0mm). 그러면
`InstrumentPanelView.cs:99` 의 `_barWidth` 를 **1.72 → 1.0** 으로 바꿔야 한다.
1.72 는 67% 짜리 짧은 메시를 억지로 늘리던 보정값이고, 모델이 고쳐졌으니 보정이 이중이 된다.

⚠ **씬에 직렬화된 값이 코드 기본값을 이긴다.** 코드만 고치면 기존 컴포넌트는 안 바뀐다 —
씬의 `InstrumentPanelView` 인스턴스 값을 직접 바꿔야 한다.

---

## 5. 사용자 결정 대기 (아무것도 진행하지 않았다)

1. **구슬을 절차형으로 갈지 블렌더 메시로 갈지** — `PENDING_DECISIONS.md` `P-20260809-01`.
   현재는 절차형이 게임에 쓰이고 블렌더 것은 **렌더러만 꺼 둔 채 남아 있다**(삭제 안 함)
2. **Duo·Cross 는 1~5층에서 정의상 못 뜬다** — 그 구간 저항 풀이 흡수체 하나뿐이라
   「두 종류의 상대 배치」가 성립하지 않는다. 초반에도 쓰려면 증식체를 앞당기거나 4번째 심볼
3. **Cross 실측 발생률 0.02~0.04%** (설계 문서 추정 3%) — 노려서 쓰는 전략이 아니라 잭팟
4. 남은 테스트 3 — 전부 밤 시작 전부터 있던 것 (적재 정책 4/6칸 · 다층 상승 0회 ·
   무계약 6층 도달 1/6)

---

## 6. 윈도우 환경 주의

- **`Captures/baseline.txt` 는 기기 종속이다.** OS·GPU·그래픽 API 가 다르면 렌더가 비트
  단위로 다르다. 윈도우에서 **베이스라인을 새로 세워야 한다** — manifest 의
  `machineFingerprint` 가 이를 강제한다
- `.claude/hooks/` 의 스크립트 21개는 **배선이 끊겨 있다**(2026-08-08 순정 전환).
  윈도우에서는 PowerShell 이 있으므로 되살릴 수 있지만, 되살리려면
  `.ps1` 을 **UTF-8 BOM 으로 저장**해야 한다. BOM 없으면 PowerShell 5.1 이 ANSI 로 읽어
  한글 문자열이 깨지고 그 바이트가 따옴표를 삼켜 **파일 전체가 파스 오류**가 된다
- `Logs/Editor.log` 는 **프로젝트 상대 경로**다. `%LOCALAPPDATA%\Unity\Editor\Editor.log` 아니다
- `Library/` `Temp/` `Logs/` `UserSettings/` `Captures/` 는 gitignore 대상이라 안 넘어간다
- **폰트 아틀라스 2장을 커밋에 섞지 마라.** `AssetDatabase.SaveAssets()` 가 1×1 → 1024×1024 로
  부풀린다. 항상 `SaveAssetIfDirty(mat)` 를 쓴다

---

## 7. 이번에 배운 것 — 다음 사람이 같은 곳에서 안 넘어지게

- **`Material.EnableKeyword()` 만으로는 키워드가 안 붙는다.** `[Toggle(...)]` 이 붙은
  프로퍼티(예: `_BandSoftEnabled`)를 1 로 세워야 직렬화에서 살아남는다.
  키워드만 켜면 저장 뒤 `False` 로 돌아가고, 렌더가 **비트 단위로 동일**해서
  「셰이더 변형 컴파일 지연」으로 오진하기 딱 좋다
- **이름 규약으로만 거르면 하위 조각이 샌다.** `SM_Sym_` 로 필터링했더니
  `Blob_0/1/2` 가 빠져나가 월드 X 15.4 에 그대로 보이고 있었다. **좌표로도 걸러야 한다**
- **블렌더 브리지에는 소유권 장치가 없다.** Unity 씬은 `unity-scene-owner` 하나로
  직렬화하는 규칙이 있는데 `.blend` 에는 그 문장이 없다. 이번에 두 에이전트가 같은
  파일을 동시에 만져 값이 원인 불명으로 되돌아갔다 —
  실체는 상대식(`현재값 ÷ 부모스케일`)이 이미 고쳐진 값을 **한 번 더 나눈 이중 보정**이었다.
  방어는 **절대식**이다: `(목표 월드값 − parent.location) / parent.scale`.
  「현재 값이 원본이다」를 가정하지 않으면 몇 번을 돌려도 같은 값에 수렴한다
- **덮어쓰기 전에 결과물을 대조한다.** FBX 교체 전 `strings` 로 오브젝트 이름 집합을
  비교하고, `FBX_OVERRIDE_BASELINE.md` 에 오버라이드 수를 기록해 둔다

---

## 8. 참고 문서

| 문서 | 내용 |
|---|---|
| `BAKE_UV_EXPORT_FIX.md` | `AD48_EXPORT` 절차 · UV 조사 · 게이지 연장 |
| `BLENDER_CONCURRENCY_INCIDENT.md` | 동시 접근 사고 · 탐지 신호 · 절대식 방어 |
| `FBX_OVERRIDE_BASELINE.md` | 교체 전 오버라이드 기준선 (138 / 4 / 162) |
| `PLAN_BUILD_DEPENDENCY.md` | 패턴 기반 난이도 설계 §C |
| `OVERHARVEST_GATE_NOTES.md` | 과수확 열쇠 2갈래 |
| `PENDING_DECISIONS.md` | `P-20260809-01` 구슬 3안 |
| `TOPDOWN_PROGRESS.md` | 밤 전체 진행 기록 |

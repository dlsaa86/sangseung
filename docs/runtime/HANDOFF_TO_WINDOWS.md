# Mac → Windows 인계 (2026-08-09)

> ## 🔴 2차 인계 — 이 절이 최신이다 (2026-08-09 저녁)
>
> 아래 본문은 **오늘 낮 작업 기준**이고 그 뒤로 게임의 코어 루프가 바뀌었다.
> 먼저 이 절을 읽고, 본문은 블렌더 export 절차(§3)를 볼 때만 참고한다.
>
> **브랜치** `agent/autonomous-polish-20260809` · `main` 대비 **37 커밋 앞**
> **마지막 커밋** `1387d5e` 챔버 유리가 불투명했다 — URP 「Preserve Specular Lighting」
>
> ### 시작하는 법
>
> 1. 브랜치를 받고 Unity 6000.5.5f1 로 연다 (`Library/` 가 없어 첫 임포트가 오래 걸린다)
> 2. `Ascend/Run Self Tests` → **655 PASS / 3 FAIL** 이 나와야 한다.
>    실패 3건은 밤 시작 전부터 있던 것이다 —
>    적재 정책 4/6칸 · 다층 상승 0회 · 무계약 6층 1/6. 숫자가 다르면 환경 문제다
> 3. **`docs/runtime/ImplementationPlan.md` 를 읽는다.** 다음 작업이 거기 순서대로 있다
>
> ### 오늘 바뀐 것 (요약)
>
> | | |
> |---|---|
> | 전력 게이지판 | 퍼센트 막대 → **LED 디스플레이**. `Fill`·`Labels` 제거, `Screen`·`Glass`·`Coin` 추가 |
> | 코어 루프 | 층이 진행 단위 → **자유 변수**. 라운드마다 목표 층, 5스핀 안에 도달 못 하면 추락 |
> | 새 규칙 모듈 | `ElevatorTravel`·`RoundGoal`·`RoundSession` — **28 PASS**, 이미 잠김 |
> | 과수확 | **보류**(삭제 아님). `PrototypeFeatures.Overharvest = false` 한 줄로 되살린다 |
> | 기계의 동그란 빛 | 계단 스페큘러로 확정, 평면 판재 7장 제외 |
> | 챔버 유리 | 불투명했다. `_BlendModePreserveSpecular = 0` 으로 해결 |
>
> ### ⚠ 아직 배선되지 않았다
>
> `RunSession` 은 **여전히 옛 모델(층마다 요구 전력)로 돈다.** 새 규칙은 만들어져
> 잠겨 있지만 게임에 연결되지 않았다. 지금 게임을 플레이하면 옛 규칙이 돈다.
>
> 새 규칙을 만져 보려면 씬의 **`GB_AscendControls`** (패널 왼쪽 벽, X −1.34)를 쓴다 —
> 자기 라운드를 스스로 소유하는 **임시 샌드박스**다. 버튼 7개(+3/+1/스핀/−1/−3,
> 종료·재시작)와 버튼 위 요구 전력 판독부가 있다.
> **`RoundSandbox` 를 지우는 것이 이주 완료의 신호다.**
>
> ### 윈도우에서 주의
>
> - `Captures/baseline.txt` 는 **기기 종속**이다. 윈도우에서 새로 세워야 한다
> - `.blend` 는 iCloud 에 있고 윈도우에서도 열린다 (경로는 본문 §2)
> - **폰트 아틀라스 2장을 커밋에 섞지 마라** — 지금도 미커밋으로 남겨 뒀다
> - `Logs/Editor.log` 는 프로젝트 상대 경로다
>
> ---
>
> ## 이하 본문 — 2026-08-09 낮 기준 (블렌더 export 절차는 여전히 유효)

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

## 2. FBX 는 **들어갔다** — 커밋 `603b5cc`

굽기 UV·라이트를 보존한 FBX 가 `Assets/` 에 들어가 커밋됐다.
**윈도우에서 블렌더를 다시 열 필요가 없다.** 임포트 후 실측:

```
평균 밝기        0.1029    (정상 0.1028 · 깨진 것 0.0411)
켜진 라이트         2개    LT_CabBulb(중앙) · LT_SoulSpill(기계)
추가 GameObject     4개    AD47_PowerBarPivot · ShellCollision · BoardCells · FX_MachineAnchor
SpinGauge     Y[1.7150, 1.8100]  레버 손잡이 최고 1.6958  간격 19.2mm
게이지 100%   world X[-0.7537, 0.2821]  눈금 0.282130 대비 오차 0.1mm
자체 검증     626 PASS / 3 FAIL
```

`_barWidth` 도 1.72 → **1.0** 으로 내렸다(코드 기본값 + 씬 인스턴스 양쪽).
모델이 0~100% 를 온전히 덮으므로 보정이 필요 없어졌다.

### 그래도 이 절이 필요한 이유 — 다음에 블렌더를 다시 열 때

`.blend` 는 iCloud 에 있고 윈도우에서도 열린다.

```
mac      ~/Library/Mobile Documents/com~apple~CloudDocs/02_Resources/ElevPanel_v10/SM_ElevCab_Panel_AD57.blend
windows  iCloudDrive\02_Resources\ElevPanel_v10\SM_ElevCab_Panel_AD57.blend
```

UV 수정 · 게이지 연장 · SpinGauge 재배치 · 구슬 3종이 전부 저장돼 있다(23,189,186 bytes).
⚠ 파일명이 `AD57` 인데 Unity FBX 는 `AD47` 이다 — **확인했고 같은 원본이 맞다.**

**§3 은 그때 반드시 다시 읽어라.** 이 절차를 몰라서 두 번 실패했다.

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

### ⚠ 맥 블렌더 5.2.0 LTS 에는 라이트 포함 FBX 를 **재import 할 때** 터지는 버그가 있다

```
io_scene_fbx/import_fbx.py:2255
AttributeError: 'CyclesLightSettings' object has no attribute 'cast_shadow'
```

`hasattr(lamp, "cycles")` 로 `.cycles` 존재만 확인하고 그 안의 `.cast_shadow` 는
확인 없이 쓴다. 이 빌드의 Cycles 애드온에 그 속성이 없어서 **import 가 통째로 취소**된다.

**export 에는 영향이 없다** — 이 코드를 안 거친다. 검증하려고 재import 할 때만 걸린다.
윈도우 블렌더 버전이 다르면 안 날 수도 있다. 나면 검증 스크립트 안에서만
`blen_read_light` 를 인메모리 패치해 우회한다(디스크의 블렌더 설치는 건드리지 않는다).

### export 후 검증 — **이름·개수가 아니라 값을 봐야 한다**

```bash
strings <FBX> | grep -c '^Light$'            #  2
strings <FBX> | grep -c 'SOCKET_ElevPanel'   #  2
strings <FBX> | grep -c 'UVBake'             # 64   ← 2026-08-09 갱신 (이전 57)
strings <FBX> | grep -c 'SM_SpinGauge'       #  6
```

> ⚠ **`UVBake` 기대값이 57 → 64 로 바뀌었다.** 회귀가 아니라 `AD59_LEDPANEL` 의 결과다.
> 숫자가 안 맞으면 여기부터 대조하라 — 증감이 전부 설명된다.
>
> ```
> 57  이전 기준선
> +3  SM_Gauge_Screen · SM_Gauge_Glass · SM_Coin            (신규)
> +6  SM_SpinGauge_Housing + Cell_0..4                       (재건하며 UV 추가)
> -2  SM_Gauge_Fill · SM_Gauge_Labels                        (퍼센트 게이지 제거)
> ─────
> 64
> ```
>
> `UVBake` 가 **없는** 메시는 6개다 — `Blob_0/1/2` · `SM_Sym_{Absorber,NormalSoul,Proliferator}`.
> 이들은 슬롯0 이 교체되지 않고 그대로 나가므로 **슬롯0 자체가 0~1 이어야 한다**(확인함).

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

## 4. 다음에 손댈 때의 시작점

바로 이어서 할 만한 것을 우선순위대로 적는다.

1. **플레이 모드 육안 확인.** 이번 밤에 못 한 유일한 검증이다. 확인할 것 넷 —
   레버에 마우스를 올렸을 때 **T자 외곽선**, 스핀 중 **구슬이 팝인/팝아웃하지 않는지**,
   레버 위 **스핀 횟수 표시기**, 전력이 찰 때 **게이지가 100% 눈금까지** 가는지
2. **`_meshOutline`** 은 현재 `ExecutionLever` 에만 켜져 있다. 기계(부품 24개)는
   상자를 유지한다 — 메시로 따면 판재 이음매마다 선이 샌다
3. **남은 테스트 3** — 전부 밤 시작 전부터 있던 것이고 새 회귀가 아니다

### 게이지 관련해서 알아 둘 것

`_barWidth` 가 **1.0** 이라는 것은 「모델과 코드가 같은 말을 한다」는 뜻이다.
다시 1 이 아닌 값이 필요해졌다면 그건 스케일 문제가 아니라 **모델이 다시 어긋났다는
신호**이므로, 그 값을 만지기 전에 블렌더 쪽 폭부터 재라.

⚠ **씬에 직렬화된 값이 코드 기본값을 이긴다.** `[SerializeField]` 기본값을 바꿔도
이미 씬에 놓인 컴포넌트는 안 바뀐다. 이번에도 코드와 씬 인스턴스를 **둘 다** 고쳤다.

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

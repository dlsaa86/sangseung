# Bake UV export 순서 사고 — 원인·수정·재검증

> ## ⚠ 다음 사람이 가장 먼저 읽어야 하는 문장
>
> **이 `.blend`의 export 인자는 `tools/blender/build_cabin.py`가 아니라, `.blend`
> 안에 저장된 텍스트 블록 `AD48_EXPORT`에서 가져온다.** `bpy.data.texts["AD48_EXPORT"]`
> 를 열어 그 스크립트를 그대로 따르지 않으면(특히 "슬롯0을 굽기 UV로 잠깐
> 바꿔치기했다가 반드시 되돌린다"는 부분), 이름·개수 검증은 다 통과하는데 **캐빈이
> 2.5배 어두워지는** 사고가 그대로 재현된다 — 이번에 실제로 그랬다. 원인은 처음에
> `build_cabin.py`의 `export()` 인자를 쓰라고 지시했던 것이었다(팀리드 판단, 이번
> 사고의 실제 근본 원인). `object_types`도 `AD48_EXPORT` 원문(`{'MESH','EMPTY','LIGHT'}`)
> 을 기준으로 삼는다 — 다른 문서·지시가 다르게 말해도 이 텍스트 블록이 우선한다.

**작업 일시**: 2026-08-09 · **담당**: implementer (`tools/blender_bridge.py`)
**대상 파일**: `SM_ElevCab_Panel_AD57.blend` (동일 파일 — `SM_Cab_*` 캐빈 셸이 여기 같이 들어있다)
**계기**: 이전 세션의 `ELV_Cabin_AD47_new.fbx`(구슬+SpinGauge 수정)를 Unity에 넣었더니
캐빈이 2.5배 어두워짐(0.1028→0.0411). team-lead가 Unity를 커밋 `01ae239`로 되돌렸고,
UV0(TEXCOORD0)이 굽기(0~1) 대신 저작(월드 스케일) UV로 나갔다고 진단했다.

---

## 0. 요약

| 항목 | 결과 |
|---|---|
| 원인 | 위 상자 참조 — `build_cabin.py` 인자로는 `AD48_EXPORT`의 UV 바꿔치기 절차를
  건너뛴다 |
| 수정 | `AD48_EXPORT` 절차를 그대로 재현 — 슬롯0 **좌표값만** 굽기 UV로 임시 복사 →
  export → 슬롯0 좌표값을 저장해 둔 원본으로 **정확히 복원**. 레이어를 지우거나
  이름을 바꾸거나 새로 만들지 않았다 |
| 검증 | export한 FBX를 **새 빈 씬에 재import**해서 슬롯0 범위가 0~1인지 직접 확인 —
  4개 메시 전부 통과, `SM_Gauge_Fill`·Light 2개도 재import로 값까지 확인 (§3·§6.4·§7) |
| 산출물 | `<scratchpad>/ELV_Cabin_AD47_fixed.fbx` (**4,196,428 bytes**, `LIGHT` 포함
  최종본) |
| `.blend` 저장 여부 | `SM_Gauge_Fill` 연장(§6, 사용자 지시)이 영구 변경이라 **1회
  저장했다.** 그 뒤 `LIGHT` 포함해 재export할 때는 게이지 정점을 다시 안 건드렸으므로
  추가 저장 없음(§7) |
| `SM_Gauge_Fill` | 0%~61.7%에서 **0%~100%로 연장** — 정점 이동만, 오차 0.0mm (§6) |
| `object_types` | **해소됨** — `{'MESH','EMPTY','LIGHT'}`로 재export, `strings`
  Light 토큰 2개 = 기준선과 일치, 재import로 `LT_CabBulb`/`LT_SoulSpill` 값까지
  확인 (§7) |

---

## 1. `uv_layers` 전수 조사 — 수정 전 (원본 상태)

지시받은 4개 메시 전부 **UV 레이어 4개**를 가진다: `UVMap`(저작 언랩) · `UVBox`(월드
미터 박스 투영) · `UVUnique`(라이트맵용 고유 언랩으로 추정, 이번 조사에서 값만
확인하고 용도는 추적하지 않음) · `UVBake`(굽기, 0~1 팩). **`active_render`는 조사한
모든 메시에서 예외 없이 인덱스 0**(리스트 순서상 첫 레이어)에 있었다 — 즉
`active_render` 자체가 옮겨간 게 아니라, **인덱스 0에 원래부터 저작 UV가 있었다.**

| 오브젝트 | 인덱스0 (export 슬롯0) | 인덱스1 | 인덱스2 | 인덱스3 |
|---|---|---|---|---|
| `SM_Cab_Ceiling` | **UVMap** `[-1.078,1.078]×[-1.078,1.233]` `active_render=True` | UVBox `[-2.7105,2.7105]×[-2.7105,3.1]` | UVUnique 〃 | UVBake `[0.003,0.526]×[0.003,0.997]` `active(편집)=True` |
| `SM_Cab_FloorTrim` | **UVMap** `[-2.616,2.616]×[-2.616,2.616]` `active_render=True` | UVBox 〃 | UVUnique 〃 | UVBake `[0.005,0.667]×[0.005,0.995]` |
| `SM_Cab_Wall_Back` | **UVBox** `[-2.7105,2.7105]×[0,3.0]` `active_render=True` | UVUnique 〃 | UVBake `[0.005,0.751]×[0.005,0.995]` | UVMap `[-1.193,1.193]×[0,1.32]` |
| `SM_Cab_Wall_Left` | **UVBox** `[-2.7105,2.6105]×[-2.6105,3.0]` `active_render=True` | UVUnique 〃 | UVBake `[0.003,0.762]×[0.003,0.997]` | UVMap `[-1.193,1.149]×[-1.149,1.32]` |

**메시마다 인덱스0에 오는 레이어 이름이 다르다**(Ceiling/FloorTrim은 `UVMap`,
Wall_Back/Left는 `UVBox`) — 어느 쪽이든 **굽기가 아니다**라는 점이 공통이다. 팀리드가
제시한 (가)"순서가 바뀌었다"·(나)"`active_render`가 옮겨갔다" 중 **정확히는 둘 다
아니다** — 순서가 "바뀐" 게 아니라 **애초에 굽기가 첫 자리였던 적이 없다**. 이
파일에서 인덱스0을 굽기로 만드는 것은 **export 시점에만 임시로 하는 작업**이고,
그 절차가 `AD48_EXPORT` 텍스트로 이미 있었다(§2).

**전체 스윕(더 넓게, 이름·순서·`active_render`만)**: 캐빈 셸·챔버·게이지·레버·
장식물 등 **UV 레이어가 있는 메시 69개 중 57개가 `UVBake` 레이어를 갖고 있었다.**
57개 전부 `active_render`가 인덱스0에 있었고, 인덱스0의 이름은 메시마다
`UVMap` 아니면 `UVBox`였다(둘 다 저작 데이터, 굽기 아님) — **예외 없이 일관된
패턴**이었다. `SM_SpinGauge_*`·`SM_Sym_*`·`Blob_*`(이번 세션이 새로 만든 오브젝트)
12개는 `UVBake` 레이어 자체가 없다 — 아직 굽지 않은 신규 메시라 당연하다.

---

## 2. 무엇을, 왜 바꿨나

### 2.1 발견 — `.blend` 안에 이미 절차가 있었다

`bpy.data.texts`를 조회해 export 전용 텍스트 블록 `AD48_EXPORT`(2026-08-08 작성)를
찾았다. 그 안에 **정확히 이 문제를 다루는 주석**이 있었다:

> "⚠ 이 파일의 UV 레이어 0 은 `UVBox` 이고 월드 미터 박스 투영이다... 유니티의
> `Ascend/Stylized` 는 TEXCOORD0 만 읽으므로 FBX 의 첫 레이어가 굽기 UV(`UVBake`)여야
> 한다. 그래서 내보낼 때만 잠깐 바꿔치기하고 되돌린다."

이 텍스트는 `git log`의 커밋 `207a80e`("굽기 UV 를 만들면서 사용자의 저작 UV 를
덮어썼다")가 낳은 재발 방지 장치였다 — 그 사고에서 굽기 UV를 슬롯0에 **영구
복사**해 저작 UV(`UVMap`)를 잃어버렸었고(부분적으로만 복구 가능했다), 그 교훈으로
"임시로 바꾸고 반드시 되돌리는" 스크립트를 `.blend` 안에 박아 둔 것이었다.

**나는 이 텍스트 블록의 존재를 몰랐다** — 이전 세션에서 `build_cabin.py`의
`export()` 함수 인자를 그대로 복사해 썼는데, 그 함수는 이 UV 바꿔치기 절차를
포함하지 않는(또는 모르는) 별도 경로였다. 즉 "검증(이름·개수 존재)은 통과했지만
값이 틀렸다"는 이번 사고의 근본 원인은 **내가 이 파일 고유의 export 전제조건을
몰랐던 것**이지 무작위 버그가 아니었다.

### 2.2 적용한 수정 — `AD48_EXPORT`의 패턴을 그대로 재현

레이어를 **재정렬하거나 삭제·생성하지 않았다.** 대신 원본과 동일하게:

```python
for o in meshes:                       # UV가 있는 모든 메시 (57개, UVBake 보유분만)
    bake = me.uv_layers.get("UVBake")
    first = me.uv_layers[0]            # 이름은 메시마다 다름(UVMap 또는 UVBox)
    saved[o.name] = [tuple(d.uv) for d in first.data]   # ① 원래 좌표를 통째로 보관
    for i in range(len(bake.data)):
        first.data[i].uv = bake.data[i].uv              # ② 슬롯0의 좌표만 굽기 값으로 임시 교체
    first.active_render = True                          # (이미 True였다 — 멱등)

# ... export ...

for o in meshes:
    first = o.data.uv_layers[0]
    for i, uv in enumerate(saved[o.name]):
        first.data[i].uv = uv                            # ③ 저장해 둔 원래 좌표로 정확히 복원
```

**레이어의 이름·개수·존재는 처음부터 끝까지 그대로다.** 바뀐 것은 슬롯0 레이어
객체가 담고 있는 **정점별 UV 좌표값**뿐이고, 그마저도 export 직후 **저장해 둔 원본
값으로 loop 하나하나 정확히 되돌렸다.**

### 2.3 되돌림 검증 — 복원값이 최초 조사값과 정확히 일치

| 오브젝트 | 스왑 직후 (export에 실제로 나간 값) | 복원 직후 | §1의 최초 조사값과 일치? |
|---|---|---|---|
| `SM_Cab_Ceiling` | `UVMap` `[0.00252,0.52581]×[0.00252,0.99748]` | `UVMap` `[-1.07797,1.07797]×[-1.07797,1.23287]` | ✅ 일치 |
| `SM_Cab_FloorTrim` | `UVMap` `[0.00545,0.66664]×[0.00545,0.99455]` | `UVMap` `[-2.616,2.616]×[-2.616,2.616]` | ✅ 일치 |
| `SM_Cab_Wall_Back` | `UVBox` `[0.00516,0.75121]×[0.00516,0.99484]` | `UVBox` `[-2.7105,2.7105]×[0.0,3.0]` | ✅ 일치 |
| `SM_Cab_Wall_Left` | `UVBox` `[0.00251,0.76205]×[0.00251,0.99749]` | `UVBox` `[-2.7105,2.6105]×[-2.6105,3.0]` | ✅ 일치 |

57개 메시 전부 같은 절차로 스왑·복원했고, 이 4개는 표본 확인용으로 매번 값을 찍었다.
**`.blend`는 저장하지 않았다** — 인메모리 상태가 원본과 다시 같아졌으니 저장할
새 내용이 없다(단, `bpy.data.is_dirty`는 `True`다 — 편집 이력 자체는 있었으므로.
값이 같아도 dirty 플래그는 그대로다. 저장하지 않았으니 디스크의 `.blend`는 이번
세션 내내 **전혀 변경되지 않았다.**)

---

## 3. export 후 재import 검증 — 팀리드가 "이 작업의 핵심"이라 못박은 부분

**방법**: `<scratchpad>/ELV_Cabin_AD47_fixed.fbx`를 저장 중인 `.blend`가 아니라
**새로 만든 격리된 씬**(`bpy.data.scenes.new()`)에 재import해서 슬롯0 좌표 범위를
다시 쟀다 — 이름만 보거나 채널 존재만 확인하지 않았다. 확인 후 임포트된 오브젝트와
씬을 전부 지워 작업 씬을 오염시키지 않았다.

| 재import된 오브젝트 (이름 충돌로 `.001` 접미사 붙음) | 슬롯0 레이어명 | 슬롯0 U 범위 | 슬롯0 V 범위 | 0~1 팩(굽기)인가 |
|---|---|---|---|---|
| `SM_Cab_Ceiling.001` | UVMap | `[0.00252, 0.52581]` | `[0.00252, 0.99748]` | ✅ **예** |
| `SM_Cab_FloorTrim.001` | UVMap | `[0.00545, 0.66664]` | `[0.00545, 0.99455]` | ✅ **예** |
| `SM_Cab_Wall_Back.001` | UVBox | `[0.00516, 0.75121]` | `[0.00516, 0.99484]` | ✅ **예** |
| `SM_Cab_Wall_Left.001` | UVBox | `[0.00251, 0.76205]` | `[0.00251, 0.99749]` | ✅ **예** |

4개 전부 **export 직전 스왑값과 소수점까지 정확히 일치**한다 — Blender→FBX→Blender
왕복을 거쳐도 좌표가 그대로 보존됐다는 뜻이다. **슬롯0이 이제 굽기 UV다.**

**부가 확인**:
- `SOCKET_ElevPanel` 문자열 카운트 2, `SM_SpinGauge` 6, `SM_Sym_` 3 — 이전 세션의
  구슬·SpinGauge export 검증과 동일하게 통과(이번 export도 같은 검증을 반복 실행함).
- 새 FBX의 `UVBake` 문자열 카운트 = **57** — 현재 `Assets/`에 살아 있는(커밋
  `01ae239`, 정상으로 확인된) `ELV_Cabin_AD47.fbx`와 **정확히 같은 수**다. 레이어
  구성 자체가 정상 기준선과 일치한다는 교차검증이다.

---

## 4. 못 한 것 · 확신 없는 것 — 숨기지 않는다

### 4.1 ~~`object_types`에 `LIGHT`를 뺐다~~ → **해소됨 (§7)**

처음엔 지시서가 명시한 `{'MESH','EMPTY'}`를 그대로 따라 `LIGHT`를 뺐었다. team-lead가
"`AD48_EXPORT` 원문대로 `LIGHT` 포함, 기준선과 다른 걸 남기지 마라"고 정정해 재export
했다 — 상세·최종 검증 숫자는 §7. (참고: team-lead가 처음 잰 기준선 Light 토큰 수는
"5개"였다가 재측정으로 "2개"로 정정됐다 — 실제 씬의 LIGHT 타입 오브젝트도 정확히
2개(`LT_CabBulb`, `LT_SoulSpill`)였다.)

### 4.2 `path_mode='STRIP'`(지시서·이전 export와 동일) vs `AD48_EXPORT`의 `'COPY'`

`AD48_EXPORT`는 텍스처를 FBX 옆에 복사해 담는 `path_mode='COPY'`를 쓴다(원본이
윈도우 경로 `B:\...\Assets\...`로 직접 내보내는 절차라 그럴 만하다). 나는 지시서가
이전에도 명시했던 `'STRIP'`을 유지했다 — 스크래치패드로 나가는 검증용 파일이라
텍스처 파일을 끌고 오는 게 무의미하고, UV 좌표 자체와는 무관한 설정이다. Unity로
실제 반영할 때 텍스처 링크 방식은 team-lead가 판단할 부분이다.

### 4.3 문짝을 닫힘 자세로 두지 않았다 — 정보만 확인, 손대지 않음

`AD48_EXPORT`는 export 전에 `AD43_DOOR2PANEL`을 실행해 문짝을 닫힘 위치로 옮긴다.
이번 지시에는 그 내용이 없어 **문짝은 건드리지 않았다** — 현재 상태만 확인:

| 문짝 | 현재 Y | 닫힘 목표 Y | 이미 닫혀 있는가 |
|---|---|---|---|
| `SM_Door_L` | −0.502 | −0.502 | ✅ 이미 닫힘 |
| `SM_Door_R` | 0.502 | 0.502 | ✅ 이미 닫힘 |

**둘 다 이미 닫힘 목표값과 정확히 일치한다** — `AD43_DOOR2PANEL`을 실행할 필요
자체가 없었다(달라서 안 옮긴 게 아니라, 옮길 게 없었다). 이번 export도 문짝
자세는 정상 기준선과 같다.

### 4.4 Unity 쪽 밝기 재확인은 하지 못했다

이 세션은 Unity MCP가 금지돼 있다. §3의 재import 검증(슬롯0 = 0~1 범위, 정상
기준선과 `UVBake` 카운트 일치)까지가 이 세션에서 확인할 수 있는 최대치다.
**실제로 2.5배 어두워짐이 해소되는지는 team-lead가 Unity에 넣어 확인해야 한다.**

### 4.5 다른 UV 레이어(`UVUnique`)의 용도는 추적하지 않았다

이름으로 미루어 라이트맵용 고유 UV로 추정하나 확인하지 않았다. 이번 버그와
무관해 보여 조사하지 않았다 — 추정이라는 점만 기록한다.

---

## 6. `SM_Gauge_Fill` 0%~100% 연장 (사용자 지시, 같은 세션에 추가됨)

전력 게이지 Fill 메시가 0%~61.7%(사용자 체감 "67%쯤")까지만 모델링돼 있어 Unity가
`localScale.x = _barWidth(1.72) × 비율`로 보정하던 문제. **정점만 이동**해 0%~100%
전체로 늘렸다 — 원점·스케일·Y/Z는 전혀 건드리지 않았다.

### 6.1 100% 눈금 X — 다시 재서 확인 (지시대로 문서값을 믿지 않음)

`SM_Gauge_Labels`를 연결성 기준으로 재클러스터링(그루브 수정 세션과 **동일한
알고리즘** — 인접 컴포넌트를 X 중심 간격 0.06 미만이면 같은 숫자로 묶음)한 결과:

| 눈금 | 재측정 X 중심 | 그루브(내가 이미 고친 홈)와 교차검증 |
|---|---|---|
| 0% | **−0.753628** | 홈 좌측 끝 −0.753628 — 정확히 일치 |
| 100% | **0.282130** | 홈 우측 끝 0.282130 — 정확히 일치 |

지시서가 "0.2899는 부분 샘플링이었다"고 경고한 것과 달리, **다시 재도 0.282130이
그대로 나왔다** — 그루브 수정 세션 이후 라벨 위치도 홈 위치도 변한 게 없었다.
팀리드가 우려한 "그 사이 홈을 고쳤으니 기준이 달라졌을 수 있다"는 실제로는
발생하지 않았다(홈은 라벨을 기준으로 맞춘 것이지 그 반대가 아니라서 당연하지만,
지시대로 가정하지 않고 실측으로 확인했다).

### 6.2 `SM_Gauge_Fill` 월드 X 범위 — 전/후

| | X min (0%) | X max | 월드 폭 |
|---|---|---|---|
| 수정 전 | −0.753628 (이미 0% 눈금과 일치) | −0.114234 | 0.639394 (= 61.74%) |
| 목표 | −0.753628 | 0.282130 | 1.035758 (100%) |
| **수정 후** | **−0.753628** | **0.282130** | **1.035758** |
| 재import 검증 (§6.4) | −0.753628 (오차 0.0mm) | 0.282130 (오차 0.0mm) | — |

0% 쪽(X min)은 손대기 전부터 이미 0% 눈금과 정확히 일치했다 — 그래서 이 끝은 아예
건드리지 않았다(§6.3).

### 6.3 늘린 방식 — 정점 이동, 원점·스케일·Y·Z 전부 불변

`SM_Gauge_Fill`은 8정점 박스다. 정점을 로컬 X 값으로 나눠 보니 **정확히 두 그룹**
(각 4개)으로 갈렸다 — 로컬 X=0(월드 X가 이미 0% 눈금과 일치, "근단")과 로컬
X=0.464256(월드 X −0.114234, "원단"). **원단 4개의 로컬 X만** 새 값 0.752051로
옮겼다 — `(TARGET_100% − parent.location.x) / parent.scale − obj.location.x`로 계산한
값이고, **오브젝트의 `location`/`scale`/`rotation`은 코드에서 아예 참조만 하고 대입한
적이 없다**(스크립트에 `fill.location = ...`나 `fill.scale = ...` 줄 자체가 없다).

- 근단 4개 정점: 좌표 완전 불변 확인(`near_end_untouched: true`)
- 8정점 전부 로컬 Y·Z: 완전 불변 확인(`all_verts_yz_unchanged: true`) — 편집 전/후
  각 정점의 Y·Z를 개별 대조했다(bbox만 보지 않았다)
- 오브젝트 `scale`: `(1.0, 1.0, 1.0)` 그대로(`object_scale_unchanged: true`)
- 오브젝트 `location`: 코드로 확인(스크립트가 안 건드림) + 직접 재조회로 float32
  정밀도 이슈가 아님을 재확인 — 완전 불변
- 정점 개수: 8개 그대로(위상 변경 없음, 이동만)

### 6.4 export 후 재import 검증

UV 스왑 절차(§2)와 **같은 export 한 번**에 실려 나갔다. 새 격리 씬에 재import해서
`SM_Gauge_Fill.001`을 다시 쟀다:

```
world_bbox X = [-0.753628, 0.28213]
목표          [-0.753628, 0.282130]
오차          0.0mm / 0.0mm
Y = [2.873251, 2.917874]   Z = [0.798538, 0.848119]   (수정 전과 완전 동일)
```

**목표 오차 1mm 이내를 요구받았는데 0.0mm — 정확히 일치.**

---

## 7. `LIGHT` 포함 재export — 최종본

§4.1에서 미뤘던 것. team-lead가 "`AD48_EXPORT` 원문대로, 기준선과 다른 걸 남기지
마라"고 정정해 `object_types={'MESH','EMPTY','LIGHT'}`로 다시 export했다. 게이지
정점(§6)은 이미 저장돼 있어 다시 옮기지 않았고, UV 스왑→export→복원만 반복했다
(그 사이 `.blend`가 그대로였다는 것도 재확인: `is_dirty=False`, 파일 크기·경로 불변).

### 7.1 최종 파일

`<scratchpad>/ELV_Cabin_AD47_fixed.fbx` — **4,196,428 bytes** (LIGHT 제외판 4,193,676
대비 +2,752 bytes, 라이트 2개분과 대략 일치하는 크기 증가).

### 7.2 `strings` 검증 — 넷 다 숫자로

| 문자열 | 카운트 | 기준선(정상 `Assets/` FBX, 커밋 `01ae239`) | 일치 |
|---|---|---|---|
| `UVBake` | 57 | 57 | ✅ |
| `SOCKET_ElevPanel` | 2 | 2 | ✅ |
| `Light`(정확 토큰) | **2** | 2(team-lead 재측정값) | ✅ |
| `SM_SpinGauge` | 6 | — (기준선엔 없음, 이번 작업 자체가 추가한 것) | ✅ |
| (덧붙임) `LT_CabBulb` / `LT_SoulSpill` | 2 / 2 | — | ✅ 이름까지 확인 |

### 7.3 ⚠ 재import 검증 중 발견한 것 — 이 Blender 빌드의 FBX 임포터 자체 버그

`LIGHT`가 포함된 FBX를 재import하면 **블렌더 자신의 번들 애드온이 예외를 던지며
전체 import가 취소된다**(오브젝트 0개 생성):

```
File ".../io_scene_fbx/import_fbx.py", line 2255, in blen_read_light
    lamp.cycles.cast_shadow = lamp.use_shadow
AttributeError: 'CyclesLightSettings' object has no attribute 'cast_shadow'
```

원문(`import_fbx.py:2254`)이 `if hasattr(lamp, "cycles"):`로 `.cycles` **속성 자체의
존재**만 확인하고 그 안의 `.cast_shadow` **하위 속성**은 확인 없이 바로 대입한다 —
이 Blender 5.2.0 LTS 빌드에 딸려 온 Cycles 애드온의 `CyclesLightSettings`에는 그
하위 속성이 없다. **내 export 코드와 무관한, 이 블렌더 설치본 자체의 번들 애드온
버전 불일치 버그다** — `AD48_EXPORT`가 만드는 실제 정상 FBX를 이 블렌더로 재import
해도 같은 이유로 똑같이 깨질 것이다(재현 조건이 "라이트가 포함된 FBX를 이 블렌더로
재import"이지 내 스크립트의 특정 동작이 아니다).

**우회**: 검증 스크립트 안에서만, `io_scene_fbx.import_fbx.blen_read_light`를 원본과
동일하되 문제의 두 줄(`hasattr` 체크 + `cast_shadow` 대입)만 뺀 함수로 **메모리
상에서만** 바꿔치기했다 — 디스크의 블렌더 설치 파일은 전혀 건드리지 않았고, 검증이
끝난 뒤 원래 함수로 되돌렸다(`blen_read_light is _ORIGINAL_blen_read_light → True`로
확인). export 자체는 이 코드를 전혀 거치지 않는다 — **오직 "내가 만든 FBX를 다시
읽어서 확인하는" 이 검증 단계에서만 필요했다.**

이 우회로 재import가 성공했고, 라이트 두 개의 실제 값까지 확인했다:

| 오브젝트 | 타입 | Energy | Color |
|---|---|---|---|
| `LT_CabBulb` | POINT | 52.0 | (1.0, 0.82, 0.5) |
| `LT_SoulSpill` | POINT | 13.0 | (1.0, 0.42, 0.22) |

현재 씬의 라이브 오브젝트(`bpy.data.objects`)를 직접 조회한 값(energy 52 / 13)과
**정확히 일치** — export→FBX→import 왕복에서 라이트 데이터가 손실 없이 보존된다.

### 7.4 UV·게이지 재확인 (LIGHT 포함판에서도)

같은 재import에서 §3·§6.4와 동일한 항목을 다시 쟀다 — **전부 그대로**:

```
① UV 슬롯0: 4개 메시 전부 0~1 범위 (SM_Cab_Ceiling/FloorTrim/Wall_Back/Wall_Left)
② SM_Gauge_Fill 월드 X = [-0.753628, 0.28213]   오차 0.0mm / 0.0mm
```

`LIGHT`를 추가해도 `MESH`/`EMPTY` 쓰기 경로는 전혀 영향받지 않는다는 것을 실측으로
확인한 셈이다(애초에 `object_types`는 타입별 독립 필터라 서로 간섭할 이유가 없다).

---

## 8. 부록 — 스크립트 (감사용)

**UV**: `uv_a_investigate.py`(§1 전수 조사, `AD48_EXPORT`/`AD43_DOOR2PANEL` 텍스트
블록 원문 확인) · `uv_b_swap_export_revert.py`(§2 1차 스왑→export→복원 검증,
§4.3 문짝 상태 확인).

**게이지**: `gauge_a_investigate.py`(§6.1 라벨 재클러스터링·그루브 교차검증·
`SM_Gauge_Fill` 정점 전수 덤프) · `gauge_b_extend_fill.py`(§6.3 정점 이동) ·
`gauge_c_final_export_save.py`(UV 스왑 + export(LIGHT 제외판) + UV 복원 + **저장**,
이번 세션의 유일한 `wm.save_mainfile()` 호출) · `gauge_d_reimport_verify.py`
(§6.4 — UV 4종 + 게이지 재import 검증, LIGHT 제외판 기준).

**LIGHT 포함 재export(§7)**: `gauge_e_reexport_with_light.py`(UV 스왑 → **최종
export**(`object_types`에 `LIGHT` 추가) → UV 복원 — 저장은 다시 안 함, 게이지
정점이 이미 저장돼 있어 순변화 없음) · `gauge_g_final_verify_patched.py`
(§7.3~7.4 — `blen_read_light` 인메모리 패치로 재import 우회, UV·게이지·라이트
전부 재확인). `gauge_f_final_verify.py`는 패치 전 시도로, 블렌더 임포터 버그
트레이스백을 그대로 기록해 뒀다(§7.3의 증거).

**최종 산출물**: `<scratchpad>/ELV_Cabin_AD47_fixed.fbx`(**4,196,428 bytes**, LIGHT
포함) — UV 수정 + 게이지 연장 + LIGHT가 **전부 함께** 실려 있다. `.blend`도
저장됐다(23,189,186 bytes, 게이지 정점 연장분만 반영 — UV는 원래대로 복원되므로
저장 내용에 없다). 저장 전 백업:
`<scratchpad>/SM_ElevCab_Panel_AD57.pre_gaugefill_backup.blend`.

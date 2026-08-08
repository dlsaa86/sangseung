# 인계 — Windows → Mac (2026-08-08)

브랜치 `agent/phase2-full-prototype`. 이 문서만 읽으면 이어서 진행할 수 있게 쓴다.

---

## ✅ 2026-08-08 Mac 에서 실측 — 1번 걱정은 일어나지 않았다

**텍스처 16장이 전부 packed 였다. 깨진 것은 0장이다.**

```
python3 tools/blender_bridge.py -c "..."   ← 실측 방법은 그 파일 참조
DATA-MISSING: 0     16장 모두 has_data=True, 픽셀 수 정상
```

경로는 확실히 전부 죽었다 — `C:/Users/hufea/...` 는 Mac 에 없고, `T_Elev_Surf_A/B`·
`T_Floor_A`·`T_Iron_A` 넷은 아래 경고대로 세션 임시 폴더를 가리킨다. **그런데 이미지
데이터가 `.blend` 안에 박혀 있어서 렌더에는 아무 지장이 없다.** 누군가 저장 전에
Pack Resources 를 눌렀거나 자동 팩이 걸려 있었다.

⚠ **그래도 경로는 죽은 채다.** 지금 「Unpack」 하거나 「Reload」 를 누르면 그 순간
데이터가 사라진다. 외부 파일로 되돌리고 싶으면 먼저 **File → External Data →
Unpack Resources → Write files to current directory** 로 꺼낸 뒤 경로를 다시 잡는다.

아래 원문은 이력으로 남긴다.

---

## ⚠ 먼저 해야 할 것 — 안 하면 블렌더가 Mac 에서 깨진다

### 1. 텍스처 4장이 **세션 임시 폴더**를 가리키고 있다

`SM_ElevCab_Panel_AD57.blend` 안의 이미지 경로를 조사했을 때(2026-08-08) 아래 넷이
**다른 세션의 스크래치패드**를 가리키고 있었다. 그 경로는 Windows 에서도 언제든 지워지고,
**Mac 에는 아예 없다.**

```
T_Elev_Surf_A   C:\Users\hufea\AppData\Local\Temp\claude\...\scratchpad\tex_T_Elev_Surf_A.png
T_Elev_Surf_B   (같은 폴더)
T_Floor_A       (같은 폴더)   ← 바닥 타공 텍스처. 방금 1.5배 작업에 쓴 그것이다
T_Iron_A        (같은 폴더)
```

나머지(`T_Wall_*`·`T_Ceil_*`·`T_Floor_N/R`·`T_Iron_N/R`·`T_Elev_Edge`·`T_Elev_Macro`)는
iCloud 폴더나 `//엘리베이터\tex\` 상대경로라 Mac 에서도 따라온다.

**Windows 를 끄기 전에 반드시:**

```
Blender → File → External Data → Pack Resources → 저장
```

이러면 이미지가 `.blend` 안에 박혀 어디서 열어도 살아 있다. 파일이 수십 MB 커지지만
그게 정상이고, 이 파일은 git 에 안 올리므로 저장소에 영향이 없다.

⚠ 이 세션에서는 확인·수정을 못 했다 — 블렌더가 닫힌 뒤에 알아차렸다.
**Mac 에서 열었을 때 재질이 분홍/검정이면 이것이 원인이다.**

### 2. 블렌더 파일은 git 에 없다 — iCloud 로 간다

```
Windows  C:\Users\hufea\iCloudDrive\02_Resources\ElevPanel_v10\SM_ElevCab_Panel_AD57.blend
Mac      ~/Library/Mobile Documents/com~apple~CloudDocs/02_Resources/ElevPanel_v10/SM_ElevCab_Panel_AD57.blend
```

**의도적으로 git 에 넣지 않았다.** 23 MB 바이너리라 저장할 때마다 히스토리에 통째로
새 사본이 쌓이고(20 세션이면 ~460 MB), iCloud 가 이미 같은 일을 하고 있어 진실이 두 벌이 된다.
버전 스냅샷을 커밋에 묶고 싶어지면 그때 Git LFS 를 도입하는 게 맞다.

이전 버전 15 개는 같은 폴더 `_backup/` 에 있다. 저장소 안 `tools/blender/cabin.blend` 는
**옛 생성본이고 지금 쓰는 파일이 아니다.**

### 3. 스크립트 안의 Windows 절대경로

`.blend` 안 텍스트 블록 두 개가 하드코딩돼 있다. Mac 에서 그대로 돌리면 실패한다.

| 텍스트 블록 | 고칠 곳 |
|---|---|
| `AD48_EXPORT` | `DST = r"B:\PROJECT_NEW_BORN\...\ELV_Cabin_AD47.fbx"` |
| `AD53_BAKEALL` | `DST = r"B:\PROJECT_NEW_BORN\...\Textures\BakedAD47"` |

### 4. 캡처 베이스라인은 기기 종속이다

`Captures/baseline.txt` 의 `machineFingerprint` 가 이를 강제한다. Mac 에서는
**그 기기에서 베이스라인을 새로 세운다.** 이번 세션의 EEVEE 기준선 이미지는
세션 임시 폴더에 있어 남지 않는다 — 필요하면 Mac 에서 다시 렌더한다(포즈는 아래).

---

## 이번 세션에서 한 일

커밋 `e19b969` + 이 커밋. 상세는 `production/session-logs/20260808-unity-mood-converge-eevee.md`
와 `production/goals/unity-mood-converge-eevee-20260808.md` 에 있다.

### 모델링 (블렌더 `AD47` → `AD57`)

- 문틀·기계 프레임 모서리의 **동일 평면 제거** — Cycles 에서 검은 사각형이 나오던 원인
- 전등의 **두께 없는 부유 링** 삭제, 바닥 후프를 살대에 물리게 확대
- **벽 판재 4장 삭제** (벽이 단층 평면이 됨), 문 쪽 벽 3단 단차 제거 (66 → 18 면)
- 문짝 속 **파묻힌 박스 8개** 삭제 (55,900 발 광선으로 안 보임을 실측한 뒤)
- 문 두께 **28 → 60 mm**
- **8번 통관 리브 면 9장 복원** — 옆 칸(9번)의 구성을 복제. 칸 전체 면 개수 668 로 일치
- 바닥 타공 **1.5배** (`M_Cab_Floor` 의 `AD50_PUNCH` Mapping scale 3.0 → 2.0)

### 셰이더 `AscendStylized.shader` — 전부 기본값 불변

- `Quantize` 경계 클램프 `min(floor(v*steps), steps-1)`
- `_NearAttenClamp` 신설 (기본 1.0 → 비트 단위 동일)
- **`_ADDITIONAL_LIGHT_SHADOWS` / `_SHADOWS_SOFT` 키워드 + 그림자 샘플링 오버로드**
  — 이 셰이더에는 점광 그림자가 아예 구현돼 있지 않았다

### Unity

- 굽기 텍스처 57장 **DXT1 → BC7**, 해상도 상향(바닥 1024 → 2048)
- 문짝 위치 **510 mm** 오차 수정 (`DoorAxisAdapter._closedWorld`, SerializedObject 로 저장)
- 게이지바 **706 mm** 오차 수정
- 구슬 재질(`URP/Lit`) `_Smoothness` 0.72 → 0.30
- 무드를 EEVEE 기준선에 수렴 (수치 AC 14/14)

---

## ⚠ 이 세션에서 내가 낸 사고 두 건 — 같은 실수 반복 금지

**둘 다 「루트를 통째로 켜고 끈 것」이 원인이다.**

1. `ReferenceRoom`·`GrayboxWorld` 를 `SetActive(false)` 로 껐더니 **게임 로직 17개**가
   같이 죽었다 (`InteractableLever`·`MachineImpactView`·`AudioSource` 등).
   기계 주변 이펙트·애니메이션·사운드가 전부 사라졌다.
2. 되살리려고 루트를 켰더니 이번엔 **조명 7개**가 같이 켜졌다. 활성 조명이 6개가 되면서
   URP 의 `maxAdditionalLightsCount = 4` 를 넘겨 **오브젝트마다 다른 조명 조합**이 칠해졌고,
   그 경계에 검은 선이 생겼다. 기계도 `ExecutionLeverKeyLight` 때문에 밝아졌다.

**규칙: 이 씬에서 「안 보이게」는 `MeshRenderer.enabled = false` 로만 한다.**
게임오브젝트를 끄면 스크립트·오디오·조명이 함께 죽는다.

현재 상태 — 렌더러 514개 가림, 활성 조명 2개(`LT_CabBulb`·`LT_SoulSpill`).

---

## 측정 규약 (무드 회귀 확인용)

| 시점 | Unity 카메라 | 블렌더 카메라 |
|---|---|---|
| PANEL | `(0, 1.65, -2.30)` rot 0, FOV 62 | `(0, -2.30, 1.65)` +Y, 수직화각 62 |
| DOOR | `(2.00, 1.60, 0)` rot(0,-90,0), FOV 62 | `(0, 2.00, 1.60)` -X, 수직화각 62 |

지표는 sRGB 인코딩 후 휘도 분위수·채도·R/B 평균비. 허용오차와 최종 수치는 목표 파일에 있다.

⚠ **캡처는 반드시 두 번 렌더하고 두 번째를 채집한다.** 셰이더 변형이 비동기 컴파일되면
첫 프레임이 검게 나온다. `ShaderUtil.allowAsyncCompilation = false` 도 같이 건다.

⚠ **수치만 믿지 않는다.** 이 세션에서 AC 14/14 를 통과했는데 화면에는 거대한 검은 쐐기가
있었고, **그 쐐기가 p05 를 낮춰 통과에 기여했다.** 회차마다 그림도 같이 본다.

---

## 다음 세션 시작점 — 우선순위 순

1. **`GameSystems` 를 켤지 결정** — `RunController`·`FloorController`·`RouletteController`·
   `PassengerManager` 가 여기 붙어 있고 **세션 시작 전부터 꺼져 있었다**(내가 끈 게 아니다).
   게임 진행 자체가 안 된다면 이쪽이다. 의도적으로 꺼둔 것인지 확인이 필요해 손대지 않았다.
2. **계단식 빛** — `_Steps` 가 `Range(2, 8)` 에 이미 8(최댓값)이다. 더 부드럽게 하려면
   셰이더의 상한을 올려야 한다(예: 32). ⚠ `_BandFloor` 는 `(band + f)/(steps - 1 + f)` 로
   나뉘므로 **steps 를 올리면 바닥값 기여가 같이 줄어 방이 어두워진다.** 비례해서 올릴 것.
   (`_Steps` 8 로 올렸다가 방이 통째로 꺼진 적이 있다.)
3. **기계가 찰흙 같음** — `Ascend/Stylized` 에 **스페큘러 항이 하나도 없다**
   (`spec`·`halfDir`·`reflect`·GGX 전부 0 건). 금속이 빛날 방법이 없다. 하이라이트 항을
   추가해야 한다. 키워드로 감싸고 기본값 off 로 두면 「기본값 불변 규약」을 지킨다.
4. **유리** — `M_Elev_ChamberGlass` 의 `_Smoothness` 를 0.30 에서 되돌린다(0.55 정도).
   씬의 `reflectionIntensity = 0` 이라 환경 반사는 안 나오고 점광 하이라이트만 나온다.
5. **드리운 그림자** — 독립 평가의 1순위 지적이고 **미해결이다.** 런타임 점광 그림자는
   이 방에서 설정을 바꿀 때마다 아티팩트가 자리만 옮긴다(캐스터가 전구에서 몇 cm,
   URP 는 2048 아틀라스를 큐브 6면으로 나눔). **가장 싼 해법은 블렌더 굽기에
   direct+indirect 를 켜는 것** — `AD53_BAKEALL` 의 `use_pass_direct/indirect` 를 `True` 로.
6. **달리기·점프·앉기** — 미착수. 현재 `Player` 는 이동만 있다.

---

## 자체 검증 상태 — 커밋 게이트 우회 이유

```
Ascend/Run Self Tests : 607 PASS / 7 FAIL
세션 시작 기준선       : 607 PASS / 7 FAIL   ← 동일, 회귀 없음
```

실패 7건은 적재 정책·다층 상승·10층 완주·남은 스핀 정산으로 **전부 밸런스**이고 이번
렌더링·모델링 작업과 접점이 없다. `commit-gate.sh` 는 `fail=[1-9]` 를 무조건 막고
「회귀 없음」 개념이 없어, 이 상태로는 무관한 작업이 영구히 막힌다. 그래서 문서화된
`SKIP_SELFTEST_GATE=1` 을 쓰되 커밋 메시지에 남겼다.

**게이트를 「기준선 대비 회귀만 막는」 방식으로 고치는 것이 별개 작업으로 남아 있다.**

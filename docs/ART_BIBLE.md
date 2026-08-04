# ART_BIBLE — 아트 방향과 금지 요소

> **출처**: Notion MASTER PRD §12 / [에셋 부록] 06 3D 프롬프트 가이드 /
> [비주얼 부록] 07 영혼·저항체 디자인 체계 / 2026-07-28 3D 리소스 제작 타겟
> **작성**: 2026-08-04 Notion 전수 조사 기반
> **상위**: `docs/VISUAL_SPEC.md`(충돌 시 우선) · [PROJECT_VISION.md](PROJECT_VISION.md)
> **관련**: `docs/VISUAL_BIBLE.md`, `docs/SYMBOL_DESIGN_SPEC.md`, `docs/DEVICE_DESIGN_SPEC.md`

---

## 1. Style Lock (확정 — 모든 프롬프트에 동일 반복)

```
Established game art direction:
stylized low-poly industrial occult horror,
PS2-era survival-horror influence,
faceted but readable geometry,
dark rusted iron, aged brass and stained wood,
muted brown, charcoal black, dirty olive and desaturated bone colors,
handcrafted worn surfaces,
simple chunky silhouettes,
grim supernatural freight-elevator aesthetic,
not photorealistic and not pixel art.
```

MASTER PRD §12.1은 여기에 `dirty glass` 한 항목을 추가한다.
세계관 외형은 **낡은 1960년대 산업용 화물 엘리베이터**.

### 1.1 ⚠ 시대 레퍼런스 충돌

| 표기 | 출처 |
|---|---|
| `PS2-era survival horror influence` | 06 §3·§5·§6·§7, MASTER PRD §12.1 (다수) |
| `PS1 and early-PS2 survival-horror influence` | 06 §13 장치 전용 프롬프트 |
| "PS1~초기 PS2" | 프로젝트 내부 기준 (`CLAUDE.md`, `visual-director`) |

**폴리곤 예산 수치가 없어 실무 차이가 정의되지 않는다.** → [DECISION_LOG.md](DECISION_LOG.md) C-06.

### 1.2 ⚠ "픽셀 텍스처"는 Style Lock과 충돌한다

`not pixel art`가 **모든 프롬프트에 강제**된다. 로우폴리 + 마모 표면이지 픽셀아트가 아니다.
디더링·CRT·아날로그 화면 효과는 **Notion 전체에 규정이 없다** — 금지도 확정도 아닌 백지다.

## 2. 형태 언어 (확정)

| 항목 | 규정 |
|---|---|
| 실루엣 | `clear and readable silhouette`, `chunky readable silhouette` |
| 지오메트리 | `faceted but readable geometry`, `simple chunky silhouettes` |
| 비례 | `chunky but believable proportions` |
| 조준용 | 상호작용 오브젝트는 **단순하고 두꺼운 실루엣** |
| 캐릭터 | `simplified anatomy`, `moderate polygon density`, `faceted facial planes` |
| 기본 포즈 | **약한 A-pose** — 팔을 몸에서 약 **30도**, 손바닥 아래, 손가락 약간 벌림, 발은 어깨너비 |
| 공간 | 좁고 높고 박스형이며 **실제로 작동할 것 같은** 구조 |

**분리 제작 대상**: 긴 머리·망토·치마·날개·무기·백팩처럼 몸체 실루엣을 가리는 요소는 **별도 에셋**.

**핵심 요구**: 룰렛 기계·계약 패널·전력 계기판·과수확 레버가 **실루엣만으로 구분**되어야 한다.

## 3. 색상과 재질

### 3.1 팔레트 (확정 — 구체 색값은 미명시)

```
dark rusted iron · aged brass · stained wood · dirty glass
muted brown · charcoal black · dirty olive · desaturated bone
```

심볼도 이 팔레트(산화 철·낡은 황동·오염된 유리·탈색된 뼈색)와 조화를 유지한다.

**HEX/RGB 값은 세 문서 어디에도 없다.** 심볼의 최종 색·재질은 **승인 대기** — 잠그지 않는다.

### 3.2 재질·셰이더 (확정)

- URP용 **공통 스타일 셰이더 또는 Shader Graph**
- 저폴리 면 분할이 읽히는 **단순화된 조명 반응**
- 금속 glint는 **제한적으로**. 화면 전체가 반짝이지 않게
- 녹·기름때·마모는 **마스크와 버텍스 컬러**로 강도 조정 가능하게
- 오염 유리·통관은 **투명도·왜곡·때 마스크를 독립 조정**
- 정상 영혼·저항체는 **발광·맥동·내부 코어를 데이터로 조정**
- **Subsurface scattering은 전역 필수가 아니다** — 비교용 옵션 셰이더로만, 성능·스타일 평가 후 채택
- 고비용 셰이더 기능은 품질 프리셋으로 비활성화 가능해야 한다

**형태·재질 분리 원칙 (확정)**:
> 녹·얼룩·명암이 **실제 돌출 구조로 오인되는 문제**를 막기 위해 형태와 재질을 분리한다.

## 4. 텍스처·폴리곤·UV — 🔴 전부 미명시

| 항목 | 상태 |
|---|---|
| 텍스처 해상도 (px) | **수치 미명시** |
| 폴리곤·트라이앵글 예산 | **수치 미명시** (`low-poly friendly`, `moderate polygon density` 같은 정성 표현만) |
| UV 규칙·텍셀 밀도·아틀라스 레이아웃 | **규정 부재** |

관련 규정(수치 없음):
- 텍스처 임포트 크기·압축·밉맵·알파를 **에셋 카테고리별 Preset**으로 관리
- 반복 환경 재질은 **아틀라스 또는 공유 머티리얼** 우선
- **무압축 원본을 런타임에 직접 사용하지 않는다**
- 투명 재질은 통관·일부 영혼·제한된 VFX에만
- LOD는 승강기 외부 공간과 대형 캐릭터에 우선

> ⚠ **3D를 실제로 발주하려면 텍셀 밀도·트라이앵글 상한·UV 채널 규약이 지금 존재하지 않는다.**
> 이는 문서 공백이지 폐기가 아니다.

세 문서에 실제로 존재하는 치수는 **A-pose 30도**와 **문 밖 공간 깊이 3~5m** 둘뿐이다.

## 5. 조명 (확정)

### 5.1 인게임

| 항목 | 규정 |
|---|---|
| 평상시 | **탁한 따뜻한 천장등** vs **차가운 통관 발광**의 대비 |
| 위험 단계 | 붉은 회전등과 불규칙 암전이 공간의 구조를 다시 드러냄 |
| 대기광 | 형태를 숨기지 않을 정도로 유지 |
| 안개·먼지·빛줄기 | 분위기를 만들되 **결과판과 상호작용물을 가리지 않음** |
| 실시간 그림자 | 핵심 조명에 한정, 품질 프리셋으로 제어 |
| 캐스케이드 | 단계가 높아질수록 통관 조명·전력 탱크·사운드 층을 **누적** |

**🔑 발견 가능성 규칙 (확정)**
> 모든 핵심 상호작용물은 어두운 상태에서도
> **실루엣 · 국부 조명 · 하이라이트 중 둘 이상**으로 발견 가능해야 한다.

### 5.2 레퍼런스 이미지 촬영 조건

```
isolated on a uniform neutral light-gray background,
soft neutral studio lighting,
very subtle ambient occlusion,
minimal soft contact shadow,
no dramatic cast shadows
```

배경은 **순백이 아니라 균일한 밝은 중성 회색** — 밝은 피부·뼈·금속 하이라이트가 묻히는 것을 줄인다.
`shadow`를 완전히 금지하지 않는다. 형태가 뜨거나 납작해지는 것을 막기 위해
**minimal soft contact shadow**만 허용한다.

## 6. 후처리 — 규정 부재

**인게임 포스트프로세싱 규정이 Notion 전체에 존재하지 않는다.**
06의 negative prompt에 있는 `motion blur`·`depth of field` 금지는
**레퍼런스 이미지 촬영 조건**이지 게임 후처리 규정이 아니다. 혼동하지 말 것.

확정된 것은 구조뿐 — 포스트프로세싱은 `VisualQualityProfile`에서 조정한다.
카메라 셰이크 강도·사이렌 음량·섬광 강도는 **접근성 옵션으로 분리**한다.

### 6.1 파티클 (확정 / 수치 미명시)

먼지·녹가루·스파크·정화 파편·캐스케이드 유입·급강하 잔해를 사용한다.

- **파티클은 판정 원인을 가리지 않는다**
- 화면을 채우는 불투명 파티클보다 **방향성 있는 짧은 파티클** 우선
- 단계별 최대 동시 파티클 수와 오버드로우 예산을 프로파일로 제한

## 7. 심볼 3종 디자인 체계 (확정) — 🔴 최우선 작업

### 7.1 공통 형태 규칙

- 모든 심볼은 통관 안에서 순환 가능한 **유사한 구형 부피**를 가진다
- **외곽 실루엣과 내부 코어는 종류마다 명확히 다르게** 만든다
- 작은 글자와 복잡한 표면 장식은 사용하지 않는다
- 정면뿐 아니라 **회전 중에도** 인식되도록 문양을 여러 면에 반복하거나 내부 코어를 차별화한다

**톤**: 정상 영혼은 완전히 선하고 깨끗한 인상보다 **이미 정제·순응된 존재**처럼.
저항체는 단순한 악성 바이러스보다 **내부에서 무언가가 버티거나 움직이는** 인상.

### 7.2 3종 대조표

| 축 | **정상 영혼** | **흡수체** | **증식체** |
|---|---|---|---|
| 외곽 실루엣 | 둥글고 안정된 외곽 | 불규칙한 외곽 | 분열선과 **새싹처럼 돋는 돌기** |
| 내부 코어 | 안정된 코어 | **중앙이 비거나 안쪽으로 빨려 들어가는 코어** | 하나의 구체 안에 **작은 핵이 여러 개** |
| 표면 문양 | 정돈됨, 돌출 적음 | 표면에서 **중심으로 향하는 홈** | 분열선 |
| 맥동 | 부드럽고 **반복적인** 내부 맥동 | **낮고 지속적인** 진동 | 판정 직전 작은 핵이 **복제될 듯 팽창** |
| 회전 | **일정한 방향**의 느린 회전 | 역회전·불규칙 진동·벽 부착 | 동일 규칙 적용 |
| 부속 요소 | 전력 탱크로 흡수되는 명확한 이동 | 전력 탱크로 가는 **가느다란 흡수 실선** | — |
| 기본 정화 | (대상 아님) | 중심 공백이 **닫히며 붕괴** | 내부 핵이 **동시에 터짐** |
| 직선 패턴 | — | 세 심볼을 **관통하는 절단선** | 분열선이 직선으로 이어져 **연쇄 폭발** |
| 연결 붕괴 | — | 연결된 코어가 한꺼번에 **압착**되고 빈칸으로 빨려 나감 | 작은 핵이 퍼지려다 **역으로 빨려 들어감** |

### 7.3 저항체 공통 규칙

- 종류별로 **한 가지 큰 상징만** 사용한다
- 계약 적용 시 **외곽 링 또는 계기판 표식**으로 보상 증가를 표시
- 3개 기본 정화 시 같은 종류 전체에 **공통 윤곽선이 연결**된다
- 패턴 성립 시 개수 정화보다 **더 강한 선·면·균열 연출**

> ⚠ 비주얼 부록 07의 *"위치가 떨어져 있어도 번호·색상·맥동을 통일"* VFX 규칙은
> 인접 정화 전환(`D-20260801-03`)으로 **무효화됐다.**

### 7.4 파쇄·상태 단계

- 저항체는 **온전함 · 미세 균열 · 심한 균열 · 파열 직전**의 최소 4상태 (균열 축)
- 각 심볼의 **기본 · 계약 · 정화 직전** 상태 (계약/정화 축)

두 축은 상보적이다. **두 축의 곱집합이 몇 개인지는 아무도 명시하지 않았다** — 제작 수량 불확정.

**정상 영혼 오배출 VFX와 저항체 정화 VFX가 명확히 달라야 한다.**

## 8. 1초 판독 규칙 (확정) — 통과의 산술적 전제

| 규칙 | 원문 |
|---|---|
| 색 단독 금지 | **색만으로 구분하지 않고** 실루엣·코어·표면 문양·움직임을 함께 사용 |
| 무채색 판독 | **색이 없어도** 실루엣과 코어 형태로 구분되어야 한다 |
| 정지 판독 | 결과는 빠르게 움직이는 중이 아니라 **정지된 3×3 판**에서 읽는다 |
| 1초 기준 | 정지된 결과판에서 **1초 안에** 세 심볼을 구분 |
| 색각 대응 | **흑백 또는 색각 차이**가 있어도 실루엣과 코어로 구분 |
| 겹침 금지 | 3×3 결과판에서 **서로 겹치지 않는 크기** |

> 🔴 `VISUAL_SPEC` §4는 심볼 3종이 **색 외 최소 3항목**으로 갈릴 것을 요구한다.
> 이 조건이 깨지면 판독성 평균 4.0은 **산술적으로 불가능**하다.
> 현재 REJECT의 근본 원인이 이것이다 → [CURRENT_STATE.md](CURRENT_STATE.md).

### 8.1 결과판 판독 우선순위 5단계

시각 효과도 **이 순서대로 점등**한다.

1. 정상 영혼과 저항체 구분
2. 같은 저항체의 총개수
3. 직선 3개 여부
4. 4개 이상 직교 연결 여부
5. 캐스케이드와 승객·부품 발동

(MASTER PRD §6.2는 이를 8단계로 세분화한다. 상충이 아니라 확장 관계.)

### 8.2 패턴 시각화

| 패턴 | 연출 |
|---|---|
| 개수 기본 정화 | 해당 종류에 **같은 외곽 윤곽선**, 번호·색상·맥동 통일, 보상 표시는 **작고 빠르게** |
| 직선 3개 | 가로·세로·대각선을 **관통하는 한 줄의 강한 효과**, 방향에 따라 **장치 내부 기계 부품이 반응** |
| 4개 이상 연결 | 연결된 칸을 **하나의 면처럼** 채움, **외곽선이 먼저 닫힌 뒤** 내부 붕괴, 제거된 칸에 **빈 공간을 명확히 보여준 뒤** 충전 |

### 8.3 캐스케이드 시각 규칙

- 제거 → 빈칸 → 새 심볼 유입 → 재판정 순서를 **생략하지 않는다**
- 너무 빠른 연쇄에서도 **각 단계의 원인이 최소 한 번은 읽혀야** 한다
- **큰 배수 하나와 핵심 발동 승객만** 강조한다

## 9. 3D 에셋 생성 파이프라인

### 9.1 에셋 1개당 이미지 4종 (확정)

1. **Clay Turnaround** — 형태·비례 추출용 무채색 정투상 멀티뷰
2. **Material Turnaround** — 컬러·재질·노후화 멀티뷰
3. **Detail Sheet** — 얼굴·손·문양·계기판·손잡이
4. **In-game Context Image** — 실제 배치 분위기

> 순서: **콘셉트 → 무채색 멀티뷰 → 파츠 분리 → 재질 멀티뷰 → 인게임 배치**.
> Clay Turnaround를 먼저 확정한 뒤 Material Turnaround를 제작한다.

### 9.2 프롬프트 5블록 구조 (확정)

```
[OBJECT]    오브젝트의 이름, 기능, 세계관상 역할
[GEOMETRY]  실루엣, 구조, 파츠 분리, 대칭 여부, 3D 복원 조건
[CAMERA]    정면·후면·좌우·상단 등 필요한 정투상 시점
[STYLE]     프로젝트 공통 아트 디렉션과 재질 방향
[OUTPUT]    배경, 조명, 여백, 해상도와 금지 조건
```

### 9.3 공통 마스터 프롬프트 (확정 — 원문)

```
[ASSET NAME AND DESCRIPTION],

single isolated game asset,
complete object fully visible,
clear and readable silhouette,
simplified construction suitable for 3D reconstruction,
low-poly friendly geometry,
clean separated parts,
no floating components,
no hidden major surfaces,
symmetrical where appropriate,

orthographic turnaround reference sheet,
front view, back view, left side view, right side view,
all views aligned to the same ground line,
all views shown at exactly the same scale,
consistent proportions and identical design across every view,
no perspective distortion,
no foreshortening,

stylized low-poly industrial occult horror game asset,
PS2-era survival horror influence,
faceted geometry,
chunky but believable proportions,
handcrafted indie horror visual language,
matching the established rusty supernatural elevator art direction,

neutral gray clay material,
uniform matte surface,
neutral studio lighting,
subtle ambient occlusion,
minimal soft contact shadow,

isolated on a uniform neutral light-gray background,
centered composition,
generous empty margin around the asset,
high resolution,
sharp clean edges.
```

### 9.4 캐릭터 포즈 스니펫 (확정)

```
neutral A-pose,
arms angled approximately 30 degrees away from the body,
palms facing downward,
fingers slightly separated,
feet shoulder-width apart
```

리깅용 정확한 수평 팔이 필요할 때만 별도로:
```
strict T-pose for rigging reference,
arms perfectly horizontal,
palms facing downward
```

### 9.5 🚨 장치 전용 프롬프트 — 그대로 쓰면 안 된다

에셋 부록 06 §13의 장치 Clay Turnaround 프롬프트에 **폐기된 수동 정지 장치가 남아 있다.**

```
one large mechanical stop button directly below each corresponding pipe,     ← 삭제
hostile resistance spheres can be stopped and aligned at the three windows,  ← 삭제
clear mechanical relationship between each pipe, its capture window
and its button,                                                              ← 삭제
```

시각 우선순위 1번 *"통관 3개와 버튼 3개의 대응 관계"*도 삭제 대상이다.

같은 페이지의 「필수 파츠」는 *"통관별 정지 버튼 없음"*, 3D 리소스 타겟은
*"통관별 정지 버튼을 만들지 않는다. 플레이어가 만지는 핵심 장치는 공통 실행 레버 하나다"*,
MASTER PRD §4.2는 명시적 제외다. **같은 문서 안의 자기모순이며 PRD·타겟이 이긴다.**

**블렌더 MCP로 돌리기 전에 위 3구절 + 우선순위 1번을 반드시 제거할 것.**

## 10. 제작 리소스 목록과 우선순위 (확정)

### 10.1 최종 범위 한 줄

> 엘리베이터 1세트 + 3통관 자동 룰렛 1세트 + 정상 영혼 1종 + 저항체 2종 +
> 패턴·캐스케이드 VFX + 복도 1세트 + 캐릭터 베이스 3종 + 부품 4종 + 위협 1종

### 10.2 제작 순서 12단계 (= 우선순위)

1. 엘리베이터 내부 그레이박스
2. 자동 룰렛 본체와 통관 3개
3. 통관별 결과창 3개 배치
4. **공통 실행 레버**
5. 정상 영혼·흡수체·증식체 더미
6. 자동 회전·결과 공개
7. 3개 정화·직선·연결 판정 VFX
8. 빈칸 재충전과 캐스케이드
9. 전력·계약·오염 UI
10. 승객·부품 더미와 발동 연출
11. 문 개폐·상승·과부하
12. 복도와 위협 실루엣

### 10.3 엘리베이터 모듈 명명

```
ELV_Wall_A            ELV_Wall_Ladder       ELV_Ceiling_Hatch_Closed
ELV_Ceiling_Hatch_Open  ELV_Door_Frame      ELV_Door_Sliding
ELV_Floor_Panel       ELV_Engine_Lever      ELV_Harvest_Display
ELV_Floor_Indicator   ELV_NPC_Bench
```

**엘리베이터 전체를 한 덩어리로 만들지 않는다.** 모듈로 분리한다.

### 10.4 UI·VFX로 처리 (3D 제작 대상 아님)

전력·돈·무게·남은 스핀 / 저항 계약 선택과 확률·보상·대가 / 심볼 가중치 /
개수 정화와 패턴 이름·배수 / 승객·부품 발동 순서 / 캐스케이드 단계 / 초과 전력 분배.

## 11. 🚫 금지 사항 — 완전 목록

### 11.1 아트 스타일

| # | 금지 | 근거 |
|---|---|---|
| A-1 | **스팀펑크 장식 과다** | `no ornate steampunk clutter` |
| A-2 | **카지노 슬롯머신 외형** | `no slot-machine reels, no casino decorations` |
| A-3 | **실사 PBR / 포토리얼** | `not photorealistic` |
| A-4 | **순수 픽셀아트 / 지나친 저해상도화** | `not pixel art` |
| A-5 | 심볼의 작은 글자·복잡한 표면 장식 | 07 |
| A-6 | 소품의 불필요한 미세 디테일 | `no unnecessary micro-detail` |
| A-7 | **결과판보다 눈에 띄는 장식** | 3D 타겟 |
| A-8 | 평면 UI 패널처럼만 보이는 장치 | 06 §8 |
| A-9 | **기능 없는 미세 조작부 남발** | 장식용 버튼과 실제 클릭 대상이 혼동되지 않게 |
| A-10 | 엘리베이터를 한 덩어리로 제작 | 06 §8 |
| A-11 | 계약 심볼의 과도한 희귀 연출 | 많이 나와야 하는 대상이므로 일관된 강조만 |
| A-12 | 순백 배경 | 균일한 밝은 중성 회색을 기본값으로 |

### 11.2 이미지 생성 Negative Prompt — 공통 (원문)

```
perspective view, wide-angle lens, fisheye distortion, foreshortening,
cinematic camera angle, dramatic lighting, hard cast shadow, strong rim light,
complex background, environment scene, multiple unrelated objects, duplicate object,
cropped, out of frame, occluded, floating parts, merged parts,
asymmetrical design inconsistency, different design between views,
blurry, pixelated, motion blur, depth of field, text, logo, watermark
```

**예외**: `shadow`를 완전히 금지하지 않는다. `minimal soft contact shadow`만 허용.

### 11.3 캐릭터 추가 Negative (원문)

```
dynamic pose, action pose, crossed arms, hands in pockets,
arms touching torso, legs touching, hair covering body, cape covering back,
weapon held across body, extreme facial expression, different costume in each view
```

### 11.4 지오메트리·구조

`no floating components` / `no hidden major surfaces` / `no invisible rear structure` /
`no perspective distortion` / `no foreshortening` / `no surrounding environment` /
`no attached unrelated props`

### 11.5 게임 디자인 — 현재 만들지 않는 것

통관별 정지 버튼·브레이크 조작 **(폐기)** / 구슬 위치 이동·교환 **(폐기)** /
연타·리듬·순서 입력 장치 **(폐기)** / 심볼 9종 완성 모델·9종 등급 체계 **(폐기)** /
실시간 타이밍 판독 기준 **(폐기)** / L·T·십자·고리 전용 구조 (프로토타입 제외, 설계는 유효) /
추가 통관·3×3보다 큰 보드 (범위 밖 — 단 **모듈 프레임은 형상으로 확보**) /
완성 NPC 8~12명 / 특수 층 전용 맵 / 전투 모션·완성형 파손 버전.

### 11.6 MASTER PRD 추가 금지

- §8.1 "별도의 균형추, 투명 관측창, 안전핀 장치를 만들지 않는다"
- §8.3 "사이렌은 지속 재생하지 않는다" / "카메라 셰이크보다 **환경 오브젝트와 승객을 우선** 흔든다"

## 12. 승인 전 잠그지 않을 항목 (확정)

캐릭터 최종 외형·의상 / 최종 모션·표정 / **심볼의 최종 색과 재질** /
공포 강도·점프스케어 여부 / 최종 수치 밸런스 / 캐스케이드 최종 속도 /
카메라 셰이크 강도 / 사운드 믹스 / UI 표현 방식.

각 항목은 **플레이스홀더와 2~3개 교체 가능한 프리셋**으로 구현한다.
승인 전에 코드·프리팹·머티리얼에 **단일 값으로 고정하지 않는다.**

## 13. Good / Bad — 검수 체크리스트

Notion에 「Good 사례 / Bad 사례」로 명명된 절이나 예시 이미지는 **존재하지 않는다.**
세 문서에 이미지가 0장이다. 가장 근접한 것이 아래 체크리스트다.

### 13.1 에셋 검수 (06 §11)

- [ ] 모든 시점에서 동일한 디자인인가?
- [ ] 정면·측면·후면의 비례가 일치하는가?
- [ ] 오브젝트 전체가 잘리지 않고 보이는가?
- [ ] 원근 왜곡 없이 정투상으로 표현됐는가?
- [ ] 주요 파츠가 서로 명확히 분리되어 있는가?
- [ ] 재질 얼룩이 형상으로 오인될 정도로 강하지 않은가?
- [ ] 배경과 오브젝트의 명도 대비가 충분한가?
- [ ] 프로젝트 Style Lock을 유지하는가?
- [ ] 실사도 픽셀아트도 아닌 중간 수준의 로우폴리인가?
- [ ] 엘리베이터 모듈의 연결 면과 두께가 일관적인가?

### 13.2 심볼·판독 검수 (07)

- [ ] 정지된 결과판에서 1초 안에 세 심볼을 구분할 수 있는가?
- [ ] 흑백 또는 색각 차이가 있어도 실루엣과 코어로 구분되는가?
- [ ] 같은 저항체 3개를 빠르게 셀 수 있는가?
- [ ] 직선과 연결 덩어리가 자동 판정 전에 눈으로도 읽히는가?
- [ ] 기본 정화와 패턴 잭팟의 연출 강도 차이가 명확한가?
- [ ] 캐스케이드 중 어떤 칸이 제거되고 새로 들어왔는지 이해되는가?
- [ ] 계약한 저항체와 일반 저항체의 차이를 오해 없이 읽을 수 있는가?
- [ ] 공포 분위기를 유지하면서도 결과 판독성을 해치지 않는가?

### 13.3 정량 합격선

[QUALITY_GATES.md](QUALITY_GATES.md) §5가 정본. 요약:
**판독성 평균 4.0 이상 / 스타일 일관성 평균 4.0 이상 / 단일 항목 2점 이하 없음 /
이전 버전보다 개선되지 않은 수정은 채택하지 않는다.**

루브릭 8번이 곧 Bad 판정선 — *"화면이 카지노 슬롯머신 또는 장식적 스팀펑크로 보이지 않는가?"*

## 14. 미결정 항목 (후보)

정상 영혼의 최종 형상과 서사적 흔적 / 흡수체·증식체의 최종 색과 재질 / **계약 링의 형태** /
패턴별 VFX 길이와 화면 흔들림 / 캐스케이드 속도 조절 옵션 / 고밀도 정상 영혼 도입 시점 /
후속 특수 패턴과 저항체의 시각 언어.

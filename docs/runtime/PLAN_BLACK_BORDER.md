# 검정색 테두리 — 진단 과 해결안

> 문제: 엘리베이터 기계 주변 테두리가 검정색으로 나온다.

## 1. RiskStateView가 ambientLight를 계산하는 방식

**파일**: `Assets/Prototype_Elevator/Scripts/Risk/RiskStateView.cs`

### 런타임 계산 경로 (LateUpdate)

| 줄 번호 | 함수 | 계산 |
|--------|------|------|
| 884 | `ApplyAmbient()` | `tinted = Lerp(_originalAmbient, _blended.LightColor, _ambientBlend)` |
| 901 | `ApplyAmbient()` | `tinted = RiskAmbientLadder.WithValue(tinted, _ambientValue)` |
| 904 | `ApplyAmbient()` | `RenderSettings.ambientLight = tinted * Lerp(1f, flicker, 0.5f)` |

### 위험 단계별 값 (프로파일 기본값)

**파일**: `Assets/Prototype_Elevator/Scripts/Data/Profiles/DangerFeedbackProfile.cs` (코드 프리셋)

| 위험 단계 | LightColor (RGB) | LightIntensity |
|---------|------------------|----------------|
| **Stable** | (1.00, 0.85, 0.80) | 1.00 |
| **Strain** | (1.00, 0.83, 0.75) | 0.92 |
| **Critical** | (1.00, 0.75, 0.50) | 0.80 |
| **Collapse** | (1.00, 0.50, 0.30) | 0.60 |

**적용 방식**:
- `_originalAmbient` (편집 모드): ~0.0015 (매우 어두움)
- `_ambientBlend` 기본값: **0.55** (line 48) — 원래 앰비언트 45%, 위험 색 55%
- `_driveAmbientValue` 기본값: **true** (line 52) — 명도축도 함께 변경
- `_ambientValueFloorRatio` 기본값: **0.20** (line 57) — Collapse 시 최소 20% 명도 유지

**플레이 모드**: RiskStateView가 `RenderSettings.ambientLight` 를 약 **0.0997** (0.0015의 66배) 로 올림

## 2. 문제의 근본 원인

셰이더 **ambient 항** (AscendStylized.shader:765-766):

```hlsl
float3 ambientTerm = albedo * sh * _ShadowLift
                   + _ShadowTint.rgb * 0.35 * saturate(Lum(sh) * 2.0);
```

- `sh` = `SampleSH(normalWS)` — **Flat 모드라 화면 전체에서 상수** (line 760-763)
- `_ShadowLift` = **0.55** (기본값) — 어두운 쪽을 밝힘
- `_ShadowTint` = (0.20, 0.26, 0.24) — 회녹색 그림자 색

**핵심**: `sh` 가 전역 `RenderSettings.ambientLight` 값이고, 그 값이 0.0015 → 0.0997 로 변할 때
`ambientTerm` 은 너무 작아 실루엣 구분이 안 된다. 특히 **광원이 닿지 않는 기계 음영 부위**가 검정으로 떨어진다.

## 3. 해결안 3개

### (a) 전역 Ambient를 올린다

**무엇을 바꾸는가**: `RenderSettings.ambientLight` 의 최소값

**구체적 변경**:
- `RiskStateView.cs:48` — `_ambientValueFloorRatio` 를 **0.20 → 0.35** 로 상향
  - 결과: Collapse 시에도 앰비언트 명도가 35% (현 20%) 이상 유지
- 또는 `RiskStateView.cs:884` — `_ambientBlend` 를 **0.55 → 0.75** 로 상향
  - 결과: 위험 색이 25% 덜 섞임, 원래 앰비언트의 영향 증가

**장점**:
- 구현 단순 (상수 하나 변경)
- 즉시 효과 (플레이 모드에서 바로 보임)

**부작용**:
- 전체 방이 밝아짐 — 위험 단계 간 대비 감소
- `VISUAL_SPEC.md §12 목표 대역` (mean 0.055~0.075) 을 초과할 수 있음
- 프리셋 비교 시 모든 상태가 함께 밝아져 캡처 A~F 기준선과 차이 발생

**되돌리는 법**:
```csharp
// 되돌리기
_ambientValueFloorRatio = 0.20f;     // 또는
_ambientBlend = 0.55f;
```

### (b) 셰이더에 최소 필(fill) 항을 넣는다

**무엇을 바꾸는가**: `AscendStylized.shader` 에 스탠드얼론 앰비언트 기본값 추가

**구체적 변경**:
```hlsl
// AscendStylized.shader 의 Properties에 추가:
_MinFill ("최소 채우기 (상수 밝기)", Range(0, 0.3)) = 0.06

// Frag 함수, 765줄 근처에서:
float3 ambientTerm = albedo * sh * _ShadowLift
                   + _ShadowTint.rgb * 0.35 * saturate(Lum(sh) * 2.0)
                   + albedo * _MinFill;  // ← 추가
```

**정확한 수치** (추측):
- 기본값 `_MinFill = 0.06` — Stable 에서 현재 앰비언트의 약 6%를 상수로 더함
- 목표: 광원이 안 닿는 기계 부위를 명도 0.01 이상 유지

**장점**:
- 기계 실루엣이 검정에서 회색으로 떨어짐 (구분 가능)
- RiskStateView 변경 없음 — 위험 단계 연출 그대로 유지
- 머티리얼별로 켜/끌 수 있음 (shader_feature_local)

**부작용**:
- 셰이더 기본값 불변 규약 위반 (하지만 키워드로 격리 가능)
- 모든 머티리얼에 적용되면 방 전체가 밝아짐 (필 강도 조정 필요)
- 고정 캡처 A~F의 조명 값이 미묘하게 변함

**되돌리는 법**:
```hlsl
// 셰이더에서 _MinFill 줄 삭제 또는 기본값을 0으로 재설정
```

### (c) 리플렉션 프로브 또는 보조 조명을 넣는다

**무엇을 바꾸는가**: 장면에 **간접광 원본** 추가

**구체적 변경**:
1. **ReflectionProbe** (비베이크):
   - 장면에 Sphere ReflectionProbe 하나 생성
   - `Intensity` = 0.5~1.0 (기본 1)
   - `Mode` = Realtime (성능 고려)
   - 엘리베이터 기계 주변에 배치

2. 또는 **보조 Point Light** (3번째 광원):
   - 기계 뒤쪽에 Point Light 추가
   - `Intensity` = 0.8~1.2
   - `Range` = 3~4 m
   - `Color` = (0.8, 0.8, 0.7) 중성회색

**정확한 수치** (추측):
- ReflectionProbe: Intensity 0.8, Blending Distance 2.0
- 또는 Point Light: Intensity 1.0, Range 3.5 m, Color (0.85, 0.85, 0.80)

**장점**:
- 간접광이 씬 전체에 자연스럽게 퍼짐 (EEVEE 처럼)
- RiskStateView/셰이더 수정 불필요
- 레퍼런스(블렌더 EEVEE)에 더 가까워짐

**부작용**:
- 성능 비용 (실시간 프로브는 매 프레임 업데이트)
- 위험 단계가 간접광까지 물들어야 함 (색조 관리 복잡)
- 고정 캡처 베이스라인이 크게 바뀜
- **「라이트맵 베이킹은 시간 비용 높음」** (제외)

**되돌리는 법**:
```
장면에서 ReflectionProbe/Point Light 삭제
```

## 4. VISUAL_SPEC과의 충돌 지점

**`docs/VISUAL_SPEC.md` §12 목표 대역**:

| 지표 | 레퍼런스 | 허용 대역 | 현재 상태 |
|------|---------|---------|---------|
| mean (전체 밝기) | 0.0647 | 0.055~0.075 | **미측정** |
| p50 (중앙값) | 0.0512 | 0.040~0.062 | **미측정** |
| g/r 색조 | 0.829 | 0.78~0.90 | **위험** |

**인용**:
> "레퍼런스는 **따뜻하다**(r > g > b). `VISUAL_BIBLE` §3 의 「탁한 갈색」과 일치하고,
> **차가운 회녹색은 §2.1 이 「그림자 색」으로 한정**한 것이지 방 전체의 색이 아니다."

**충돌**: (a) ambient 를 올리면 회녹색 `_ShadowTint` (0.20, 0.26, 0.24, 불포화 청색)이 함께 강해져
목표 색조(따뜻한 갈색)에서 멀어질 수 있음.

## 5. 추천

**추천: (b) 셰이더 최소 필(fill) 항 + (a) 앰비언트 값 미세 조정**

**이유**:
1. **(b)만으로도 기본 결함 해결** — 기계 실루엣이 검정→회색으로 개선
2. **셰이더 키워드로 격리** — 필요한 머티리얼에만 적용, 되돌리기 용이
3. **(a)와 조합 시 대비 보존** — 위험 단계 간 밝기 간격 유지 (§12 지표 근거)
4. **프로브/조명 추가 없음** — 성능·베이스라인 영향 최소

**단계별 실행**:
1. 셰이더에 `_MinFill` 토글 추가 (기본 끔)
2. 기계 머티리얼만 켜서 캡처 비교
3. 필요하면 `_ambientValueFloorRatio` 를 0.20 → 0.25 로만 상향 (보수적)
4. 색조(g/r, b/r) 측정 후 `VISUAL_SPEC` 대역 재확인

**소요 시간**: 10~20분 (셰이더 변경 + 캡처 1회)


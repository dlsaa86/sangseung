# 빌드 콘텐츠 감사 (2026-08-09)

## 결론

**패턴 기반 점수 시스템의 3갈래가 성립하려면 추가 콘텐츠가 필요하다.**

총 14개 아이템/승객 + 0개 기본 미구현 = **콘텐츠 자체는 충분**하지만,
효과 종류별 커버리지가 **비대칭**해 3갈래 균형이 깨져 있다.

---

## 1. 저작된 아이템·승객 전수

**저장소 위치:** `BuildLoadout.cs:210~485` (코드 상수)

| ID | 이름 | 종류 | Axis | 무게 | 하차층 | 주요 효과 | 조건 효과 수 |
|---|---|---|---|---|---|---|---|
| PSG_SURVEYOR | 계측 기사 | 승객 | Stability | 8 | 5 | PurifyThreshold(2) | 2개 |
| PSG_MOURNER | 문상객 | 승객 | Stability | 7 | 6 | RefillSoulBias(0.15) | 1개 |
| PSG_TECHNICIAN | 정비공 | 승객 | Stability | 9 | 7 | NormalSoulValue(+3) | 2개 |
| PSG_PORTER | 짐꾼 | 승객 | Load | 10 | 8 | CapacityBonus(+30) | **0개** |
| PSG_ZEALOT | 광신자 | 승객 | Residual | 16 | 10 | Appearance(1.25 증식) | 2개 |
| PSG_SURVEYOR_LINE | 측량사 | 승객 | Pattern | 6 | 9 | LineMultiplier(+0.5) | 2개 |
| PSG_METER_READER | 검침원 | 승객 | Residual | 11 | 8 | ResidualForgive(1) | 2개 |
| PRT_DIAGONAL_BINDER | 사선 결속기 | 부품 | Cascade | 26 | — | DiagonalConnects | 1개 |
| PRT_CASCADE_GOVERNOR | 연쇄 조속기 | 부품 | Cascade | 22 | — | CascadeStep(+0.25) | 2개 |
| PRT_RESIDUAL_DAMPENER | 잔류 감쇠기 | 부품 | Residual | 18 | — | ResidualMitigation(0.55) | 2개 |
| PRT_CASCADE_COIL | 연쇄 코일 | 부품 | Cascade | 20 | — | ExtraCascadeReroll(1) | 1개 |
| PRT_SOUL_TRAP | 영혼 포집망 | 부품 | Stability | 16 | — | GuaranteeNormalSouls(1) | 1개 |
| PRT_PATTERN_DOUBLER | 중복 계수기 | 부품 | Pattern | 24 | — | MultiplePatterns | 1개 |
| PRT_OVERHARVEST_TRANSFORMER | 과수확 변압기 | 부품 | Residual | 24 | — | PurifyReward(1.5배) | 0개 |

**총 14개** (승객 7, 부품 7)

---

## 2. BuildEffectKind 별 커버리지

**코드 위치:** `BuildItem.cs:52~108`

| 효과 종류 | 구현 여부 | 사용한 품목 | 개수 |
|---|---|---|---|
| `PurifyThreshold` | ✓ | 계측 기사 | 1개 |
| `GuaranteeNormalSouls` | ✓ | 영혼 포집망 | 1개 |
| `RefillSoulBias` | ✓ | 문상객 | 1개 |
| `ResidualMitigation` | ✓ | 잔류 감쇠기, 과수확 변압기 | 2개 |
| `CascadeStep` | ✓ | 연쇄 조속기, 연쇄 코일 | 2개 |
| `PatternBonus` | ✓ | 계측 기사, 광신자 | 2개 |
| `PurifyReward` | ✓ | 계측 기사, 광신자, 잔류 감쇠기, 과수확 변압기 | 4개 |
| `NormalSoulValue` | ✓ | 정비공, 문상객, 영혼 포집망 | 3개 |
| `MultiplePatterns` | ✓ | 중복 계수기 | 1개 |
| `DiagonalConnects` | ✓ | 사선 결속기 | 1개 |
| `ClusterMultiplier` | ✓ | 사선 결속기 | 1개 |
| `LineMultiplier` | ✓ | 측량사 | 1개 |
| `Appearance` | ✓ | 광신자 | 1개 |

**미사용 코드 효과:** 0개 (모두 구현됨)

---

## 3. 패턴 기반 시스템의 3갈래 성립 가능성

**필요한 갈래:**

### (A) 안정형 — PurifyThreshold + Appearance
- **필요 효과:** PurifyThreshold (정화 최소 개수 낮추기)
- **필요 효과:** Appearance (특정 심볼 빈도 ↑)
- **현황:** ✓ 모두 구현됨 (계측 기사 + 광신자 또는 future 품목)
- **비용:** 0개 추가 필요

### (B) 조화형 — DiagonalConnects + PatternBonus
- **필요 효과:** DiagonalConnects (대각 연결 인정)
- **필요 효과:** PatternBonus (패턴 배수 상향)
- **현황:** ✓ 모두 구현됨 (사선 결속기 + 계측 기사/광신자)
- **비용:** 0개 추가 필요

### (C) 폭발형 — Appearance(증식) + ClusterMultiplier
- **필요 효과:** Appearance 증식체 (광신자가 함)
- **필요 효과:** ClusterMultiplier (연결 배수 상향)
- **현황:** ✓ 모두 구현됨 (광신자 + 사선 결속기)
- **비용:** 0개 추가 필요

**결론:** 3갈래 모두 **현재 콘텐츠로 성립 가능**

---

## 4. 등장 타이밍 — 적재 보상이 5층부터 맞는가?

**FloorPlan.cs 확인:**
- 1층(Hero Slice): `OffersBuildReward = false` → 미제시
- 2층: `OffersBuildReward = false`
- 3층: `OffersBuildReward = false`
- 4층: `OffersBuildReward = false`
- **5층:** `OffersBuildReward = true` ← **첫 제시**
- 8층: `OffersBuildReward = true`

**문제:** 초반 4층 동안 빌드를 못 씀 → "빌드를 이용해야만 가능"이 초반에는 불가능

**선택지:**
1. 2층부터 제시 (패턴 기반 시스템 완성 후)
2. 1층 Hero Slice에만 제시 (현재 구조 유지)

---

## 5. 과수확 게이팅 — 구현 지점 확정

**파일:** `FloorSession.cs:470~471`

```csharp
public bool CanTakeExtraSpin => IsOverharvestUnlocked && SpinsRemaining > 0
    && ExtraSpinsTaken < _overharvest.MaxExtraSpins;

public bool IsOverharvestUnlocked => _overharvest.IsUnlocked(Power, RequiredPower);
```

**현황 (464줄):**
```csharp
public bool IsOverharvestUnlocked => _overharvest.IsUnlocked(Power, RequiredPower);
```

**UnlockThreshold 저장소:**
- 파일: `OverharvestProfile.cs` (코드 기본값) + `OverharvestProfile.asset` (에셋 덮어쓰기)
- 기본값: 1.0 (100% 달성)

**추가 조건을 넣는 가장 작은 변경:**

```csharp
// Option 1: UnlockThreshold만 상향 (1.0 → 1.15)
// OverharvestProfile.cs의 기본값을 바꾸면 완료

// Option 2: BuildLoadout 조건 추가
public bool IsOverharvestUnlocked => 
    _overharvest.IsUnlocked(Power, RequiredPower)
    && (_loadout == null || _loadout.Count > 0);  // 빌드 있어야만 해금
```

**권장:** Option 1 (수치만 변경, 코드 최소)

---

## 6. 결론 — 추가 저작 필요량

### 패턴 기반 점수 시스템 구현 후

| 조건 | 필요한 것 | 개수 |
|---|---|---|
| **3갈래 성립** | 추가 아이템/승객 | **0개** ✓ |
| **초반 빌드 사용 가능** | 2층부터 제시 (코드 변경만) | 0개 ✓ |
| **과수확 게이팅** | UnlockThreshold 상향 (코드 1줄) | 0개 ✓ |
| **新 패턴 효과** | `TargetPairFrequency` 등 (구현 필요) | **최대 3개 이펙트** |

### 최종 결론

**사용자 지시 「빌드를 이용해야만 가능」를 구현하려면:**

1. ✓ 콘텐츠: 14개 기존 아이템으로 충분
2. △ 코드: 패턴 기반 효과 `TargetPairFrequency`, `CrossPatternMode` 등 **추가 3~5개 BuildEffect 구현 필요**
3. ✓ 게이팅: UnlockThreshold 상향만 (1줄)
4. ✓ 타이밍: 2층부터 제시 (1줄)

**콘텐츠 추가 작업: 0개**
**코드 추가 작업: 3~5개 새로운 BuildEffect**

---

## 참고

- BuildCatalog는 코드 상수로만 존재 (에셋 없음)
- 모든 아이템이 DestinationFloor를 명시 → 층별 제시 로직이 있는지 `OffersFor` 메서드 확인 필요
- 조건부 효과(`When`)는 전부 구현됨 → 패턴 기반 시스템 추가 효과만 필요


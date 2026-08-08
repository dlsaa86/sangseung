# 전력 의존 분석 — 레버만 당기기 차단 방안 (2026-08-09)

## 1. 전력 계산 경로

### 기본 산출 (무계약, 무적재)

| 단계 | 파일:줄 | 값 | 적용점 |
|---|---|---|---|
| 정상 영혼 기본값 | SpinRuleSet.cs:42 | `14.0f` | 영혼 개수 × 14 × 캐스케이드 배수 |
| 정화 보상 기본값 | SpinRuleSet.cs:53 | `8.4f` | 정화 칸 수 × 8.4 × 정화배수 × 캐스케이드 배수 |
| **전력 최종 계산** | SpinEngine.cs:331, 279~303 | `GrossPower` | 영혼 + 정화 (순서: 가산) |
| **순전력 (NetPower)** | SpinEngine.cs:405 | `GrossPower - ResidualLoss` | 흡수체 잔류 차감 후 |

### 적용 순서 (규칙 다발 생성 시)

1. **기본값** (SpinRuleSet) → 2. **층** (FloorPlan.cs) → 3. **계약** (ResistanceContract) → 4. **승객/부품** (BuildLoadout.ApplyTo:128)

(중요: 계약이 승객보다 먼저 적용되므로, 계약의 곱셈이 승객의 가산 위에 얹혀 같은 조합도 순서와 무관하게 같은 값을 낸다)

---

## 2. 지금 「레버만으로」 통과하는 이유

### 측정값 (1층, 요구 330)

| 스핀 | 측정 전력 | 요구 대비 |
|---|---|---|
| 1회 | 104 | 48% |
| 2회 | 156 | 73% |
| 3회 | 510 | **237%** |

**결론:** 5회 중 2~3회만으로 충분 (PLAN_EARLY_DIFFICULTY.md 실측)

### 근본 원인 —정상 영혼만으로 가능

- 기본 영혼 기대값: 9칸 중 4~5개 (심볼 가중치 기본)
- 4개 × 14 = 56 (17%)
- 캐스케이드 1회로 정화 추가 → 200+p (60%+)
- 미:정 비율 변화로 스핀 3회 → 510 (237%)

**계약/승객 없음:**
- 1층 계약 선택 없음 (FloorPlan.cs:333 Array.Empty)
- 적재는 5층부터 (FloorPlan.cs:353, OffersBuildReward)

---

## 3. 빌드 의존 만드는 세 축

### ① 기본 산출 낮추기
- `NormalSoulValue` 14→12 (−14%), 모든 층 영향
- 단점: 계약/승객도 약해짐

### ② 요구 전력 올리기
- FloorPlan.cs:332 `RequiredPower = 330` → 480~550
- 효과: 330→480 (+45%): 스핀 3.5회 필요 → 레버만 불충분
- 단점: 곡선 전체 상승, 중반 재조정 필요

### ③ 승객/부품 조건화
- 2층부터 OffersBuildReward = true (FloorPlan.cs:353)
- 초반용 경량 승객 추가 (무게 10~15kg, 보너스 소)
- 효과: 계약+적재로 점진적 수용곡선 → 기본산 상쇄 가능

**권장:** ②+③ 조합 (요구 480, 2층부터 적재)

---

## 4. 과수확 게이팅 — CanTakeExtraSpin

**파일:** FloorSession.cs:470~471

```csharp
public bool CanTakeExtraSpin => IsOverharvestUnlocked && SpinsRemaining > 0
    && ExtraSpinsTaken < _overharvest.MaxExtraSpins;
```

**세 조건:**
1. `IsOverharvestUnlocked` (464줄): Power ≥ RequiredPower × UnlockThreshold (기본 1.0)
2. `SpinsRemaining > 0`: 남은 스핀
3. `ExtraSpinsTaken < MaxExtraSpins`: 프로파일 상한

**아이템/승객/계약 조건 추가 — 가장 적은 변경:**
- UnlockThreshold 1.0 → 1.15로 상향 (OverharvestProfile.cs 또는 .asset)
- 또는 BuildLoadout.Count ≥ 2 조건 추가 (BuildLoadout.ApplyTo 근처)

---

## 5. 구슬 종류

### 현재 3종 심볼

- `NormalSoul` (정상 영혼)
- `Absorber` (흡수체)
- `Proliferator` (증식체)

### 층별 풀

| 층 | 풀 | 비고 |
|---|---|---|
| 1~5 | Soul + Absorber | 증식체 6층에 소개 |
| 6~10 | Soul + Absorber + Proliferator | 3종 |

### 종류 추가 시

1. SymbolKind.cs에 새 값
2. SpinRuleSet.cs에 가중치 필드
3. FloorPlan.cs에 SymbolPool 수정
4. ResistanceWeightScale 재계산 (종류 증가 시 배율 상향)

**권장:** 새 종류보다 현 3종 밸런스 우선

---

## 6. 권장안 — 즉시 시행

| 파일:줄 | 현재 | 제안 | 사유 |
|---|---|---|---|
| FloorPlan.cs:332 | 330 | **480** | 기본산 200~250 → 스핀 3.5회 필요 |
| OverharvestProfile | 1.0 | **1.15** | 과수확 임계 상향 (100% → 115%) |
| FloorPlan.cs:353 (2층) | false | **true** | 초반부터 적재 선택 가능 |

**되돌리는 법:** 값 복구만으로 복원 (구조 변경 없음)

---

**근거:** FloorPlan.cs (방향), SpinEngine.cs (산출), FloorSession.cs (계산), PLAN_EARLY_DIFFICULTY.md (측정)  
**코드 추정:** 심볼 기대값 계산 = 심볼풀 가중치 비율 × 9칸

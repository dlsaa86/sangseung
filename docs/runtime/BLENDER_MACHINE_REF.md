# Blender Machine Part Materials Reference

**측정 일시:** 2026-08-09  
**측정자:** scout (blender_bridge.py, 한 번의 페이로드)  
**계산 방식:** Principled BSDF Base Color RGB 직읽기  
**중요:** 이 문서는 **알베도(Base Color)만** 기록한다. 렌더 후 최종 휘도(lighting + tone mapping 포함)는 다르다. 렌더 무드 평가는 고정 캡처로 수행할 것.

---

## 1. 벽 기준 (M_Cab_Wall)

| 항목 | 값 |
|---|---|
| **Base Color RGB** | (0.8000, 0.8000, 0.8000) |
| **휘도** | 0.8000 |
| **채도** | 0.0000 (완벽한 그레이) |

---

## 2. 기계 부품 머티리얼 목록

### Core 구조재 (공통)

| 머티리얼 | RGB | 휘도 | 채도 | 벽 대비 | Metallic | Roughness | 사용 부품 |
|---|---|---|---|---|---|---|---|
| **M_Elev_FrameSteel** | (0.0830, 0.0790, 0.0740) | 0.0795 | 0.1084 | 9.94% | 0.88 | 0.46 | Frame, Lever, Dispenser |
| **M_Elev_BoltSteel** | (0.2750, 0.2660, 0.2480) | 0.2666 | 0.0982 | 33.33% | 0.95 | 0.26 | Bolts, Fasteners |
| **M_Elev_CavityDark** | (0.0070, 0.0070, 0.0070) | 0.0070 | 0.0000 | 0.88% | 0.00 | 0.95 | Recesses |

### 베젤 및 하우징

| 머티리얼 | RGB | 휘도 | 채수 | 벽 대비 | Metallic | Roughness | 사용 부품 |
|---|---|---|---|---|---|---|---|
| **M_Elev_ChamberBezel** | (0.2050, 0.2000, 0.1920) | 0.2005 | 0.0634 | 25.06% | 0.86 | 0.31 | Dispenser Door/Housing, Gauge Housing, Siren Cage |
| **M_Elev_ChamberBezel_Chamber** | (0.2050, 0.2000, 0.1920) | 0.2005 | 0.0634 | 25.06% | 0.86 | 0.31 | Chamber Array |

### 레버 및 어두운 부품

| 머티리얼 | RGB | 휘도 | 채도 | 벽 대비 | Metallic | Roughness | 사용 부품 |
|---|---|---|---|---|---|---|---|
| **M_Elev_DarkIron** | (0.0460, 0.0430, 0.0380) | 0.0433 | 0.1739 | **5.41%** | 0.88 | 0.60 | Lever Bay |
| **M_Elev_LeverIron** | (0.1180, 0.1090, 0.0940) | 0.1098 | 0.2034 | 13.73% | 0.88 | 0.36 | Lever Handles |

### 유리 및 광학

| 머티리얼 | RGB | 휘도 | 채도 | 벽 대비 | Metallic | Roughness | 사용 부품 |
|---|---|---|---|---|---|---|---|
| **M_Elev_ChamberGlass** | (0.0450, 0.0470, 0.0520) | 0.0469 | 0.1346 | 5.86% | 0.00 | 0.05 | Chamber Glass ×9 |

### 표시 및 장식

| 머티리얼 | RGB | 휘도 | 채도 | 벽 대비 | Metallic | Roughness | Emission | 사용 부품 |
|---|---|---|---|---|---|---|---|---|
| **M_Elev_TickPaint** | (0.0300, 0.0290, 0.0270) | 0.0291 | 0.1000 | 3.64% | 0.00 | 0.82 | 0.00 | Gauge Labels |
| **M_Elev_GaugeFill** | (0.9500, 0.1350, 0.0140) | 0.2995 | **0.9853** | 37.44% | 0.00 | 0.34 | **0.55** | Gauge Fill (발광) |
| **M_Elev_SirenLens** | (0.6000, 0.2400, 0.0300) | 0.3014 | **0.9500** | 37.68% | 0.00 | 0.24 | **0.80** | Siren Bulb (발광) |
| **M_Elev_LampEmissive** | (0.4000, 0.0200, 0.0080) | 0.0999 | **0.9800** | 12.49% | 0.00 | 0.30 | **0.90** | Lamp Emitter (발광) |

---

## 3. 기계 부품 전체 휘도 분석

### 비발광 부품만 (단순 평균)

머티리얼별 휘도 평균 (발광 제외):
```
(0.0795 + 0.2666 + 0.0070 + 0.2005 + 0.2005 + 0.0433 + 0.1098 + 0.0469 + 0.0291) / 9
= 0.1228
```

**기계 전체 평균 휘도:** 0.1228  
**벽 대비 비율:** **15.35%** (벽이 약 6.5배 더 밝음)

---

## 4. 채도 분포 (HSV Saturation)

**무채색 (S = 0%):**
- M_Elev_CavityDark

**저채도 (0% < S < 15%):**
- M_Elev_FrameSteel (10.84%)
- M_Elev_BoltSteel (9.82%)
- M_Elev_ChamberBezel (6.34%)
- M_Elev_ChamberGlass (13.46%)

**중채도 (15% ≤ S < 25%):**
- M_Elev_DarkIron (17.39%)
- M_Elev_LeverIron (20.34%)
- M_Elev_TickPaint (10.00%)

**고채도 발광 부품 (S > 95%):**
- M_Elev_GaugeFill (98.53%)
- M_Elev_SirenLens (95.00%)
- M_Elev_LampEmissive (98.00%)

---

## 5. 주요 발견

### 암정 대비 분석

| 그룹 | 범위 | 실측 |
|---|---|---|
| **극암** (거의 검은색) | L < 0.01 | M_Elev_CavityDark (0.007) |
| **매우 어두움** | 0.01 ≤ L < 0.05 | M_Elev_DarkIron (0.0433), M_Elev_TickPaint (0.0291) |
| **어두움** | 0.05 ≤ L < 0.15 | M_Elev_FrameSteel (0.0795), M_Elev_ChamberGlass (0.0469) |
| **중간톤** | 0.15 ≤ L < 0.35 | M_Elev_BoltSteel (0.2666), M_Elev_ChamberBezel (0.2005) |
| **중간-밝음** | 0.35 ≤ L < 0.6 | (없음) |
| **벽** | L ≥ 0.6 | M_Cab_Wall (0.8000) |

### 사용자 피드백 상황

기존 M_Elev_DarkIron (L=0.0433, 벽의 5.41%)은 극도로 어두워 색감이 거의 보이지 않음.  
**권장:** 레버베이의 휘도를 벽의 25~35% 정도로 상향 조정하면 따뜻한 갈색 톤(S=17.39%)이 가시화될 것으로 예상.

---

**측정 신뢰도:** ✓ 블렌더 API 직읽기 · Principled BSDF 기본값  
**한계:** 렌더 결과는 조명(warm 2000~3000K), Tone mapping(AgX), Exposure(2.3)의 영향을 받음.  
**다음 단계:** 색상값 수정 후 고정 캡처로 시각 평가 수행.

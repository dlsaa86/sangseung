<#
.SYNOPSIS
    capture-metrics 지표 계산기의 자체 검사 — 합성 이미지로 반증한다.

.DESCRIPTION
    이 저장소는 「측정 도구가 틀린 것을 재고 있었다」로 여러 번 실패했다.
    그래서 손으로 계산할 수 있는 이미지를 만들어 도구의 출력이 그 값과 일치하는지
    단정한다. **기댓값을 도구에 맞추지 않는다 — 어긋나면 도구가 틀린 것이다.**

    검사 케이스

      A  순수 단색 (128 회색)          국소분산 0 · 빈평면 100% · 평탄구간 1개(최장=폭) · L5=L50=L95=128
      B  화소별 랜덤 노이즈            국소분산 45~65 · 빈평면 0% · 평탄구간 200개 이상
      C  4단 계단 (0/64/128/192)       평탄구간 4개 · 각 길이 = 폭/4 · 국소분산 0 · 빈평면 100%
      D  검정 + 흰 사각 5%             발광 비율 정확히 5.000%
      E  순수 마젠타 (255,0,255)       마젠타 검출 = 전체 화소 · 금색 0 (AND 조건 반증)
      F  순수 금색 (212,175,55)        금색 검출 = 전체 화소 · 마젠타 0 (AND 조건 반증)
      G  1단위 계단 (0/1/2/3)          δ≤1 평탄구간 1개 · δ=0 평탄구간 4개
      H  2단위 계단 (0/2/4/6)          δ≤1 · δ=0 모두 평탄구간 4개

    G·H 가 한 쌍으로 「≤ 1」의 경계를 양쪽에서 고정한다. 이것이 없으면 임계값이
    조용히 바뀌어도 아무도 모른다. G 는 동시에 δ=1 과 δ=0 이 **다른 답을 낸다**는
    사실을 고정한다 — 실제 캡처에서 이 둘이 결론을 뒤집기 때문이다.

    ── G-4 계단·단차 반증 케이스 (I~N) ─────────────────────────────────────────
    직전 G-4 정의(「평탄구간 ≤ 24개 그리고 최장 ≥ 20px」)는 **완전히 평평한 벽을
    자동으로 통과시켰다.** 무지 벽의 주사선은 평탄 구간 1개·최장 200px 이라 두 조건을
    여유롭게 만족한다. 아래 45건은 그 결함을 하나도 잡지 못했다 — 계단 수만 셌지
    「계단 사이에 단차가 있는가」를 묻지 않았기 때문이다.

      I  완전 무지 주사선 (A 재사용)   계단 1 · 단차 0 · **관측 안 됨**  ← 이번 결함의 회귀 방지선
      J  4단 계단 64간격 (C 재사용)    계단 4 · 단차 3 · **관측됨**
      K  매끄러운 선형 램프 (화소당 +1) δ≤1 계단 1 · 단차 0 / δ=0 계단 0 · **관측 안 됨**
      L  단차가 작은 4단 (H 재사용)     계단 4 · 단차 0 · **관측 안 됨** (구간은 나뉘었으나 안 보인다)
      M  4px 폭 64단                    계단 0 (전부 8px 미만) · **관측 안 됨**
      N  64px + 3px 조각 + 64px         계단 2 · 단차 1 — 짧은 조각을 건너뛰고 이웃 계단끼리 비교한다

    ── G-1b·G-1c 반증 케이스 (O~R) ─────────────────────────────────────────────
    G-1a(전체 블록 중앙값)는 「텍스처가 잘 보이는가」가 아니라 「화면의 몇 %가
    텍스처인가」를 재고 있었다 (GRAPHICS_TARGET §5.1). 아래가 그 결함을 고정한다.

      O  완전 무지 (A 재사용)          G-1a 0 · **G-1b 정의불가(블록 0개)** · G-1c 0%
      P  좌 1/2 무지 + 우 1/2 노이즈    텍스처 블록 정확히 512/1024 · G-1a 는 눌리고 G-1b 는 노이즈 값
      Q  좌 3/4 무지 + 우 1/4 노이즈    **G-1a 가 정확히 0** 인데 G-1b 는 ≈55 — 결함 그 자체
      R  전면 노이즈 (B 재사용)         전 블록이 텍스처 → **G-1b ≡ G-1a** (불변식)

    Q 가 핵심이다. 텍스처가 화면의 25% 를 덮고 있어도 G-1a 는 0 을 낸다 —
    「통과선 12.0 에 닿으려면 화면의 절반 이상이 텍스처여야 한다」가 이것이다.

    ── G-4 세 갈래 반증 케이스 (S~W) ───────────────────────────────────────────
    주사선이 조명 없는 면 위에 있으면 계단은 **원리적으로** 만들어지지 않는다.
    그것을 「미관측」으로 세면 「고치면 되는 것」과 섞인다 (GRAPHICS_TARGET §5.4).

      S  주사선 완전 단색 (A 재사용)    휘도폭 0 → **측정 불가**
      T  휘도폭 5 인데 계단 4·단차 3    직전 두 갈래로는 「관측됨」 — 지금은 **측정 불가**
      U  휘도폭 8 인 같은 모양          경계값 → **관측됨** (임계를 양쪽에서 고정한다)
      V  4단 계단 192폭 (C 재사용)      **관측됨**
      W  매끄러운 램프 (K 재사용)       휘도폭 255 → 측정 가능하지만 **미관측**

    T·U 가 한 쌍으로 「휘도폭 8」의 경계를 고정한다. T 는 동시에 **직전 정의가
    무엇을 틀리게 통과시켰는지**를 고정한다 — 이것이 없으면 정정이 회귀해도 아무도 모른다.

.PARAMETER KeepImages
    생성한 합성 PNG 를 지우지 않는다 (눈으로 확인할 때).

.OUTPUTS
    케이스별 기댓값 vs 실측 표. 전부 일치하면 exit 0, 아니면 exit 2.
#>

[CmdletBinding()]
param([switch] $KeepImages)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

. (Join-Path $PSScriptRoot 'CaptureMetricsCore.ps1')
Initialize-CaptureMetrics

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("capture-metrics-selftest-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$null = New-Item -ItemType Directory -Path $work -Force

$checks = New-Object System.Collections.ArrayList
$EPS = 1e-6

function Assert-Value {
    param(
        [string] $Case,
        [string] $Metric,
        $Expected,
        $Actual,
        [double] $Tolerance = 0.0
    )
    $ok = $false
    $expText = "$Expected"
    if ($Expected -is [string] -and $Expected -match '^\s*(.+?)\s*\.\.\s*(.+?)\s*$') {
        # "lo..hi" 범위 표기
        $lo = [double]$Matches[1]; $hi = [double]$Matches[2]
        $a = [double]$Actual
        $ok = ($a -ge $lo) -and ($a -le $hi)
        $expText = "{0} ~ {1}" -f $lo, $hi
    } elseif ($Expected -is [double] -or $Tolerance -gt 0) {
        $ok = ([Math]::Abs([double]$Actual - [double]$Expected) -le $Tolerance)
        $expText = "$Expected (±$Tolerance)"
    } else {
        $ok = ("$Actual" -eq "$Expected")
    }
    $null = $checks.Add([pscustomobject]@{
        Case = $Case; Metric = $Metric; Expected = $expText; Actual = "$Actual"; Ok = $ok
    })
}

# ══════════════════════════════════════════════════════════════════════════════
# A — 순수 단색 256×256 회색 128
#     휘도 가중치 합이 1.0 이므로 회색 v 의 휘도는 v 다.
# ══════════════════════════════════════════════════════════════════════════════
$pA = Join-Path $work 'A_solid.png'
[CaptureMetrics.TestImages]::Solid($pA, 256, 256, 128, 128, 128)
$A = [CaptureMetrics.Analyzer]::Analyze($pA, 0.5, 0.0, 0)   # 주사선 = 전체 폭

Assert-Value 'A 단색'  '국소 분산 중앙값 (8×8)'  0.0 $A.LocalStdMedian $EPS
Assert-Value 'A 단색'  '빈 평면 비율 (32×32)'    100.0 $A.EmptyPlanePercent $EPS
Assert-Value 'A 단색'  '평탄 구간 수 (δ≤1)'      1 $A.FlatRunCount
Assert-Value 'A 단색'  '최장 평탄 구간 = 폭'     256 $A.FlatRunLongest
Assert-Value 'A 단색'  '평탄 구간 수 (δ=0)'      1 $A.FlatRunCountEq
Assert-Value 'A 단색'  '최장 평탄 구간 (δ=0)'    256 $A.FlatRunLongestEq
Assert-Value 'A 단색'  '주사선 휘도 폭'          0 $A.ScanSpan
Assert-Value 'A 단색'  '휘도 5퍼센타일'          128 $A.LumP5
Assert-Value 'A 단색'  '휘도 중앙값'             128 $A.LumP50
Assert-Value 'A 단색'  '휘도 95퍼센타일'         128 $A.LumP95
Assert-Value 'A 단색'  '발광 비율'               0.0 $A.GlowPercent $EPS
Assert-Value 'A 단색'  '8×8 블록 수 (256/8)²'    1024 $A.BlockCount8
Assert-Value 'A 단색'  '32×32 블록 수 (256/32)²' 64 $A.BlockCount32
Assert-Value 'A 단색'  '마젠타'                  0 $A.MagentaPixels
Assert-Value 'A 단색'  '금색'                    0 $A.GoldPixels

# ══════════════════════════════════════════════════════════════════════════════
# B — 화소별 랜덤 노이즈 256×256
#     RGB 각각 균등 0..255. 휘도 분산 = (0.2126²+0.7152²+0.0722²)·256²/12 ≈ 3069
#     → 표준편차 ≈ 55.4. 8×8(64표본) 블록 표준편차 중앙값도 그 근처여야 한다.
# ══════════════════════════════════════════════════════════════════════════════
$pB = Join-Path $work 'B_noise.png'
[CaptureMetrics.TestImages]::Noise($pB, 256, 256, 20260802)
$B = [CaptureMetrics.Analyzer]::Analyze($pB, 0.5, 0.0, 0)

Assert-Value 'B 노이즈' '국소 분산 중앙값 (이론 ≈55.4)' '45..65' $B.LocalStdMedian
Assert-Value 'B 노이즈' '빈 평면 비율'                   0.0 $B.EmptyPlanePercent $EPS
Assert-Value 'B 노이즈' '평탄 구간 수 (δ≤1, ≈폭)'        '200..256' $B.FlatRunCount
Assert-Value 'B 노이즈' '최장 평탄 구간 (짧아야)'        '1..10' $B.FlatRunLongest
Assert-Value 'B 노이즈' '평탄 구간 수 (δ=0, ≈폭)'        '240..256' $B.FlatRunCountEq

# ══════════════════════════════════════════════════════════════════════════════
# C — 4단 계단 그라디언트 256×256, 레벨 0/64/128/192, 띠 폭 64px
#     띠 폭 64 는 8 과 32 의 배수라 블록이 경계를 걸치지 않는다 → 분산 0.
# ══════════════════════════════════════════════════════════════════════════════
$pC = Join-Path $work 'C_steps4.png'
[CaptureMetrics.TestImages]::StepGradient($pC, 256, 256, 4, 64)
$C = [CaptureMetrics.Analyzer]::Analyze($pC, 0.5, 0.0, 0)

Assert-Value 'C 4단계단' '평탄 구간 수 (δ≤1)'      4 $C.FlatRunCount
Assert-Value 'C 4단계단' '각 구간 길이 = 폭/4'     64 $C.FlatRunLongest
Assert-Value 'C 4단계단' '평탄 구간 수 (δ=0)'      4 $C.FlatRunCountEq
Assert-Value 'C 4단계단' '최장 평탄 구간 (δ=0)'    64 $C.FlatRunLongestEq
Assert-Value 'C 4단계단' '주사선 휘도 폭 (0..192)' 192 $C.ScanSpan
Assert-Value 'C 4단계단' '국소 분산 중앙값'        0.0 $C.LocalStdMedian $EPS
Assert-Value 'C 4단계단' '빈 평면 비율'            100.0 $C.EmptyPlanePercent $EPS
Assert-Value 'C 4단계단' '휘도 5퍼센타일'          0 $C.LumP5
Assert-Value 'C 4단계단' '휘도 중앙값'             64 $C.LumP50
Assert-Value 'C 4단계단' '휘도 95퍼센타일'         192 $C.LumP95
Assert-Value 'C 4단계단' '발광 비율 (192<200)'     0.0 $C.GlowPercent $EPS

# ══════════════════════════════════════════════════════════════════════════════
# D — 완전 검정 400×400 에 흰 사각 100×80 = 8,000 / 160,000 = 정확히 5%
# ══════════════════════════════════════════════════════════════════════════════
$pD = Join-Path $work 'D_glow5.png'
[CaptureMetrics.TestImages]::BlackWithWhiteRect($pD, 400, 400, 100, 80)
$D = [CaptureMetrics.Analyzer]::Analyze($pD, 0.5, 0.0, 0)

Assert-Value 'D 발광5%' '발광 비율 (8000/160000)' 5.0 $D.GlowPercent 1e-9
Assert-Value 'D 발광5%' '전체 화소'               160000 $D.TotalPixels
Assert-Value 'D 발광5%' '휘도 중앙값 (검정 95%)'  0 $D.LumP50
Assert-Value 'D 발광5%' '마젠타'                  0 $D.MagentaPixels
Assert-Value 'D 발광5%' '금색 (무채색)'           0 $D.GoldPixels

# ══════════════════════════════════════════════════════════════════════════════
# E — 순수 마젠타 128×128 (255,0,255) = 16,384 화소
#     금색 조건은 R−B ≥ 60 이므로 마젠타(R−B = 0)는 금색이 아니다. AND 를 반증한다.
# ══════════════════════════════════════════════════════════════════════════════
$pE = Join-Path $work 'E_magenta.png'
[CaptureMetrics.TestImages]::Solid($pE, 128, 128, 255, 0, 255)
$E = [CaptureMetrics.Analyzer]::Analyze($pE, 0.5, 0.0, 0)

Assert-Value 'E 마젠타' '마젠타 = 전체 화소'   16384 $E.MagentaPixels
Assert-Value 'E 마젠타' '전체 화소'            16384 $E.TotalPixels
Assert-Value 'E 마젠타' '금색 (R−B=0 → 0)'     0 $E.GoldPixels
Assert-Value 'E 마젠타' '휘도 중앙값 (0.2848×255=72.6→73)' 73 $E.LumP50

# ══════════════════════════════════════════════════════════════════════════════
# F — 순수 금색 128×128 (212,175,55): R−B=157 ≥60 · G−B=120 ≥30
#     B=55 이므로 마젠타 조건(B>200)에는 걸리지 않는다.
# ══════════════════════════════════════════════════════════════════════════════
$pF = Join-Path $work 'F_gold.png'
[CaptureMetrics.TestImages]::Solid($pF, 128, 128, 212, 175, 55)
$F = [CaptureMetrics.Analyzer]::Analyze($pF, 0.5, 0.0, 0)

Assert-Value 'F 금색' '금색 = 전체 화소'  16384 $F.GoldPixels
Assert-Value 'F 금색' '마젠타 (B=55 → 0)' 0 $F.MagentaPixels
Assert-Value 'F 금색' '휘도 중앙값 (174.2→174)' 174 $F.LumP50

# ══════════════════════════════════════════════════════════════════════════════
# G — 1단위 계단 0/1/2/3. 경계 휘도차 = 1 → 「≤ 1」이므로 끊기지 않는다.
# ══════════════════════════════════════════════════════════════════════════════
$pG = Join-Path $work 'G_step1.png'
[CaptureMetrics.TestImages]::StepGradient($pG, 256, 256, 4, 1)
$G = [CaptureMetrics.Analyzer]::Analyze($pG, 0.5, 0.0, 0)

Assert-Value 'G 차이1' '평탄 구간 수 (δ≤1, 경계 포함)' 1 $G.FlatRunCount
Assert-Value 'G 차이1' '최장 평탄 구간 (δ≤1)'          256 $G.FlatRunLongest
Assert-Value 'G 차이1' '평탄 구간 수 (δ=0 은 끊는다)'   4 $G.FlatRunCountEq
Assert-Value 'G 차이1' '최장 평탄 구간 (δ=0)'          64 $G.FlatRunLongestEq

# ══════════════════════════════════════════════════════════════════════════════
# H — 2단위 계단 0/2/4/6. 경계 휘도차 = 2 → 끊긴다.
# ══════════════════════════════════════════════════════════════════════════════
$pH = Join-Path $work 'H_step2.png'
[CaptureMetrics.TestImages]::StepGradient($pH, 256, 256, 4, 2)
$H = [CaptureMetrics.Analyzer]::Analyze($pH, 0.5, 0.0, 0)

Assert-Value 'H 차이2' '평탄 구간 수 (δ≤1, 경계 제외)' 4 $H.FlatRunCount
Assert-Value 'H 차이2' '최장 평탄 구간 (δ≤1)'          64 $H.FlatRunLongest
Assert-Value 'H 차이2' '평탄 구간 수 (δ=0)'            4 $H.FlatRunCountEq

# ── δ 파라미터가 실제로 전달되는가 ────────────────────────────────────────────
# G(0/1/2/3) 를 δ=0 으로 지정해 부르면 1차 결과도 4개가 나와야 한다.
$G0 = [CaptureMetrics.Analyzer]::Analyze($pG, 0.5, 0.0, 0, 0)
Assert-Value 'G δ=0 지정' '허용 휘도차'   0 $G0.FlatDeltaUsed
Assert-Value 'G δ=0 지정' '평탄 구간 수'  4 $G0.FlatRunCount
$G3 = [CaptureMetrics.Analyzer]::Analyze($pH, 0.5, 0.0, 0, 3)   # H(0/2/4/6) 을 δ=3 으로
Assert-Value 'H δ=3 지정' '평탄 구간 수'  1 $G3.FlatRunCount

# ── 주사선 파라미터가 실제로 반영되는가 (부분 구간) ──────────────────────────
$C2 = [CaptureMetrics.Analyzer]::Analyze($pC, 0.5, 0.25, 100)   # x 64..163 → 64~127 / 128~163
Assert-Value 'C 부분주사선' '주사선 시작 x'   64 $C2.ScanX0
Assert-Value 'C 부분주사선' '주사선 길이'     100 $C2.ScanLen
Assert-Value 'C 부분주사선' '평탄 구간 수'    2 $C2.FlatRunCount
Assert-Value 'C 부분주사선' '최장 (64..127)'  64 $C2.FlatRunLongest
$C3 = [CaptureMetrics.Analyzer]::Analyze($pC, 0.435, 0.0, 0)
Assert-Value 'C 주사선 y'   'y = round(0.435×256)' 111 $C3.ScanY

# ══════════════════════════════════════════════════════════════════════════════
# G-4 계단·단차 — 「계단은 경계가 있는 평탄면이다」
#
# 계단 = 인접 휘도차 ≤ δ 인 연속 구간 중 길이 ≥ 8px
# 단차 = 인접한 두 계단의 평균 휘도차 절대값 ≥ 4
# 관측 = 계단 ≥ 3 그리고 단차 ≥ 2
#
# 기댓값은 전부 손으로 계산했다. 회색 v 의 휘도는 정확히 v 다(가중치 합 = 1.0).
# ══════════════════════════════════════════════════════════════════════════════
$G4_StepMin  = 8
$G4_BoundMin = 4
$G4_StepsMin = 3
$G4_BoundsMin= 2
function Test-Observed { param($M) return (($M.StepCount -ge $G4_StepsMin) -and ($M.BoundaryCount -ge $G4_BoundsMin)) }

# ── I 완전 무지 주사선 (A 재사용: 256px 전부 회색 128) ────────────────────────
#    구간은 하나뿐이다 → 계단 1 (256px ≥ 8), 비교할 이웃이 없다 → 단차 0.
#    직전 정의(평탄 ≤24개 · 최장 ≥20px)는 이것을 **통과**시켰다. 지금은 아니다.
Assert-Value 'I 무지벽' '계단 수 (구간 1개)'        1 $A.StepCount
Assert-Value 'I 무지벽' '최장 계단 = 폭'            256 $A.StepLongest
Assert-Value 'I 무지벽' '단차 수 (이웃 없음)'       0 $A.BoundaryCount
Assert-Value 'I 무지벽' '계단 관측 (1<3)'           $false (Test-Observed $A)
Assert-Value 'I 무지벽' '계단 최소 길이 반영'       $G4_StepMin $A.StepMinLengthUsed
Assert-Value 'I 무지벽' '단차 임계 반영'            $G4_BoundMin $A.BoundaryMinDeltaUsed

# ── J 4단 계단 (C 재사용: 0/64/128/192, 각 64px) ──────────────────────────────
#    경계 휘도차 64 > δ 라 4구간으로 끊긴다. 각 64px ≥ 8 → 계단 4.
#    평균 0·64·128·192 의 이웃 차 64 ≥ 4 → 단차 3.
Assert-Value 'J 4단계단' '계단 수'              4 $C.StepCount
Assert-Value 'J 4단계단' '최장 계단 (폭/4)'     64 $C.StepLongest
Assert-Value 'J 4단계단' '단차 수 (4단 → 3)'    3 $C.BoundaryCount
Assert-Value 'J 4단계단' '계단 관측 (4≥3, 3≥2)' $true (Test-Observed $C)

# ── K 매끄러운 선형 램프 256px, 화소당 +1 (0..255) ────────────────────────────
#    δ≤1 에서는 인접 차가 정확히 1 이라 끊기지 않는다 → 구간 1개(256px) = 계단 1.
#    δ=0 에서는 화소마다 끊겨 길이 1 짜리 조각 256개 → 8px 미만이므로 계단 0.
#    두 경우 모두 단차 0, 관측 안 됨 — 그라디언트는 계단이 아니다.
$pK = Join-Path $work 'K_ramp.png'
[CaptureMetrics.TestImages]::LinearRamp($pK, 256, 64, 0, 1)
$K  = [CaptureMetrics.Analyzer]::Analyze($pK, 0.5, 0.0, 0)      # δ≤1
$K0 = [CaptureMetrics.Analyzer]::Analyze($pK, 0.5, 0.0, 0, 0)   # δ=0

Assert-Value 'K 그라디언트' '평탄 구간 수 (δ≤1)'      1 $K.FlatRunCount
Assert-Value 'K 그라디언트' '계단 수 (δ≤1, 구간 1개)' 1 $K.StepCount
Assert-Value 'K 그라디언트' '단차 수 (δ≤1)'           0 $K.BoundaryCount
Assert-Value 'K 그라디언트' '계단 관측 (δ≤1)'         $false (Test-Observed $K)
Assert-Value 'K 그라디언트' '평탄 구간 수 (δ=0)'      256 $K0.FlatRunCount
Assert-Value 'K 그라디언트' '계단 수 (δ=0, 전부 1px)' 0 $K0.StepCount
Assert-Value 'K 그라디언트' '단차 수 (δ=0)'           0 $K0.BoundaryCount
Assert-Value 'K 그라디언트' '계단 관측 (δ=0)'         $false (Test-Observed $K0)
Assert-Value 'K 그라디언트' '주사선 휘도 폭 (0..255)' 255 $K.ScanSpan

# ── L 단차가 작은 4단 (H 재사용: 0/2/4/6, 각 64px) ────────────────────────────
#    경계 차 2 > δ=1 이라 4구간으로 나뉜다 → 계단 4.
#    그러나 평균 차가 2 < 4 라 사람 눈에 단차로 보이지 않는다 → 단차 0, 관측 안 됨.
#    「구간은 나뉘었는데 눈에 안 보이는」 경우를 가른다.
Assert-Value 'L 약한단차' '계단 수 (구간은 4개)'   4 $H.StepCount
Assert-Value 'L 약한단차' '단차 수 (2 < 4)'        0 $H.BoundaryCount
Assert-Value 'L 약한단차' '계단 관측 (단차 0)'     $false (Test-Observed $H)

# ── M 4px 폭 64단 (레벨 0,4,8,…,252) ──────────────────────────────────────────
#    경계 차 4 > δ 라 4px 마다 끊긴다. 모든 구간이 8px 미만 → 계단 0.
#    계단 최소 길이 필터가 실제로 동작하는지 고정한다.
$pM = Join-Path $work 'M_step4px.png'
[CaptureMetrics.TestImages]::StepGradient($pM, 256, 64, 64, 4)
$M = [CaptureMetrics.Analyzer]::Analyze($pM, 0.5, 0.0, 0)

Assert-Value 'M 4px계단' '평탄 구간 수 (256/4)'    64 $M.FlatRunCount
Assert-Value 'M 4px계단' '최장 평탄 구간'          4 $M.FlatRunLongest
Assert-Value 'M 4px계단' '계단 수 (4px < 8px)'     0 $M.StepCount
Assert-Value 'M 4px계단' '최장 계단'               0 $M.StepLongest
Assert-Value 'M 4px계단' '단차 수'                 0 $M.BoundaryCount
Assert-Value 'M 4px계단' '계단 관측'               $false (Test-Observed $M)

# ── N 64px(0) + 3px(100) + 64px(8) ────────────────────────────────────────────
#    가운데 3px 조각은 8px 미만이라 계단이 아니다. 채택된 계단은 평균 0 과 8 뿐이고
#    그 차 8 ≥ 4 → 단차 1. 조각을 계단으로 셌다면 단차가 2 가 나온다 — 그것을 가른다.
$pN = Join-Path $work 'N_scrap.png'
$valsN = New-Object byte[] 131
for ($i = 0;  $i -lt 64;  $i++) { $valsN[$i] = 0 }
for ($i = 64; $i -lt 67;  $i++) { $valsN[$i] = 100 }
for ($i = 67; $i -lt 131; $i++) { $valsN[$i] = 8 }
[CaptureMetrics.TestImages]::Columns($pN, 16, $valsN)
$N = [CaptureMetrics.Analyzer]::Analyze($pN, 0.5, 0.0, 0)

Assert-Value 'N 짧은조각' '평탄 구간 수 (64/3/64)'  3 $N.FlatRunCount
Assert-Value 'N 짧은조각' '계단 수 (3px 조각 제외)' 2 $N.StepCount
Assert-Value 'N 짧은조각' '최장 계단'               64 $N.StepLongest
Assert-Value 'N 짧은조각' '단차 수 (|8−0| ≥ 4)'     1 $N.BoundaryCount
Assert-Value 'N 짧은조각' '계단 관측 (2 < 3)'       $false (Test-Observed $N)

# ── 계단·단차 임계가 실제로 전달되는가 ────────────────────────────────────────
# H(0/2/4/6) 를 단차 임계 2 로 다시 읽으면 단차 3 이 나와야 한다 (2 ≥ 2).
$H2 = [CaptureMetrics.Analyzer]::Analyze($pH, 0.5, 0.0, 0, 1, 8, 2)
Assert-Value 'H 단차임계2' '단차 임계 반영'  2 $H2.BoundaryMinDeltaUsed
Assert-Value 'H 단차임계2' '단차 수 (2 ≥ 2)' 3 $H2.BoundaryCount
# M(4px 폭) 을 계단 최소 길이 4 로 다시 읽으면 계단 64 · 단차 63 이 나와야 한다.
$M4 = [CaptureMetrics.Analyzer]::Analyze($pM, 0.5, 0.0, 0, 1, 4, 4)
Assert-Value 'M 최소길이4' '계단 최소 길이 반영' 4 $M4.StepMinLengthUsed
Assert-Value 'M 최소길이4' '계단 수'             64 $M4.StepCount
Assert-Value 'M 최소길이4' '단차 수 (64−1)'      63 $M4.BoundaryCount

# ══════════════════════════════════════════════════════════════════════════════
# G-1b / G-1c — 「텍스처가 잘 보이는가」와 「화면의 몇 %가 텍스처인가」를 가른다
#
# G-1b = 블록 std ≥ 4.0 인 블록만 모은 중앙값 (무지 면을 분모에서 뺀다)
# G-1c = 전체 블록 중 std ≥ 8.0 인 것의 비율(%)
#
# 기댓값은 손으로 계산했다. 8×8 블록이므로 256×256 이미지의 블록 수는 32×32 = 1024 다.
# ══════════════════════════════════════════════════════════════════════════════
$G1b_Min = 4.0
$G1c_Min = 8.0

# ── O 완전 무지 (A 재사용: 256×256 회색 128) ──────────────────────────────────
#    모든 블록의 std 가 정확히 0 이다 → 4.0 을 넘는 블록이 **하나도 없다.**
#    그러므로 G-1b 의 분모가 0 이고 중앙값은 **정의되지 않는다.**
#    0 을 내면 「텍스처가 있는데 평평하다」와 구분이 사라지므로 NaN 으로 내고
#    표시 문자열은 「정의불가」로 고정한다.
Assert-Value 'O 무지' 'G-1a 전체 중앙값'          0.0 $A.LocalStdMedian $EPS
Assert-Value 'O 무지' '텍스처 블록 수 (std≥4)'    0 $A.TexturedBlockCount
Assert-Value 'O 무지' '텍스처 블록 비율'          0.0 $A.TexturedBlockPercent $EPS
Assert-Value 'O 무지' 'G-1b 는 NaN'               $true ([double]::IsNaN($A.TexturedBlockStdMedian))
Assert-Value 'O 무지' 'G-1b 표시 = 정의불가'      '정의불가' (Format-CaptureG1b $A.TexturedBlockStdMedian $A.TexturedBlockCount)
Assert-Value 'O 무지' 'G-1c 선명 블록 비율'       0.0 $A.SharpBlockPercent $EPS
Assert-Value 'O 무지' '선명 블록 수'              0 $A.SharpBlockCount
Assert-Value 'O 무지' 'G-1b 임계 반영'            $G1b_Min $A.G1bBlockStdMin $EPS
Assert-Value 'O 무지' 'G-1c 임계 반영'            $G1c_Min $A.G1cBlockStdMin $EPS
# 히스토그램: std 0 인 블록 1024 개가 전부 첫 칸(0.0~0.5)에 들어간다.
Assert-Value 'O 무지' '히스토그램 첫 칸 = 전 블록' 1024 $A.BlockStdHist[0]
Assert-Value 'O 무지' '히스토그램 칸 폭'          0.5 $A.BlockStdHistBinWidth $EPS

# ── C(4단 계단)도 텍스처가 아니다 ─────────────────────────────────────────────
#    띠 폭 64 는 8 의 배수라 블록이 경계를 걸치지 않는다 → 전 블록 std 0.
#    「계단이 있다」와 「텍스처가 있다」가 다른 축이라는 것을 고정한다.
Assert-Value 'O 4단계단' '텍스처 블록 수'      0 $C.TexturedBlockCount
Assert-Value 'O 4단계단' 'G-1b 표시'           '정의불가' (Format-CaptureG1b $C.TexturedBlockStdMedian $C.TexturedBlockCount)
Assert-Value 'O 4단계단' 'G-1c'                0.0 $C.SharpBlockPercent $EPS

# ── R 전면 노이즈 (B 재사용) — 전 블록이 텍스처면 G-1b ≡ G-1a ─────────────────
#    필터가 아무것도 걸러내지 않으면 두 중앙값은 **같은 표본의 중앙값**이므로
#    정확히 같아야 한다. 필터가 잘못 구현되면 여기서 어긋난다.
Assert-Value 'R 전면노이즈' '텍스처 블록 = 전 블록' 1024 $B.TexturedBlockCount
Assert-Value 'R 전면노이즈' '텍스처 블록 비율'      100.0 $B.TexturedBlockPercent $EPS
Assert-Value 'R 전면노이즈' 'G-1c = 100%'           100.0 $B.SharpBlockPercent $EPS
Assert-Value 'R 전면노이즈' 'G-1b ≡ G-1a'           $true ([Math]::Abs($B.TexturedBlockStdMedian - $B.LocalStdMedian) -le $EPS)
Assert-Value 'R 전면노이즈' 'G-1b 범위 (이론 ≈55.4)' '45..65' $B.TexturedBlockStdMedian

# ── P 좌 1/2 무지 + 우 1/2 노이즈 (256×256, 경계 x=128) ───────────────────────
#    128 은 8 의 배수라 블록이 경계를 걸치지 않는다.
#    블록 열 32개 중 좌 16열 = 512 블록이 std 0, 우 16열 = 512 블록이 std ≈55.
#    G-1a = 1024개의 중앙값 = (정렬 512번째 + 513번째)/2 = (0 + 최소노이즈)/2.
#      64표본 블록 std 의 표본오차 ≈ 55.4/√128 ≈ 4.9 이고 512개 중 최소는
#      평균 −3.2σ ≈ 39.7 근처다 → G-1a ≈ 20. 손계산 밴드 14~30 을 쓴다.
#    G-1b = 노이즈 블록 512개만의 중앙값 → B 와 같은 밴드 45~65.
$pP = Join-Path $work 'P_half_blank.png'
[CaptureMetrics.TestImages]::SolidWithNoiseRight($pP, 256, 256, 128, 128, 20260802)
$P = [CaptureMetrics.Analyzer]::Analyze($pP, 0.5, 0.0, 0)

Assert-Value 'P 반반' '전체 블록 수'              1024 $P.BlockCount8
Assert-Value 'P 반반' '텍스처 블록 수 (우 16열)'  512 $P.TexturedBlockCount
Assert-Value 'P 반반' '텍스처 블록 비율'          50.0 $P.TexturedBlockPercent $EPS
Assert-Value 'P 반반' 'G-1c 선명 블록 비율'       50.0 $P.SharpBlockPercent $EPS
Assert-Value 'P 반반' 'G-1a 는 무지에 눌린다'     '14..30' $P.LocalStdMedian
Assert-Value 'P 반반' 'G-1b 는 노이즈 쪽'         '45..65' $P.TexturedBlockStdMedian
Assert-Value 'P 반반' 'G-1b > G-1a'               $true ($P.TexturedBlockStdMedian -gt $P.LocalStdMedian)

# ── Q 좌 3/4 무지 + 우 1/4 노이즈 (256×256, 경계 x=192) ───────────────────────
#    좌 24열 = 768 블록이 std 0, 우 8열 = 256 블록이 std ≈55.
#    정렬 512·513번째가 **둘 다 무지 블록** 안에 있으므로 G-1a 는 **정확히 0** 이다.
#    텍스처가 화면의 25% 를 실제로 덮고 있는데도 그렇다 — 이것이 §5.1 의 결함이다.
$pQ = Join-Path $work 'Q_quarter_tex.png'
[CaptureMetrics.TestImages]::SolidWithNoiseRight($pQ, 256, 256, 128, 192, 20260802)
$Q = [CaptureMetrics.Analyzer]::Analyze($pQ, 0.5, 0.0, 0)

Assert-Value 'Q 1/4텍스처' 'G-1a = 정확히 0 (768/1024 무지)' 0.0 $Q.LocalStdMedian $EPS
Assert-Value 'Q 1/4텍스처' '텍스처 블록 수 (우 8열)'         256 $Q.TexturedBlockCount
Assert-Value 'Q 1/4텍스처' '텍스처 블록 비율'                25.0 $Q.TexturedBlockPercent $EPS
Assert-Value 'Q 1/4텍스처' 'G-1c 선명 블록 비율'             25.0 $Q.SharpBlockPercent $EPS
Assert-Value 'Q 1/4텍스처' 'G-1b 는 살아 있다'               '45..65' $Q.TexturedBlockStdMedian
Assert-Value 'Q 1/4텍스처' '빈 평면 비율 (32×32, 24/32열)'   75.0 $Q.EmptyPlanePercent $EPS

# ── 임계가 실제로 전달되는가 ──────────────────────────────────────────────────
# Q 를 텍스처 임계 100 으로 다시 읽으면 노이즈 블록(≈55)도 걸러져 0개가 된다.
$Q100 = [CaptureMetrics.Analyzer]::Analyze($pQ, 0.5, 0.0, 0, 1, 8, 4, 100.0, 100.0)
Assert-Value 'Q 임계100' 'G-1b 임계 반영'   100.0 $Q100.G1bBlockStdMin $EPS
Assert-Value 'Q 임계100' '텍스처 블록 0개'  0 $Q100.TexturedBlockCount
Assert-Value 'Q 임계100' 'G-1b 정의불가'    '정의불가' (Format-CaptureG1b $Q100.TexturedBlockStdMedian $Q100.TexturedBlockCount)
# 임계 0 이면 모든 블록이 텍스처로 잡히고 G-1b ≡ G-1a 가 된다.
$Q0 = [CaptureMetrics.Analyzer]::Analyze($pQ, 0.5, 0.0, 0, 1, 8, 4, 0.0, 0.0)
Assert-Value 'Q 임계0' '텍스처 블록 = 전 블록' 1024 $Q0.TexturedBlockCount
Assert-Value 'Q 임계0' 'G-1b ≡ G-1a (=0)'      0.0 $Q0.TexturedBlockStdMedian $EPS

# ══════════════════════════════════════════════════════════════════════════════
# G-4 세 갈래 — 관측됨 / 미관측 / **측정 불가**
#
# 측정 불가 = 주사선 구간의 휘도 동적 범위(max−min) < 8.
# 언릿 면 위의 주사선은 계단을 만들 수 없으므로 「미관측」이 아니다 (§5.4).
# ══════════════════════════════════════════════════════════════════════════════
$G4_MinSpan = 8
function Test-Verdict { param($M) return (Get-CaptureG4Verdict -Metric $M -StepsMin $G4_StepsMin -BoundsMin $G4_BoundsMin -MinSpan $G4_MinSpan) }

Assert-Value 'G4 임계' '측정 가능 최소 휘도폭' 8 $script:CM_Classify.G4_MeasurableMinSpan

# ── S 주사선 완전 단색 (A 재사용) ─────────────────────────────────────────────
Assert-Value 'S 단색주사선' '주사선 휘도폭'   0 $A.ScanSpan
Assert-Value 'S 단색주사선' 'G-4 판정'        'UNMEASURABLE' (Test-Verdict $A)
Assert-Value 'S 단색주사선' '한글 표기'       '측정불가' (Get-CaptureG4VerdictLabel (Test-Verdict $A))

# ── T 휘도폭 5 인데 계단 4 · 단차 3 ───────────────────────────────────────────
#    값: 0×20, 5×20, 0×20, 5×20 (폭 80). 회색 v 의 휘도는 정확히 v 다.
#    δ≤1 에서 경계차 5 > 1 이라 4구간, 각 20px ≥ 8 → 계단 4.
#    평균 0·5·0·5 의 이웃 차 5 ≥ 4 → 단차 3.
#    **직전 두 갈래 판정은 이것을 「관측됨」으로 셌다** (계단 4≥3, 단차 3≥2).
#    그러나 주사선 전체의 휘도폭이 5 뿐이다 — 눈에 보이는 계단이 아니다.
$pT = Join-Path $work 'T_span5.png'
$valsT = New-Object byte[] 80
for ($i = 0;  $i -lt 20; $i++) { $valsT[$i] = 0 }
for ($i = 20; $i -lt 40; $i++) { $valsT[$i] = 5 }
for ($i = 40; $i -lt 60; $i++) { $valsT[$i] = 0 }
for ($i = 60; $i -lt 80; $i++) { $valsT[$i] = 5 }
[CaptureMetrics.TestImages]::Columns($pT, 16, $valsT)
$T = [CaptureMetrics.Analyzer]::Analyze($pT, 0.5, 0.0, 0)

Assert-Value 'T 휘도폭5' '계단 수'                4 $T.StepCount
Assert-Value 'T 휘도폭5' '최장 계단'              20 $T.StepLongest
Assert-Value 'T 휘도폭5' '단차 수'                3 $T.BoundaryCount
Assert-Value 'T 휘도폭5' '주사선 휘도폭'          5 $T.ScanSpan
Assert-Value 'T 휘도폭5' '직전 두 갈래는 관측됨'  $true (Test-Observed $T)
Assert-Value 'T 휘도폭5' 'G-4 판정 = 측정 불가'   'UNMEASURABLE' (Test-Verdict $T)

# ── U 같은 모양인데 휘도폭 8 (경계값) ─────────────────────────────────────────
#    값: 0×20, 8×20, 0×20, 8×20. 휘도폭 8 은 「≥ 8」이므로 측정 가능이다.
#    T 와 U 가 한 쌍으로 임계 8 을 양쪽에서 고정한다.
$pU = Join-Path $work 'U_span8.png'
$valsU = New-Object byte[] 80
for ($i = 0;  $i -lt 20; $i++) { $valsU[$i] = 0 }
for ($i = 20; $i -lt 40; $i++) { $valsU[$i] = 8 }
for ($i = 40; $i -lt 60; $i++) { $valsU[$i] = 0 }
for ($i = 60; $i -lt 80; $i++) { $valsU[$i] = 8 }
[CaptureMetrics.TestImages]::Columns($pU, 16, $valsU)
$U = [CaptureMetrics.Analyzer]::Analyze($pU, 0.5, 0.0, 0)

Assert-Value 'U 휘도폭8' '주사선 휘도폭'        8 $U.ScanSpan
Assert-Value 'U 휘도폭8' '계단 수'              4 $U.StepCount
Assert-Value 'U 휘도폭8' '단차 수'              3 $U.BoundaryCount
Assert-Value 'U 휘도폭8' 'G-4 판정 = 관측됨'    'OBSERVED' (Test-Verdict $U)
Assert-Value 'U 휘도폭8' '한글 표기'            '관측됨' (Get-CaptureG4VerdictLabel (Test-Verdict $U))

# ── V 4단 계단 (C 재사용: 0/64/128/192, 휘도폭 192) ───────────────────────────
Assert-Value 'V 4단계단' '주사선 휘도폭'      192 $C.ScanSpan
Assert-Value 'V 4단계단' 'G-4 판정 = 관측됨'  'OBSERVED' (Test-Verdict $C)

# ── W 매끄러운 램프 (K 재사용: 0..255) — 측정 가능하지만 미관측 ───────────────
#    휘도폭 255 라 측정은 가능하다. 그러나 계단 1 · 단차 0 이므로 계단이 아니다.
#    「측정 불가」와 「미관측」이 다른 것이라는 사실을 고정한다.
Assert-Value 'W 램프' '주사선 휘도폭'          255 $K.ScanSpan
Assert-Value 'W 램프' 'G-4 판정 = 미관측'      'UNOBSERVED' (Test-Verdict $K)
Assert-Value 'W 램프' '한글 표기'              '미관측' (Get-CaptureG4VerdictLabel (Test-Verdict $K))

# ── 나머지 기존 케이스의 세 갈래 판정 ─────────────────────────────────────────
# H(0/2/4/6): 휘도폭 6 < 8 → 측정 불가. 직전에는 「미관측」이었다.
Assert-Value 'L 약한단차' '주사선 휘도폭'      6 $H.ScanSpan
Assert-Value 'L 약한단차' 'G-4 판정'           'UNMEASURABLE' (Test-Verdict $H)
# M(4px 폭 64단): 휘도폭 252 라 측정 가능. 계단 0 → 미관측.
Assert-Value 'M 4px계단' '주사선 휘도폭'       252 $M.ScanSpan
Assert-Value 'M 4px계단' 'G-4 판정'            'UNOBSERVED' (Test-Verdict $M)
# N(0/100/8): 휘도폭 100. 계단 2 < 3 → 미관측.
Assert-Value 'N 짧은조각' '주사선 휘도폭'      100 $N.ScanSpan
Assert-Value 'N 짧은조각' 'G-4 판정'           'UNOBSERVED' (Test-Verdict $N)

# ══════════════════════════════════════════════════════════════════════════════
# 히스토그램 골 탐지 — 임계 4.0 이 실제로 두 집단을 가르는가를 재는 도구 자체의 검사
#
# P(좌 무지 std 0 · 우 노이즈 std ≈55)는 **설계상 완벽한 쌍봉**이다.
# 골은 두 봉우리 사이 어딘가의 빈 칸이어야 한다.
# ══════════════════════════════════════════════════════════════════════════════
$histP = Get-CaptureBlockStdHistogram @($P)
$sumP = 0L; foreach ($v in $histP) { $sumP += $v }
Assert-Value 'P 히스토그램' '총 블록'          1024 $sumP
Assert-Value 'P 히스토그램' '첫 칸 = 무지 512' 512 $histP[0]
$valP = Get-CaptureHistogramValley -Hist $histP -BinWidth 0.5 -SearchMaxBin 130
Assert-Value 'P 히스토그램' '쌍봉으로 판정'    $true $valP.IsBimodal
Assert-Value 'P 히스토그램' '봉우리1 = std 0'  0.0 $valP.Peak1Value $EPS
Assert-Value 'P 히스토그램' '골의 블록 수 0'   0 $valP.ValleyCount

# 단봉(전면 노이즈 B)은 쌍봉이 아니어야 한다 — 골 탐지가 아무 데서나 쌍봉을 만들면 안 된다.
$histB = Get-CaptureBlockStdHistogram @($B)
$valB = Get-CaptureHistogramValley -Hist $histB -BinWidth 0.5 -SearchMaxBin 130
Assert-Value 'B 히스토그램' '단봉 → 쌍봉 아님' $false $valB.IsBimodal

# 여러 장 합산이 실제로 더해지는가.
$histAB = Get-CaptureBlockStdHistogram @($A, $B)
$sumAB = 0L; foreach ($v in $histAB) { $sumAB += $v }
Assert-Value '합산 히스토그램' '총 블록 (1024×2)' 2048 $sumAB
Assert-Value '합산 히스토그램' '첫 칸 ≥ A 의 1024' $true ($histAB[0] -ge 1024)

# ══════════════════════════════════════════════════════════════════════════════
# 보고
# ══════════════════════════════════════════════════════════════════════════════
Write-Output ''
Write-Output '══════════════ capture-metrics 자체 검사 ══════════════'
Write-Output "합성 이미지  $work"
Write-Output ''
$fmt = '{0,-14} {1,-36} {2,-22} {3,-22} {4}'
Write-Output ($fmt -f '케이스','지표','기댓값','실측','판정')
Write-Output ('-' * 108)
foreach ($c in $checks) {
    Write-Output ($fmt -f $c.Case, $c.Metric, $c.Expected, $c.Actual, $(if ($c.Ok) { '일치' } else { '불일치' }))
}
Write-Output ''

$bad = @($checks | Where-Object { -not $_.Ok })
Write-Output ("검사 {0} 건 · 일치 {1} · 불일치 {2}" -f $checks.Count, ($checks.Count - $bad.Count), $bad.Count)

if (-not $KeepImages) { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
else { Write-Output "합성 이미지를 남겼다: $work" }

if ($bad.Count -eq 0) {
    Write-Output ''
    Write-Output 'CAPTURE_METRICS_SELFTEST_PASS'
    exit 0
}

$sb = New-Object System.Text.StringBuilder
$null = $sb.AppendLine('')
$null = $sb.AppendLine('════════ 자체 검사 실패 — 지표 계산기가 틀렸다 ════════')
foreach ($c in $bad) {
    $null = $sb.AppendLine("  [$($c.Case)] $($c.Metric)")
    $null = $sb.AppendLine("      기댓값 $($c.Expected)")
    $null = $sb.AppendLine("      실측   $($c.Actual)")
}
$null = $sb.AppendLine('')
$null = $sb.AppendLine('기댓값은 손으로 계산한 값이다. 도구를 고칠 것 — 기댓값을 고치지 말 것.')
[Console]::Error.WriteLine($sb.ToString())
exit 2

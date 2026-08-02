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
# G-SLOT-A / G-SLOT-B — VISUAL_VERDICT.md §10
#
# ROI = 결과판 아홉 칸의 화면 AABB 합집합. ROI 밖은 세지 않는다.
# A: ① |ΔL| ≥ 25 이진화 ② 장축/단축 ≥ 4 ③ 장축 ≥ ROI 폭의 35% ④ 칸 경계 2개 이상 횡단
# B: R−B ≥ 60 · G−B ≥ 30 · max−min ≥ 55 · R ≥ 120
#
# ── 공통 기하 (전부 손으로 계산했다) ────────────────────────────────────────
#   이미지 320×320 · ROI = 절대 (16,16,288,288) · 배경 회색 60 · 막대 회색 200
#   회색 v 의 휘도는 정확히 v 이므로 |ΔL| = |200−60| = 140 ≥ 25 다.
#   ROI 로컬 칸 경계: x = 96·192 · y = 96·192  (288/3 = 96)
#   ③ 의 기준: 0.35 × ROI 폭 288 = **100.8 px**
#   ROI 로컬 (lx,ly) → 절대 (16+lx, 16+ly)
#
#   「횡단」은 **엄격한 걸침**이다 — minX < 경계 그리고 maxX ≥ 경계.
#   한 칸에 꼭 맞게 들어찬 성분은 횡단 0 이다.
# ══════════════════════════════════════════════════════════════════════════════
$RoiX = 16; $RoiY = 16; $RoiW = 288; $RoiH = 288
$SlotBandMax = 0
$SlotColorMax = 2.0
function Invoke-Slot { param([string]$P, [int]$X = $RoiX, [int]$Y = $RoiY, [int]$W = $RoiW, [int]$H = $RoiH)
    return [CaptureMetrics.Analyzer]::Analyze($P, 0.5, 0.0, 0, 1, 8, 4, 4.0, 8.0, $true, $X, $Y, $W, $H) }
function Get-SlotV { param($M) return (Get-CaptureSlotVerdict -Metric $M -BandMax $SlotBandMax -ColorMaxPct $SlotColorMax) }

# ── Z0 ROI 는 줬는데 막대가 없다 → 띠 **0** (측정 불가가 아니다) ──────────────
#    배경이 균일하므로 |ΔL| = 0 < 25 → 이진화 결과가 비어 있다 → 성분 0 · 띠 0.
#    **이 케이스가 「0」과 「측정 불가」를 가르는 기준점이다.**
$pZ0 = Join-Path $work 'Z0_empty.png'
[CaptureMetrics.TestImages]::RectOnGray($pZ0, 320, 320, 60, 0, 0, 0, 0, 200)
$Z0 = Invoke-Slot $pZ0

Assert-Value 'Z0 빈ROI' 'ROI 를 받았다'           $true $Z0.SlotRoiProvided
Assert-Value 'Z0 빈ROI' 'ROI 화소 (288×288)'      82944 $Z0.SlotRoiPixels
Assert-Value 'Z0 빈ROI' 'ROI 배경 휘도 = 60'      60 $Z0.SlotRoiBackgroundLum
Assert-Value 'Z0 빈ROI' '연결 성분 0'             0 $Z0.SlotComponentCount
Assert-Value 'Z0 빈ROI' '띠 0'                    0 $Z0.SlotBandCount
Assert-Value 'Z0 빈ROI' '표시 = "0" (숫자다)'     '0' (Format-CaptureSlotBands $Z0)
Assert-Value 'Z0 빈ROI' 'G-SLOT 판정 = 통과'      'PASS' (Get-SlotV $Z0)

# ── Z1 가로 막대 하나 → 띠 **1** ─────────────────────────────────────────────
#    ROI 로컬 x 72..215 (144px) · y 135..152 (18px) → 절대 (88,151,144,18)
#    ② 장축 144 / 단축 18 = **8.0** ≥ 4                     ✓
#    ③ 144 ≥ 100.8 (= ROI 폭의 35%)                          ✓
#    ④ 세로 경계 96 (72<96, 215≥96) · 192 (72<192, 215≥192) → 2
#       가로 경계 96 (135<96? 아니다) · 192 (152≥192? 아니다) → 0   합 **2** ≥ 2 ✓
#    → 네 조건 전부 만족 = 띠 1
$pZ1 = Join-Path $work 'Z1_band.png'
[CaptureMetrics.TestImages]::RectOnGray($pZ1, 320, 320, 60, 88, 151, 144, 18, 200)
$Z1 = Invoke-Slot $pZ1

Assert-Value 'Z1 띠1' 'ROI 배경 휘도 = 60'        60 $Z1.SlotRoiBackgroundLum
Assert-Value 'Z1 띠1' '연결 성분 1'               1 $Z1.SlotComponentCount
Assert-Value 'Z1 띠1' '최대 성분 면적 (144×18)'   2592 $Z1.SlotTopArea
Assert-Value 'Z1 띠1' '경계상자 폭'               144 $Z1.SlotTopBboxW
Assert-Value 'Z1 띠1' '경계상자 높이'             18 $Z1.SlotTopBboxH
Assert-Value 'Z1 띠1' '장축'                      144 $Z1.SlotTopMajor
Assert-Value 'Z1 띠1' '단축'                      18 $Z1.SlotTopMinor
Assert-Value 'Z1 띠1' '② 종횡비 = 144/18'         8.0 $Z1.SlotTopRatio 1e-9
Assert-Value 'Z1 띠1' '① 평균 |ΔL| = 200−60'      140.0 $Z1.SlotTopMeanDelta 1e-9
Assert-Value 'Z1 띠1' '④ 횡단 수'                 2 $Z1.SlotTopCrossings
Assert-Value 'Z1 띠1' '② 통과'                    $true $Z1.SlotTopC2
Assert-Value 'Z1 띠1' '③ 통과 (144 ≥ 100.8)'      $true $Z1.SlotTopC3
Assert-Value 'Z1 띠1' '④ 통과'                    $true $Z1.SlotTopC4
Assert-Value 'Z1 띠1' '띠 = 1'                    1 $Z1.SlotBandCount
Assert-Value 'Z1 띠1' 'G-SLOT 판정 = 미달'        'FAIL' (Get-SlotV $Z1)

# ── Z2a 같은 막대를 ROI 폭의 20% 로 줄임 → 띠 0 (③에서 걸린다) ───────────────
#    로컬 x 72..129 (58px = 20.1%) · y 135..141 (7px) → 절대 (88,151,58,7)
#    ② 58/7 = 8.29 ≥ 4                                        ✓
#    ③ 58 < 100.8                                             ✗ ← 여기서 걸린다
#    ④ 세로 96 만 걸침 → 1 < 2                                 ✗
#    ⚠ 가로 막대로 폭 20% 를 만들면 ③ 과 ④ 가 **동시에** 깨진다.
#      한 칸이 ROI 폭의 33.3% 이므로 20% 짜리는 경계선을 최대 하나밖에 못 넘는다.
#      그래서 ③ 만 깨지는 경우를 Z2b 로 따로 만든다.
$pZ2a = Join-Path $work 'Z2a_short.png'
[CaptureMetrics.TestImages]::RectOnGray($pZ2a, 320, 320, 60, 88, 151, 58, 7, 200)
$Z2a = Invoke-Slot $pZ2a

Assert-Value 'Z2a 20%폭' '장축'               58 $Z2a.SlotTopMajor
Assert-Value 'Z2a 20%폭' '단축'               7 $Z2a.SlotTopMinor
Assert-Value 'Z2a 20%폭' '② 통과 (8.29≥4)'   $true $Z2a.SlotTopC2
Assert-Value 'Z2a 20%폭' '③ 실패 (58<100.8)' $false $Z2a.SlotTopC3
Assert-Value 'Z2a 20%폭' '④ 횡단 1개뿐'       1 $Z2a.SlotTopCrossings
Assert-Value 'Z2a 20%폭' '④ 실패'             $false $Z2a.SlotTopC4
Assert-Value 'Z2a 20%폭' '띠 = 0'             0 $Z2a.SlotBandCount
Assert-Value 'Z2a 20%폭' 'G-SLOT 판정 = 통과' 'PASS' (Get-SlotV $Z2a)

# ── Z2b ③ **만** 깨지는 세로 막대 ────────────────────────────────────────────
#    로컬 x 140..151 (12px) · y 94..192 (99px) → 절대 (156,110,12,99)
#    ② 99/12 = 8.25 ≥ 4                                        ✓
#    ③ 99 < 100.8                                              ✗ ← 여기서만 걸린다
#    ④ 가로 경계 96 (94<96, 192≥96) · 192 (94<192, 192≥192) → 2 ✓
#       세로 경계는 x 140..151 이 한 칸 안이라 0
$pZ2b = Join-Path $work 'Z2b_short_only3.png'
[CaptureMetrics.TestImages]::RectOnGray($pZ2b, 320, 320, 60, 156, 110, 12, 99, 200)
$Z2b = Invoke-Slot $pZ2b

Assert-Value 'Z2b ③만실패' '장축'                99 $Z2b.SlotTopMajor
Assert-Value 'Z2b ③만실패' '단축'                12 $Z2b.SlotTopMinor
Assert-Value 'Z2b ③만실패' '② 통과 (8.25≥4)'    $true $Z2b.SlotTopC2
Assert-Value 'Z2b ③만실패' '③ 실패 (99<100.8)'  $false $Z2b.SlotTopC3
Assert-Value 'Z2b ③만실패' '④ 횡단 2 (가로경계)' 2 $Z2b.SlotTopCrossings
Assert-Value 'Z2b ③만실패' '④ 통과'              $true $Z2b.SlotTopC4
Assert-Value 'Z2b ③만실패' '띠 = 0'              0 $Z2b.SlotBandCount

# ── Z3 같은 막대를 종횡비 3 으로 뭉툭하게 → 띠 0 (②에서 걸린다) ──────────────
#    로컬 x 72..215 (144px) · y 120..167 (48px) → 절대 (88,136,144,48)
#    ② 144/48 = **3.0** < 4                                    ✗ ← 여기서만 걸린다
#    ③ 144 ≥ 100.8                                             ✓
#    ④ 세로 96·192 둘 다 걸침 → 2                               ✓
$pZ3 = Join-Path $work 'Z3_blunt.png'
[CaptureMetrics.TestImages]::RectOnGray($pZ3, 320, 320, 60, 88, 136, 144, 48, 200)
$Z3 = Invoke-Slot $pZ3

Assert-Value 'Z3 종횡비3' '장축'                144 $Z3.SlotTopMajor
Assert-Value 'Z3 종횡비3' '단축'                48 $Z3.SlotTopMinor
Assert-Value 'Z3 종횡비3' '② 종횡비 = 144/48'   3.0 $Z3.SlotTopRatio 1e-9
Assert-Value 'Z3 종횡비3' '② 실패 (3 < 4)'      $false $Z3.SlotTopC2
Assert-Value 'Z3 종횡비3' '③ 통과 (144≥100.8)'  $true $Z3.SlotTopC3
Assert-Value 'Z3 종횡비3' '④ 통과 (횡단 2)'     $true $Z3.SlotTopC4
Assert-Value 'Z3 종횡비3' '띠 = 0'              0 $Z3.SlotBandCount

# ── Z4 같은 막대를 칸 하나 안에 넣음 → 띠 0 (④에서 걸린다) ───────────────────
#    한 칸은 ROI 폭의 33.3% 이므로 **정사각 ROI 에서는** 칸 안에 넣으면서 동시에
#    ③(장축 ≥ 폭의 35%)을 만족시키는 것이 기하학적으로 불가능하다.
#    그래서 세로로 긴 ROI 를 쓴다 — 이미지 320×896 · ROI 절대 (16,16,288,864).
#    로컬 칸 경계: x = 96·192 · y = **288·576** (864/3 = 288)
#    막대 로컬 x 140..151 (12px) · y 10..279 (270px) → 절대 (156,26,12,270)
#    ② 270/12 = 22.5 ≥ 4                                       ✓
#    ③ 270 ≥ 100.8 (기준은 ROI **폭** 288 이다)                 ✓
#    ④ 세로 경계 0 (x 가 한 칸 안) · 가로 경계 0 (y 279 < 288)  → **0** < 2 ✗
$pZ4 = Join-Path $work 'Z4_one_cell.png'
[CaptureMetrics.TestImages]::RectOnGray($pZ4, 320, 896, 60, 156, 26, 12, 270, 200)
$Z4 = Invoke-Slot $pZ4 16 16 288 864

Assert-Value 'Z4 한칸안' 'ROI 화소 (288×864)'    248832 $Z4.SlotRoiPixels
Assert-Value 'Z4 한칸안' '장축'                  270 $Z4.SlotTopMajor
Assert-Value 'Z4 한칸안' '단축'                  12 $Z4.SlotTopMinor
Assert-Value 'Z4 한칸안' '② 종횡비 = 270/12'     22.5 $Z4.SlotTopRatio 1e-9
Assert-Value 'Z4 한칸안' '② 통과'                $true $Z4.SlotTopC2
Assert-Value 'Z4 한칸안' '③ 통과 (270≥100.8)'    $true $Z4.SlotTopC3
Assert-Value 'Z4 한칸안' '④ 횡단 0 (칸 하나 안)' 0 $Z4.SlotTopCrossings
Assert-Value 'Z4 한칸안' '④ 실패'                $false $Z4.SlotTopC4
Assert-Value 'Z4 한칸안' '띠 = 0'                0 $Z4.SlotBandCount

# ── Z5 ROI 미지정 → **측정 불가** (0 이 아니다) ──────────────────────────────
#    같은 Z1 이미지다. 띠가 실제로 1개 있는 그림인데도 ROI 가 없으면 도구는
#    숫자를 내지 않는다. **이 구분이 이번 작업의 핵심이다** —
#    ROI 를 추정해 낸 0 은 G-4 가 무지 면을 자동 통과시킨 것과 같은 거짓 그린이다.
$Z5 = [CaptureMetrics.Analyzer]::Analyze($pZ1, 0.5, 0.0, 0)

Assert-Value 'Z5 ROI없음' 'ROI 를 받지 않았다'      $false $Z5.SlotRoiProvided
Assert-Value 'Z5 ROI없음' 'ROI 화소 0'              0 $Z5.SlotRoiPixels
Assert-Value 'Z5 ROI없음' '띠 필드는 0 으로 남는다' 0 $Z5.SlotBandCount
Assert-Value 'Z5 ROI없음' '표시 = 측정불가'         '측정불가' (Format-CaptureSlotBands $Z5)
Assert-Value 'Z5 ROI없음' '색 표시 = 측정불가'      '측정불가' (Format-CaptureSlotColor $Z5)
Assert-Value 'Z5 ROI없음' 'G-SLOT 판정 = 측정불가'  'UNMEASURABLE' (Get-SlotV $Z5)
Assert-Value 'Z5 ROI없음' '한글 표기'               '측정불가' (Get-CaptureSlotVerdictLabel (Get-SlotV $Z5))
# 그리고 그것은 **통과가 아니다** — Z0(진짜 0)과 판정이 갈려야 한다.
Assert-Value 'Z5 ROI없음' '측정불가 ≠ 통과'         $true ((Get-SlotV $Z5) -ne (Get-SlotV $Z0))

# ── ROI 밖은 세지 않는다 (천장등 배제 장치) ──────────────────────────────────
#    Z1 의 띠를 그대로 두고 ROI 만 막대가 없는 쪽으로 옮기면 띠는 0 이어야 한다.
#    ROI 절대 (16,16,288,100) 은 y 16..115 이고 막대는 y 151..168 이라 겹치지 않는다.
$Z1out = Invoke-Slot $pZ1 16 16 288 100
Assert-Value 'ROI 밖' 'ROI 밖의 띠는 세지 않는다' 0 $Z1out.SlotBandCount
Assert-Value 'ROI 밖' '연결 성분도 0'             0 $Z1out.SlotComponentCount

# ══════════════════════════════════════════════════════════════════════════════
# G-SLOT-B 색 조건 — 네 절의 경계를 하나씩 고정한다
#   R−B ≥ 60 · G−B ≥ 30 · max−min ≥ 55 · R ≥ 120
# ══════════════════════════════════════════════════════════════════════════════

# ── ZB1 금색 (212,175,55) 이 ROI 의 정확히 25% ───────────────────────────────
#    R−B=157 ≥60 ✓ · G−B=120 ≥30 ✓ · max−min=212−55=157 ≥55 ✓ · R=212 ≥120 ✓
#    배경 회색 60 은 R−B=0 이라 걸리지 않는다.
#    144×144 = 20,736 / 82,944 = **정확히 25.000%**
$pZB1 = Join-Path $work 'ZB1_gold25.png'
[CaptureMetrics.TestImages]::ColorRectOnGray($pZB1, 320, 320, 60, 16, 16, 144, 144, 212, 175, 55)
$ZB1 = Invoke-Slot $pZB1

Assert-Value 'ZB1 금색25%' '색 화소 수'          20736 $ZB1.SlotColorPixels
Assert-Value 'ZB1 금색25%' 'ROI 안 비율'         25.0 $ZB1.SlotColorPercent 1e-9
Assert-Value 'ZB1 금색25%' 'G-SLOT 판정 = 미달'  'FAIL' (Get-SlotV $ZB1)

# ── ZB2 어두운 따뜻한 색 (100,60,20) — `R ≥ 120` 이 실제로 거르는 것 ─────────
#    R−B=80 ✓ · G−B=40 ✓ · max−min=80 ✓ · R=100 **< 120** ✗ → 0%
#    14차 식(R−B·G−B 두 절)만으로는 이 화소가 걸렸다. 실제로 그것을 빼는 절은
#    채도가 아니라 **밝기 하한**이다.
$pZB2 = Join-Path $work 'ZB2_dark_warm.png'
[CaptureMetrics.TestImages]::ColorRectOnGray($pZB2, 320, 320, 60, 16, 16, 288, 288, 100, 60, 20)
$ZB2 = Invoke-Slot $pZB2

Assert-Value 'ZB2 어두운따뜻' 'R=100 < 120 → 0%' 0.0 $ZB2.SlotColorPercent 1e-9
Assert-Value 'ZB2 어두운따뜻' 'G-SLOT 판정 = 통과' 'PASS' (Get-SlotV $ZB2)

# ── ZB3 네 절의 하한을 정확히 만족 (120,90,60) → ROI 전체 100% ───────────────
#    R−B=60 = 60 ✓ · G−B=30 = 30 ✓ · max−min=60 ≥55 ✓ · R=120 = 120 ✓
$pZB3 = Join-Path $work 'ZB3_edge_pass.png'
[CaptureMetrics.TestImages]::ColorRectOnGray($pZB3, 320, 320, 60, 16, 16, 288, 288, 120, 90, 60)
$ZB3 = Invoke-Slot $pZB3
Assert-Value 'ZB3 경계통과' 'ROI 전체가 색 화소' 100.0 $ZB3.SlotColorPercent 1e-9

# ── ZB4 R 하한 바로 아래 (119,89,59) → 0% ────────────────────────────────────
#    R−B=60 ✓ · G−B=30 ✓ · max−min=60 ✓ · R=119 ✗
$pZB4 = Join-Path $work 'ZB4_edge_R.png'
[CaptureMetrics.TestImages]::ColorRectOnGray($pZB4, 320, 320, 60, 16, 16, 288, 288, 119, 89, 59)
$ZB4 = Invoke-Slot $pZB4
Assert-Value 'ZB4 R하한' 'R=119 → 0%' 0.0 $ZB4.SlotColorPercent 1e-9

# ── ZB5 R−B 하한 바로 아래 (120,89,61) → 0% ──────────────────────────────────
#    R−B=59 ✗ (나머지 셋은 만족한다)
$pZB5 = Join-Path $work 'ZB5_edge_RB.png'
[CaptureMetrics.TestImages]::ColorRectOnGray($pZB5, 320, 320, 60, 16, 16, 288, 288, 120, 89, 61)
$ZB5 = Invoke-Slot $pZB5
Assert-Value 'ZB5 R−B하한' 'R−B=59 → 0%' 0.0 $ZB5.SlotColorPercent 1e-9

# ── ZB6 G−B 하한 바로 아래 (120,89,60) → 0% ──────────────────────────────────
#    R−B=60 ✓ · G−B=29 ✗
$pZB6 = Join-Path $work 'ZB6_edge_GB.png'
[CaptureMetrics.TestImages]::ColorRectOnGray($pZB6, 320, 320, 60, 16, 16, 288, 288, 120, 89, 60)
$ZB6 = Invoke-Slot $pZB6
Assert-Value 'ZB6 G−B하한' 'G−B=29 → 0%' 0.0 $ZB6.SlotColorPercent 1e-9

# ── 채도 절(max−min ≥ 55)은 **아무것도 걸러내지 못한다** ─────────────────────
#    max ≥ R 이고 min ≤ B 이므로 max−min ≥ R−B 다. 따라서 `R−B ≥ 60` 을 통과한
#    화소는 **반드시** max−min ≥ 60 > 55 이다. 즉 반증 케이스를 만들 수 없다 —
#    「채도 조건이 정당한 광원을 살렸다」는 근거는 이 식에는 없고,
#    실제로 어두운 따뜻한 색을 빼는 것은 `R ≥ 120` 이다 (ZB2 가 그것을 보인다).
#    이 부등식이 유지되는 한 채도 절은 무효라는 사실을 검사로 고정한다.
Assert-Value 'ZB 함의' '채도 하한 < R−B 하한 ⇒ 채도절 무효' $true `
    ($script:CM_Classify.SlotB_SaturationMin -lt $script:CM_Classify.SlotB_RminusB)
Assert-Value 'ZB 함의' 'ZB3 의 max−min = 60 (R−B 와 같다)' 60 (120 - 60)

# ══════════════════════════════════════════════════════════════════════════════
# ROI 파싱 — 원점을 틀리면 조용히 다른 자리를 잰다
# ══════════════════════════════════════════════════════════════════════════════
$roiTL = ConvertTo-CaptureRoi -Text '16, 16, 288, 288' -Origin topleft
Assert-Value 'ROI 파싱' 'x' 16 $roiTL.X
Assert-Value 'ROI 파싱' 'y' 16 $roiTL.Y
Assert-Value 'ROI 파싱' 'w' 288 $roiTL.W
Assert-Value 'ROI 파싱' 'h' 288 $roiTL.H
# 아래쪽 원점 (Unity WorldToScreenPoint) → 위쪽 원점: y = H − (y + h)
# H=320 · y=10 · h=288  →  320 − 298 = **22**
$roiBL = ConvertTo-CaptureRoi -Text '16,10,288,288' -Origin bottomleft -ImageHeight 320
Assert-Value 'ROI 파싱' 'bottomleft → topleft y = 320−(10+288)' 22 $roiBL.Y
Assert-Value 'ROI 파싱' 'bottomleft 는 x 를 바꾸지 않는다'      16 $roiBL.X

# ══════════════════════════════════════════════════════════════════════════════
# CSV 칸 수 — **분기마다 어긋나면 열이 통째로 밀린다**
#
# ROI 있는 장과 없는 장이 서로 다른 개수의 칸을 내면, 그 뒤의 모든 열이 한 칸씩
# 밀린 채 저장된다. 이것은 문법 오류가 아니라 **조용히 틀린 숫자**이고,
# 실제로 이 작업 중에 한 번 났다 (측정불가 행의 slotTopC4 칸에 빈평면% 가 들어갔다).
# ══════════════════════════════════════════════════════════════════════════════
$slotHdr = Get-CaptureSlotCsvHeader
Assert-Value 'CSV 칸수' '헤더 칸 수'                    17 $slotHdr.Count
$fldMeasured = @(Format-CaptureSlotCsvFields -Metric $Z1 -Verdict 'FAIL')
$fldUnmeas   = @(Format-CaptureSlotCsvFields -Metric $Z5 -Verdict 'UNMEASURABLE')
Assert-Value 'CSV 칸수' 'ROI 있는 장 = 헤더와 같다'      17 $fldMeasured.Count
Assert-Value 'CSV 칸수' 'ROI 없는 장 = 헤더와 같다'      17 $fldUnmeas.Count
Assert-Value 'CSV 칸수' '두 분기가 서로 같다'            $true ($fldMeasured.Count -eq $fldUnmeas.Count)
Assert-Value 'CSV 칸수' '측정불가 첫 칸'                 'UNMEASURABLE' $fldUnmeas[0]
Assert-Value 'CSV 칸수' '측정불가 roiProvided = 0'       '0' $fldUnmeas[1]
Assert-Value 'CSV 칸수' '측정불가 띠 칸은 **비어 있다**' '' $fldUnmeas[8]
Assert-Value 'CSV 칸수' '측정불가 색 칸은 비어 있다'     '' $fldUnmeas[9]
Assert-Value 'CSV 칸수' 'Z1 띠 칸 = 1'                  '1' $fldMeasured[8]
Assert-Value 'CSV 칸수' 'Z1 roiProvided = 1'            '1' $fldMeasured[1]

function Test-RoiThrows { param([scriptblock] $B) try { & $B | Out-Null; return $false } catch { return $true } }
Assert-Value 'ROI 파싱' '값 3개면 거부'      $true (Test-RoiThrows { ConvertTo-CaptureRoi -Text '1,2,3' })
Assert-Value 'ROI 파싱' '정수가 아니면 거부' $true (Test-RoiThrows { ConvertTo-CaptureRoi -Text 'a,2,3,4' })
Assert-Value 'ROI 파싱' '폭 0 이면 거부'     $true (Test-RoiThrows { ConvertTo-CaptureRoi -Text '1,2,0,4' })
Assert-Value 'ROI 파싱' '높이 음수면 거부'   $true (Test-RoiThrows { ConvertTo-CaptureRoi -Text '1,2,3,-4' })

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

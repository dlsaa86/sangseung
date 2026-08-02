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

<#
.SYNOPSIS
    캡처 PNG 세트를 기계적으로 채점한다 — docs/GRAPHICS_TARGET.md §2 의 측정 축.

.DESCRIPTION
    5점 척도 인상 평가가 아니라 화소를 센다. `.claude/visual-criteria.md` 가 절대 점수를
    금지한 이유가 그것이고, GRAPHICS_TARGET §0 이 이 도구를 지정한 이유도 그것이다.

    재는 것 (전부 GRAPHICS_TARGET §2 에 통과선이 있다):

      G-1  국소 분산    8×8 블록 휘도 표준편차의 중앙값        대표 8장 중앙값 ≥ 12.0
      G-2  휘도 분포    5 / 50 / 95 퍼센타일                   p5 ≤ 24 · p50 36~96 · p95 ≥ 170 · 8/8
      G-3  발광         휘도 ≥ 200 화소 비율                   1.0% ~ 6.0% · 8/8
      G-4  계단 셰이딩  계단(≥8px 평탄) 과 단차(평균차 ≥4)     계단 ≥3 · 단차 ≥2 인 장이 24장 중 ≥ 12장
      G-5  빈 평면      32×32 블록 중 표준편차 < 4 인 비율     대표 8장 중앙값 ≤ 18%
      금색 화소         R−B ≥ 60 그리고 G−B ≥ 30               기록만 (통과선 없음)
      마젠타            R>200 그리고 B>200 그리고 G<80         전 장 0 (셰이더 오류 색)

    G-1 의 텍스처 배선 비율(47개 머티리얼)과 G-6~G-8(포스트 체인·물리·성능)은 PNG 에서
    잴 수 없다. 이 도구는 그것들을 **주장하지 않는다** — 화소에서 나오는 축만 판정한다.

.PARAMETER Set
    캡처 디렉터리. 상대 경로면 프로젝트 루트 기준. 기본값 Captures/TenFloor.

.PARAMETER Root
    프로젝트 루트. 생략하면 CLAUDE_PROJECT_DIR, 그다음 스크립트 위치의 상위.

.PARAMETER ScanYFraction
    G-4 주사선의 y 위치(이미지 높이 비율). 기본 0.435 — GRAPHICS_TARGET §2 G-4.

.PARAMETER ScanXFraction
    G-4 주사선의 x 시작 위치(이미지 폭 비율). 기본 0.10 — 좌벽.

.PARAMETER ScanLength
    G-4 주사선 길이(px). 기본 200. 0 이면 오른쪽 끝까지.

.PARAMETER FlatDelta
    G-4 평탄 판정의 허용 휘도차. 기본 1 (「인접 화소 휘도차 ≤ 1」).
    δ=0 결과는 지정값과 **무관하게 항상 함께** 계산해 표에 나란히 찍는다.
    계단(step) 판정도 이 δ 를 쓴다.

.PARAMETER Json
    판정 결과를 JSON 한 줄로도 낸다.

.PARAMETER NoCsv
    metrics.csv 를 쓰지 않는다.

.OUTPUTS
    콘솔 표 + <세트>/metrics.csv (장별) + <세트>/metrics-gates.csv (축별 판정)
    exit 0 전부 통과 · exit 1 캡처 없음 · exit 2 통과선 미달

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/capture-metrics.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/capture-metrics.ps1 -Set Captures/eyelevel
#>

[CmdletBinding()]
param(
    [string] $Set = 'Captures/TenFloor',
    [string] $Root,
    [double] $ScanYFraction = 0.435,
    [double] $ScanXFraction = 0.10,
    [int]    $ScanLength    = 200,
    [int]    $FlatDelta     = 1,
    [switch] $Json,
    [switch] $NoCsv
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

# ── 루트 결정 (verify-topdown.ps1 과 같은 규칙) ───────────────────────────────
if ([string]::IsNullOrWhiteSpace($Root)) {
    if (-not [string]::IsNullOrWhiteSpace($env:CLAUDE_PROJECT_DIR)) { $Root = $env:CLAUDE_PROJECT_DIR }
    else { $Root = Split-Path -Parent $PSScriptRoot }
}
if (-not (Test-Path (Join-Path $Root 'Assets'))) {
    [Console]::Error.WriteLine("capture-metrics: 프로젝트 루트를 찾지 못했다: $Root")
    exit 2
}

. (Join-Path $PSScriptRoot 'CaptureMetrics\CaptureMetricsCore.ps1')

$setPath = $Set
if (-not [System.IO.Path]::IsPathRooted($setPath)) { $setPath = Join-Path $Root $Set }

# ══════════════════════════════════════════════════════════════════════════════
# 캡처 수집 — 없으면 오류가 아니라 「캡처 없음」이다 (exit 1)
# ══════════════════════════════════════════════════════════════════════════════
if (-not (Test-Path -LiteralPath $setPath)) {
    Write-Output ''
    Write-Output '════════ 캡처 없음 ════════'
    Write-Output "캡처 디렉터리가 없다: $setPath"
    Write-Output '먼저 캡처 하네스를 돌릴 것 (Assets/CaptureHarness/README).'
    exit 1
}

# scaled25 같은 하위 디렉터리는 같은 이름을 다시 담고 있다 — 재귀하지 않는다.
$files = @(Get-ChildItem -LiteralPath $setPath -Filter '*.png' -File -ErrorAction SilentlyContinue |
           Sort-Object Name)
if ($files.Count -eq 0) {
    Write-Output ''
    Write-Output '════════ 캡처 없음 ════════'
    Write-Output "PNG 가 한 장도 없다: $setPath"
    Write-Output '디렉터리는 있으나 비어 있다. 캡처 하네스를 돌릴 것.'
    exit 1
}

Initialize-CaptureMetrics

# 통과선은 코어의 단일 출처를 쓴다. G-4 계단·단차 임계도 여기서 나와 측정에 그대로 들어간다.
$T = $script:CM_Thresholds

# ══════════════════════════════════════════════════════════════════════════════
# 측정
# ══════════════════════════════════════════════════════════════════════════════
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$results = New-Object System.Collections.ArrayList
$readErrors = New-Object System.Collections.ArrayList

foreach ($f in $files) {
    try {
        $m = [CaptureMetrics.Analyzer]::Analyze($f.FullName, $ScanYFraction, $ScanXFraction, $ScanLength,
                                                $FlatDelta, $T.G4_StepMinLength, $T.G4_BoundaryMinDelta)
        $null = $results.Add($m)
    } catch {
        $null = $readErrors.Add("$($f.Name) — $($_.Exception.Message)")
    }
}
$sw.Stop()
$elapsed = $sw.Elapsed.TotalSeconds

if ($results.Count -eq 0) {
    Write-Output ''
    Write-Output '════════ 캡처 없음 ════════'
    Write-Output "PNG 는 $($files.Count) 개 있으나 한 장도 읽지 못했다:"
    foreach ($e in $readErrors) { Write-Output "  · $e" }
    exit 1
}

# ── 장별 판정 ─────────────────────────────────────────────────────────────────
$rows = @($results | ForEach-Object {
    $rep = Test-CaptureRepresentative $_.Name
    $g2 = ($_.LumP5 -le $T.G2_P5Max) -and ($_.LumP95 -ge $T.G2_P95Min) -and
          ($_.LumP50 -ge $T.G2_P50Min) -and ($_.LumP50 -le $T.G2_P50Max)
    $g3 = ($_.GlowPercent -ge $T.G3_GlowMinPct) -and ($_.GlowPercent -le $T.G3_GlowMaxPct)
    # G-4 「계단이 관측된다」 = 계단 ≥ 3개 **그리고** 단차 ≥ 2개.
    # 평탄 구간 수·최장으로는 판정하지 않는다 — 무지 벽이 평탄 1개·최장 200px 로
    # 자동 통과했기 때문이다. 계단은 「경계가 있는 평탄면」이다.
    $g4 = ($_.StepCount -ge $T.G4_StepsMinPerFrame) -and ($_.BoundaryCount -ge $T.G4_BoundsMinPerFrame)
    $g5 = ($_.EmptyPlanePercent -le $T.G5_EmptyPlaneMaxPct)
    $g1 = ($_.LocalStdMedian -ge $T.G1_LocalStdMedian)
    $mg = ($_.MagentaPixels -le $T.MagentaMax)

    $miss = New-Object System.Collections.ArrayList
    if (-not $g1) { $null = $miss.Add('G-1') }
    if (-not $g2) { $null = $miss.Add('G-2') }
    if (-not $g3) { $null = $miss.Add('G-3') }
    if (-not $g4) { $null = $miss.Add('G-4') }
    if (-not $g5) { $null = $miss.Add('G-5') }
    if (-not $mg) { $null = $miss.Add('마젠타') }

    [pscustomobject]@{
        M       = $_
        Rep     = $rep
        G1      = $g1
        G2      = $g2
        G3      = $g3
        G4      = $g4
        G5      = $g5
        Magenta = $mg
        Missing = @($miss)
    }
})

$repRows = @($rows | Where-Object { $_.Rep })

# ══════════════════════════════════════════════════════════════════════════════
# 콘솔 표
# ══════════════════════════════════════════════════════════════════════════════
$sizes = @($results | ForEach-Object { "$($_.Width)x$($_.Height)" } | Select-Object -Unique)
$sample = $results[0]

Write-Output ''
Write-Output '══════════════════════════ 캡처 지표 ══════════════════════════'
Write-Output "세트      $setPath"
Write-Output "장수      $($results.Count) 장 (대표 $($repRows.Count) 장 · 해상도 $($sizes -join ', '))"
Write-Output ("주사선    y={0}px (높이의 {1:P1}) · x {2}..{3} ({4}px) · 평탄 허용차 δ≤{5}" -f `
    $sample.ScanY, $ScanYFraction, $sample.ScanX0, ($sample.ScanX0 + $sample.ScanLen - 1), $sample.ScanLen, $FlatDelta)
Write-Output ("블록      G-1 8×8 {0}개 · G-5 32×32 {1}개 · 휘도 0.2126R+0.7152G+0.0722B" -f `
    $sample.BlockCount8, $sample.BlockCount32)
Write-Output ("측정      {0:F2} 초" -f $elapsed)
if ($readErrors.Count -gt 0) {
    Write-Output "읽기실패  $($readErrors.Count) 장:"
    foreach ($e in $readErrors) { Write-Output "          · $e" }
}
Write-Output ''

$fmt = '{0,-23} {1,-3} {2,7} {3,4} {4,4} {5,4} {6,7} {7,5} {8,5} {9,6} {10,5} {11,6} {12,5} {13,5} {14,8} {15,9} {16,6}  {17}'
Write-Output ($fmt -f '파일','대표','국소분산','L5','L50','L95','발광%','계단수','단차수','평탄수','최장','평탄수0','최장0','휘도폭','빈평면%','금색px','마젠타','미달')
Write-Output ('-' * 166)
foreach ($r in $rows) {
    $m = $r.M
    Write-Output ($fmt -f `
        $m.Name,
        $(if ($r.Rep) { ' *' } else { '' }),
        ('{0:F2}' -f $m.LocalStdMedian),
        $m.LumP5, $m.LumP50, $m.LumP95,
        ('{0:F3}' -f $m.GlowPercent),
        $m.StepCount, $m.BoundaryCount,
        $m.FlatRunCount, $m.FlatRunLongest,
        $m.FlatRunCountEq, $m.FlatRunLongestEq, $m.ScanSpan,
        ('{0:F1}' -f $m.EmptyPlanePercent),
        $m.GoldPixels, $m.MagentaPixels,
        $(if ($r.Missing.Count -eq 0) { '통과' } else { ($r.Missing -join ' ') }))
}
Write-Output ("  계단수 = 길이 ≥ {0}px 인 평탄 구간(δ≤{1}) · 단차수 = 인접 계단 평균 휘도차 ≥ {2} 인 경계" -f `
    $T.G4_StepMinLength, $FlatDelta, $T.G4_BoundaryMinDelta)
Write-Output ("  → 계단 ≥ {0} 그리고 단차 ≥ {1} 이어야 그 장에서 계단이 관측된 것이다 (무지 벽은 계단 1·단차 0)" -f `
    $T.G4_StepsMinPerFrame, $T.G4_BoundsMinPerFrame)
Write-Output ("  평탄수/최장 = 허용차 δ≤{0} · 평탄수0/최장0 = 허용차 δ=0 · 휘도폭 = 주사선 최대−최소 (진단용, 판정 아님)" -f $FlatDelta)
Write-Output ''

# ══════════════════════════════════════════════════════════════════════════════
# 축별 통과선 판정
# ══════════════════════════════════════════════════════════════════════════════
$gates = New-Object System.Collections.ArrayList
function Add-Gate {
    param([string] $Axis, [string] $Name, [string] $Measured, [string] $Line, [bool] $Ok, [string] $Note = '')
    $null = $gates.Add([pscustomobject]@{
        Axis = $Axis; Name = $Name; Measured = $Measured; Line = $Line; Ok = $Ok; Note = $Note
    })
}

$repMissing = ($repRows.Count -lt $script:CM_RepresentativePrefixes.Count)
$repNote = ''
if ($repMissing) {
    $have = @($repRows | ForEach-Object { $_.M.Name.Substring(0, [Math]::Min(2, $_.M.Name.Length)) })
    $lack = @($script:CM_RepresentativePrefixes | Where-Object { $have -notcontains $_ })
    $repNote = "대표 8장 중 $($lack -join ',') 없음 — 이 축은 부분 표본이다"
}

# G-1 — 대표 8장의 국소 분산 중앙값
if ($repRows.Count -gt 0) {
    $v = Get-CaptureMedian @($repRows | ForEach-Object { [double]$_.M.LocalStdMedian })
    Add-Gate 'G-1' '국소 분산 중앙값 (대표)' ('{0:F2}' -f $v) ("≥ {0:F1}" -f $T.G1_LocalStdMedian) `
        (($v -ge $T.G1_LocalStdMedian) -and -not $repMissing) $repNote
} else {
    Add-Gate 'G-1' '국소 분산 중앙값 (대표)' '대표 0장' ("≥ {0:F1}" -f $T.G1_LocalStdMedian) $false '대표 8장을 찾지 못했다'
}

# G-2 — 대표 8장 전부에서 p5·p50·p95 충족
$g2ok = @($repRows | Where-Object { $_.G2 }).Count
Add-Gate 'G-2' '휘도 분포 (대표 전장)' "$g2ok/$($repRows.Count) 장" `
    ("p5 ≤ {0} · p50 {1}~{2} · p95 ≥ {3} · 8/8" -f $T.G2_P5Max, $T.G2_P50Min, $T.G2_P50Max, $T.G2_P95Min) `
    (($g2ok -eq $script:CM_RepresentativePrefixes.Count) -and -not $repMissing) $repNote

# G-3 — 대표 8장 전부에서 발광 1.0~6.0%
$g3ok = @($repRows | Where-Object { $_.G3 }).Count
$g3med = if ($repRows.Count -gt 0) { Get-CaptureMedian @($repRows | ForEach-Object { [double]$_.M.GlowPercent }) } else { [double]::NaN }
Add-Gate 'G-3' '발광 화소 비율 (대표 전장)' ("$g3ok/$($repRows.Count) 장 · 중앙값 {0:F3}%" -f $g3med) `
    ("{0:F1}% ~ {1:F1}% · 8/8" -f $T.G3_GlowMinPct, $T.G3_GlowMaxPct) `
    (($g3ok -eq $script:CM_RepresentativePrefixes.Count) -and -not $repMissing) $repNote

# G-4 — 24장 중 계단이 관측되는 장 수
#
# 계단 = 길이 ≥ 8px 인 평탄 구간, 단차 = 인접 계단의 평균 휘도차 ≥ 4.
# 관측 = 계단 ≥ 3 그리고 단차 ≥ 2. 「평탄면이 하나뿐」인 장은 여기서 걸러진다.
$stair = @($rows | Where-Object { $_.G4 }).Count

# 무지 면 진단 — 계단이 1개 이하이면서 단차가 0 인 장은 사실상 한 값으로 칠해진 벽이다.
# 직전 정의(평탄구간 ≤24 · 최장 ≥20px)는 **이 장들을 전부 통과시켰다.**
$blankFrames = @($rows | Where-Object { $_.M.StepCount -le 1 -and $_.M.BoundaryCount -eq 0 })
$g4Note = ''
if ($blankFrames.Count -gt 0) {
    $g4Note = "계단 ≤1 · 단차 0 인 장 $($blankFrames.Count)/$($rows.Count) — 주사선이 사실상 한 값이다 (무지 면)"
}
Add-Gate 'G-4' '계단이 관측되는 장 (전장)' "$stair/$($rows.Count) 장" `
    ("계단 ≥ {0}개(각 ≥ {1}px) 그리고 단차 ≥ {2}개(평균차 ≥ {3}) 인 장 ≥ {4}" -f `
        $T.G4_StepsMinPerFrame, $T.G4_StepMinLength, $T.G4_BoundsMinPerFrame, $T.G4_BoundaryMinDelta, $T.G4_StairFramesMin) `
    ($stair -ge $T.G4_StairFramesMin) $g4Note

# 계단·단차의 세트 중앙값 — 통과선은 없고 기록만 한다. 「몇 장이 통과했나」만으로는
# 세트가 얼마나 평평한지 보이지 않는다.
$stepMed  = Get-CaptureMedian @($rows | ForEach-Object { [double]$_.M.StepCount })
$boundMed = Get-CaptureMedian @($rows | ForEach-Object { [double]$_.M.BoundaryCount })

# G-5 — 대표 8장의 빈 평면 비율 중앙값
if ($repRows.Count -gt 0) {
    $v5 = Get-CaptureMedian @($repRows | ForEach-Object { [double]$_.M.EmptyPlanePercent })
    $n5 = @($repRows | Where-Object { $_.G5 }).Count
    Add-Gate 'G-5' '빈 평면 비율 (대표 중앙값)' ("{0:F1}% · 개별 통과 {1}/{2}" -f $v5, $n5, $repRows.Count) `
        ("≤ {0:F0}%" -f $T.G5_EmptyPlaneMaxPct) `
        (($v5 -le $T.G5_EmptyPlaneMaxPct) -and -not $repMissing) $repNote
} else {
    Add-Gate 'G-5' '빈 평면 비율 (대표 중앙값)' '대표 0장' ("≤ {0:F0}%" -f $T.G5_EmptyPlaneMaxPct) $false '대표 8장을 찾지 못했다'
}

# 마젠타 — 전 장 0
$mgBad = @($rows | Where-Object { -not $_.Magenta })
$mgTotal = ($results | Measure-Object -Property MagentaPixels -Sum).Sum
Add-Gate '회귀' '마젠타 화소 (셰이더 오류 색)' "$mgTotal px · 검출 $($mgBad.Count)/$($rows.Count) 장" '0 px' `
    ($mgBad.Count -eq 0) $(if ($mgBad.Count -gt 0) { "검출 장: " + (($mgBad | ForEach-Object { $_.M.Name }) -join ', ') } else { '' })

# 금색 — 통과선 없음, 기록만
$goldTotal = ($results | Measure-Object -Property GoldPixels -Sum).Sum
$goldFrames = @($results | Where-Object { $_.GoldPixels -gt 0 }).Count

Write-Output '══════════════════════════ 통과선 판정 ══════════════════════════'
$gfmt = '{0,-5} {1,-28} {2,-34} {3,-42} {4}'
Write-Output ($gfmt -f '축','지표','실측','통과선','판정')
Write-Output ('-' * 132)
foreach ($g in $gates) {
    Write-Output ($gfmt -f $g.Axis, $g.Name, $g.Measured, $g.Line, $(if ($g.Ok) { '통과' } else { '미달' }))
    if ($g.Note) { Write-Output ("      └ {0}" -f $g.Note) }
}
Write-Output ($gfmt -f '기록', '금색 화소 (통과선 없음)', "$goldTotal px · $goldFrames/$($rows.Count) 장", '—', '—')
Write-Output ($gfmt -f '기록', 'G-4 계단·단차 중앙값', ("계단 {0:F1} · 단차 {1:F1}" -f $stepMed, $boundMed), '통과선 없음 (판정은 장 수로 한다)', '—')
Write-Output ''

if ($blankFrames.Count -gt 0) {
    Write-Output '── ⚠ 무지 면 경보 ──'
    Write-Output ("  계단 ≤1 · 단차 0 인 장이 {0}/{1} 장이다 — 주사선 {2}px 이 사실상 한 값이다." -f `
        $blankFrames.Count, $rows.Count, $sample.ScanLen)
    Write-Output ("  해당 장: {0}" -f (($blankFrames | ForEach-Object { $_.M.Name }) -join ', '))
    Write-Output '  직전 G-4 정의(평탄구간 ≤24개 · 최장 ≥20px)는 이 장들을 **전부 통과시켰다.**'
    Write-Output '  무지 벽의 주사선은 평탄 구간 1개 · 최장 200px 이라 두 조건을 여유롭게 만족한다.'
    Write-Output '  계단은 「경계가 있는 평탄면」이므로 지금은 계단 ≥3 · 단차 ≥2 를 함께 요구한다.'
    Write-Output ''
}

Write-Output '── 이 도구가 판정하지 않는 것 ──'
Write-Output '  · G-1 텍스처 배선 비율(47개 머티리얼) — 에셋을 세는 축이라 PNG 에 없다'
Write-Output '  · G-2 좌벽 ΔL(위험 4단계) ≥ 15 — 상태별 캡처 쌍을 비교해야 한다'
Write-Output '  · G-3 발광원 종 수 · G-5 프롭 종 수 — 씬 계수이지 화소 계수가 아니다'
Write-Output '  · G-6 포스트 체인 · G-7 물리 · G-8 GC — 단일 정지 프레임에서 잴 수 없다'
Write-Output ''

# ══════════════════════════════════════════════════════════════════════════════
# CSV
# ══════════════════════════════════════════════════════════════════════════════
$csvPath = Join-Path $setPath 'metrics.csv'
$gatePath = Join-Path $setPath 'metrics-gates.csv'
if (-not $NoCsv) {
    $lines = New-Object System.Collections.ArrayList
    $null = $lines.Add('file,width,height,totalPixels,representative,localStdMedian,lumP5,lumP50,lumP95,glowPct,scanY,scanX0,scanLen,flatDelta,stepMinLength,boundaryMinDelta,stepCount,boundaryCount,stepLongest,flatRunCount,flatRunLongest,flatRunCountEq0,flatRunLongestEq0,scanSpan,emptyPlanePct,goldPixels,magentaPixels,g1,g2,g3,g4,g5,magentaOk,missing')
    foreach ($r in $rows) {
        $m = $r.M
        $null = $lines.Add(('{0},{1},{2},{3},{4},{5:F4},{6},{7},{8},{9:F5},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24:F4},{25},{26},{27},{28},{29},{30},{31},{32},{33}' -f `
            $m.Name, $m.Width, $m.Height, $m.TotalPixels, $(if ($r.Rep) { 1 } else { 0 }),
            $m.LocalStdMedian, $m.LumP5, $m.LumP50, $m.LumP95, $m.GlowPercent,
            $m.ScanY, $m.ScanX0, $m.ScanLen, $m.FlatDeltaUsed,
            $m.StepMinLengthUsed, $m.BoundaryMinDeltaUsed, $m.StepCount, $m.BoundaryCount, $m.StepLongest,
            $m.FlatRunCount, $m.FlatRunLongest,
            $m.FlatRunCountEq, $m.FlatRunLongestEq, $m.ScanSpan,
            $m.EmptyPlanePercent, $m.GoldPixels, $m.MagentaPixels,
            $(if ($r.G1) { 1 } else { 0 }), $(if ($r.G2) { 1 } else { 0 }), $(if ($r.G3) { 1 } else { 0 }),
            $(if ($r.G4) { 1 } else { 0 }), $(if ($r.G5) { 1 } else { 0 }), $(if ($r.Magenta) { 1 } else { 0 }),
            ($r.Missing -join ' ')))
    }
    Write-Utf8Bom $csvPath $lines

    $glines = New-Object System.Collections.ArrayList
    $null = $glines.Add('axis,metric,measured,threshold,verdict,note')
    foreach ($g in $gates) {
        $null = $glines.Add(('{0},"{1}","{2}","{3}",{4},"{5}"' -f `
            $g.Axis, $g.Name, $g.Measured, $g.Line, $(if ($g.Ok) { 'PASS' } else { 'FAIL' }), $g.Note))
    }
    $null = $glines.Add(('기록,"금색 화소","{0} px / {1} 장","—",INFO,""' -f $goldTotal, $goldFrames))
    $null = $glines.Add(('기록,"G-4 계단·단차 중앙값","계단 {0:F1} / 단차 {1:F1}","—",INFO,"계단 ≤1 · 단차 0 인 무지 면 {2}/{3} 장"' -f `
        $stepMed, $boundMed, $blankFrames.Count, $rows.Count))
    Write-Utf8Bom $gatePath $glines

    Write-Output "CSV       $csvPath"
    Write-Output "          $gatePath"
    Write-Output ''
}

# ══════════════════════════════════════════════════════════════════════════════
# 판정
# ══════════════════════════════════════════════════════════════════════════════
$failed = @($gates | Where-Object { -not $_.Ok })

if ($Json) {
    $obj = [pscustomobject]@{
        ok        = ($failed.Count -eq 0)
        set       = $setPath
        frames    = $results.Count
        rep       = $repRows.Count
        seconds   = [Math]::Round($elapsed, 2)
        gates     = @($gates | ForEach-Object { [pscustomobject]@{ axis = $_.Axis; ok = $_.Ok; measured = $_.Measured; threshold = $_.Line } })
        images    = @($rows | ForEach-Object {
                        [pscustomobject]@{
                            file = $_.M.Name; localStdMedian = [Math]::Round($_.M.LocalStdMedian, 4)
                            p5 = $_.M.LumP5; p50 = $_.M.LumP50; p95 = $_.M.LumP95
                            glowPct = [Math]::Round($_.M.GlowPercent, 5)
                            steps = $_.M.StepCount; boundaries = $_.M.BoundaryCount
                            stepLongest = $_.M.StepLongest
                            flatRuns = $_.M.FlatRunCount; flatLongest = $_.M.FlatRunLongest
                            flatRunsEq0 = $_.M.FlatRunCountEq; flatLongestEq0 = $_.M.FlatRunLongestEq
                            scanSpan = $_.M.ScanSpan
                            emptyPlanePct = [Math]::Round($_.M.EmptyPlanePercent, 4)
                            gold = $_.M.GoldPixels; magenta = $_.M.MagentaPixels
                            missing = @($_.Missing)
                        } })
    }
    Write-Output ($obj | ConvertTo-Json -Depth 5 -Compress)
}

if ($failed.Count -eq 0) {
    Write-Output "CAPTURE_METRICS_PASS  ($($results.Count) 장 · $('{0:F2}' -f $elapsed) 초)"
    exit 0
}

$sb = New-Object System.Text.StringBuilder
$null = $sb.AppendLine('')
$null = $sb.AppendLine('════════ 캡처 지표 미달 ════════')
$null = $sb.AppendLine("세트 $setPath · $($results.Count) 장 · $('{0:F2}' -f $elapsed) 초")
$null = $sb.AppendLine("미달 축 $($failed.Count) / $($gates.Count)")
$null = $sb.AppendLine('')
foreach ($g in $failed) {
    $null = $sb.AppendLine("[$($g.Axis)] $($g.Name)")
    $null = $sb.AppendLine("      실측  $($g.Measured)")
    $null = $sb.AppendLine("      통과선 $($g.Line)")
    if ($g.Note) { $null = $sb.AppendLine("      비고  $($g.Note)") }
}
$null = $sb.AppendLine('')
$null = $sb.AppendLine('통과선의 출처는 docs/GRAPHICS_TARGET.md §2 다. 숫자를 낮추려면 문서를 먼저 고칠 것.')
[Console]::Error.WriteLine($sb.ToString())
exit 2

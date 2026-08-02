<#
.SYNOPSIS
    캡처 PNG 세트를 기계적으로 채점한다 — docs/GRAPHICS_TARGET.md §2 의 측정 축.

.DESCRIPTION
    5점 척도 인상 평가가 아니라 화소를 센다. `.claude/visual-criteria.md` 가 절대 점수를
    금지한 이유가 그것이고, GRAPHICS_TARGET §0 이 이 도구를 지정한 이유도 그것이다.

    재는 것 (전부 GRAPHICS_TARGET §2 에 통과선이 있다):

      G-1a 국소 분산    8×8 블록 휘도 표준편차의 **전체** 중앙값 대표 8장 중앙값 ≥ 12.0
      G-1b 텍스처 블록  std ≥ 4.0 인 블록만의 중앙값            **통과선 없음 — 기록만**
      G-1c 선명 블록    전체 블록 중 std ≥ 8.0 인 비율(%)       **통과선 없음 — 기록만**
      G-2  휘도 분포    5 / 50 / 95 퍼센타일                   p5 ≤ 24 · p50 36~96 · p95 ≥ 170 · 8/8
      G-3  발광         휘도 ≥ 200 화소 비율                   1.0% ~ 6.0% · 8/8
      G-4  계단 셰이딩  관측됨 / 미관측 / **측정 불가** 세 갈래  관측됨이 24장 중 ≥ 12장
      G-5  빈 평면      32×32 블록 중 표준편차 < 4 인 비율     대표 8장 중앙값 ≤ 18%
      금색 화소         R−B ≥ 60 그리고 G−B ≥ 30               기록만 (통과선 없음)
      마젠타            R>200 그리고 B>200 그리고 G<80         전 장 0 (셰이더 오류 색)

    G-1 의 텍스처 배선 비율(47개 머티리얼)과 G-6~G-8(포스트 체인·물리·성능)은 PNG 에서
    잴 수 없다. 이 도구는 그것들을 **주장하지 않는다** — 화소에서 나오는 축만 판정한다.

    ── 2026-08-02 축 정정 (GRAPHICS_TARGET §5) ─────────────────────────────────
    전담 조사가 G-1·G-4 가 **다른 질문에 답하고 있었다**는 것을 확정했다.

    ① G-1 은 「텍스처가 잘 보이는가」와 「화면의 몇 %가 텍스처인가」를 섞고 있었다.
       전체 블록의 28.6% 가 무지 표면 위에 있고 그 std 는 ≈1.75 로 구조적으로 고정된다.
       그래서 **화면의 절반 이상이 텍스처 위에 있어야만** 중앙값 12 가 원리적으로 가능하다.
       G-1a 는 회귀 비교용으로 **그대로 두고**, G-1b·G-1c 를 옆에 세운다.
       세 값을 전부 낸다 — 통과선은 실측을 보고 사람이 정한다.

    ② G-4 에는 판정 전제 조건이 빠져 있었다. 주사선이 19장 중 9장에서 Unlit `TubeFrame`
       위에 있고, 조명이 없는 면에서 계단은 **원리적으로** 만들어지지 않는다.
       그래서 주사선 휘도 동적 범위 < 8 인 장은 「미관측」이 아니라 **「측정 불가」**다.
       「고치면 되는 것」과 「이 축으로는 잴 수 없는 것」을 한 숫자에 합치지 않는다.

    ③ G-1 은 **포스트를 끈 세트에서** 재야 한다 (post ON 2.840 vs OFF 2.466).
       포스트가 켜진 세트면 G-1 줄 옆에 경고를 찍는다 — 경고이지 실패가 아니다.

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

.PARAMETER NoHistogram
    블록 std 히스토그램을 찍지 않는다. 기본은 찍는다 —
    G-1b 의 임계 4.0 이 실제로 무지 블록과 텍스처 블록을 가르는지 매번 눈으로 확인해야
    하기 때문이다. 쌍봉이 아니거나 골이 4.0 이 아니면 **임계를 옮길 게 아니라 보고할 일**이다.

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
    [switch] $NoCsv,
    [switch] $NoHistogram
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
# 분류 임계(G-1b·G-1c·G-4 측정 가능)는 통과선이 아니다 — 무엇을 무엇으로 셀 것인가만 정한다.
$K = $script:CM_Classify
$PR = $script:CM_Proposals

# ══════════════════════════════════════════════════════════════════════════════
# 세트 출처 — 「이 수가 어느 상태에서 나왔는가」
#
# manifest.txt 에는 machineFingerprint 는 있어도 **커밋 해시가 없다** (GRAPHICS_TARGET §5.6).
# 그래서 「2.84 가 어느 씬 상태인가」를 파일 mtime 으로 역추적해야 했다.
# 매니페스트를 쓰는 것은 이 도구가 아니므로 여기서 고칠 수 없다. 대신 **측정 결과에는
# 남긴다** — PNG mtime 범위와 측정 시점의 HEAD 를 콘솔과 CSV 양쪽에 찍는다.
# 이것은 캡처 시점의 커밋이 아니라 **측정 시점의 커밋**이다. 둘을 혼동하지 말 것.
# ══════════════════════════════════════════════════════════════════════════════
function Get-GitLine {
    # 읽기 전용 git 질의만 한다 (rev-parse · log -1). 워킹 트리를 건드리지 않는다.
    # ⚠ Windows PowerShell 5.1 은 $ErrorActionPreference='Stop' 아래에서 네이티브 명령의
    #   stderr 를 종료 오류로 승격시킬 수 있다. 그래서 이 함수 안에서만 Continue 로 낮춘다.
    param([string] $RepoRoot, [string[]] $GitArgs)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & git -C $RepoRoot @GitArgs 2>$null
        if ($LASTEXITCODE -ne 0) { return '' }
        return (@($out) -join ' ').Trim()
    } catch {
        return ''
    } finally {
        $ErrorActionPreference = $prev
    }
}
$headSha    = Get-GitLine $Root @('rev-parse', 'HEAD')
$headShort  = Get-GitLine $Root @('rev-parse', '--short', 'HEAD')
$headBranch = Get-GitLine $Root @('rev-parse', '--abbrev-ref', 'HEAD')
$headSubject= Get-GitLine $Root @('log', '-1', '--pretty=%s')
if ([string]::IsNullOrWhiteSpace($headSha)) { $headSha = '(git 없음)'; $headShort = '(git 없음)' }

# ══════════════════════════════════════════════════════════════════════════════
# 측정
# ══════════════════════════════════════════════════════════════════════════════
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$results = New-Object System.Collections.ArrayList
$readErrors = New-Object System.Collections.ArrayList

foreach ($f in $files) {
    try {
        $m = [CaptureMetrics.Analyzer]::Analyze($f.FullName, $ScanYFraction, $ScanXFraction, $ScanLength,
                                                $FlatDelta, $T.G4_StepMinLength, $T.G4_BoundaryMinDelta,
                                                $K.G1b_TexturedBlockStd, $K.G1c_SharpBlockStd)
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

    # ── G-4 세 갈래 (2026-08-02 정정) ────────────────────────────────────────
    # 관측됨    = 측정 가능 그리고 계단 ≥ 3 그리고 단차 ≥ 2
    # 미관측    = 측정 가능한데 계단·단차가 모자란다  → **고치면 되는 것**
    # 측정 불가 = 주사선 휘도폭 < 8                    → **이 축으로는 잴 수 없는 것**
    #
    # 직전 판정은 두 갈래였고, 그래서 조명 없는 면 위를 지나는 9장이 「미관측」으로
    # 세어졌다. 그 둘을 한 숫자에 합치면 개선이 어디서 왔는지 알 수 없다.
    $g4v = Get-CaptureG4Verdict -Metric $_ -StepsMin $T.G4_StepsMinPerFrame `
                                -BoundsMin $T.G4_BoundsMinPerFrame -MinSpan $K.G4_MeasurableMinSpan
    $g4 = ($g4v -eq 'OBSERVED')
    # 직전 두 갈래 정의 — 측정 가능 여부를 묻지 않는다. 회귀 비교용으로만 남긴다.
    $g4legacy = ($_.StepCount -ge $T.G4_StepsMinPerFrame) -and ($_.BoundaryCount -ge $T.G4_BoundsMinPerFrame)

    $g5 = ($_.EmptyPlanePercent -le $T.G5_EmptyPlaneMaxPct)
    $g1 = ($_.LocalStdMedian -ge $T.G1_LocalStdMedian)
    $mg = ($_.MagentaPixels -le $T.MagentaMax)

    $miss = New-Object System.Collections.ArrayList
    if (-not $g1) { $null = $miss.Add('G-1a') }
    if (-not $g2) { $null = $miss.Add('G-2') }
    if (-not $g3) { $null = $miss.Add('G-3') }
    # 미달 목록에서도 둘을 구분한다 — 합치면 정정의 의미가 사라진다.
    if ($g4v -eq 'UNOBSERVED')   { $null = $miss.Add('G-4') }
    if ($g4v -eq 'UNMEASURABLE') { $null = $miss.Add('G-4(불가)') }
    if (-not $g5) { $null = $miss.Add('G-5') }
    if (-not $mg) { $null = $miss.Add('마젠타') }

    [pscustomobject]@{
        M         = $_
        Rep       = $rep
        G1        = $g1
        G2        = $g2
        G3        = $g3
        G4        = $g4
        G4Verdict = $g4v
        G4Legacy  = $g4legacy
        G5        = $g5
        Magenta   = $mg
        Missing   = @($miss)
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

# ── 세트 출처 ─────────────────────────────────────────────────────────────────
# manifest.txt 가 커밋 해시를 적지 않으므로(§5.6) 최소한 여기에는 남긴다.
$mtMin = ($files | Measure-Object -Property LastWriteTime -Minimum).Minimum
$mtMax = ($files | Measure-Object -Property LastWriteTime -Maximum).Maximum
$mtSpanMin = [Math]::Round(($mtMax - $mtMin).TotalMinutes, 1)
Write-Output ("PNG mtime {0:yyyy-MM-dd HH:mm:ss} ~ {1:yyyy-MM-dd HH:mm:ss}  (폭 {2} 분)" -f $mtMin, $mtMax, $mtSpanMin)
Write-Output ("HEAD      {0}  [{1}]  {2}" -f $headShort, $headBranch, $headSubject)
Write-Output '          ↑ 측정 시점의 HEAD 다. **캡처 시점의 커밋이 아니다** —'
Write-Output '            manifest.txt 는 커밋 해시를 적지 않는다 (GRAPHICS_TARGET §5.6).'
$manifestPath = Join-Path $setPath 'manifest.txt'
if (Test-Path -LiteralPath $manifestPath) {
    $mfInfo = Get-Item -LiteralPath $manifestPath
    Write-Output ("manifest  있음 · {0:yyyy-MM-dd HH:mm:ss}" -f $mfInfo.LastWriteTime)
} else {
    Write-Output 'manifest  없음 — 이 세트는 캡처 하네스가 남긴 지문 자체가 없다'
}
Write-Output ''

$fmt = '{0,-23} {1,-3} {2,7} {3,8} {4,7} {5,4} {6,4} {7,4} {8,7} {9,5} {10,5} {11,5} {12,9} {13,8} {14,9} {15,6}  {16}'
Write-Output ($fmt -f '파일','대표','G-1a','G-1b','G-1c%','L5','L50','L95','발광%','계단','단차','휘도폭','G-4','빈평면%','금색px','마젠타','미달')
Write-Output ('-' * 172)
foreach ($r in $rows) {
    $m = $r.M
    Write-Output ($fmt -f `
        $m.Name,
        $(if ($r.Rep) { ' *' } else { '' }),
        ('{0:F2}' -f $m.LocalStdMedian),
        (Format-CaptureG1b $m.TexturedBlockStdMedian $m.TexturedBlockCount),
        ('{0:F1}' -f $m.SharpBlockPercent),
        $m.LumP5, $m.LumP50, $m.LumP95,
        ('{0:F3}' -f $m.GlowPercent),
        $m.StepCount, $m.BoundaryCount, $m.ScanSpan,
        (Get-CaptureG4VerdictLabel $r.G4Verdict),
        ('{0:F1}' -f $m.EmptyPlanePercent),
        $m.GoldPixels, $m.MagentaPixels,
        $(if ($r.Missing.Count -eq 0) { '통과' } else { ($r.Missing -join ' ') }))
}
Write-Output ("  G-1a = 8×8 블록 std 의 **전체** 중앙값 (직전 정의 그대로 — 회귀 비교용)")
Write-Output ("  G-1b = std ≥ {0:F1} 인 블록만의 중앙값 (무지 면을 분모에서 뺀다) · 그런 블록이 0개면 「정의불가」" -f $K.G1b_TexturedBlockStd)
Write-Output ("  G-1c = 전체 블록 중 std ≥ {0:F1} 인 것의 비율(%)" -f $K.G1c_SharpBlockStd)
Write-Output ("  계단 = 길이 ≥ {0}px 인 평탄 구간(δ≤{1}) · 단차 = 인접 계단 평균 휘도차 ≥ {2} 인 경계 · 휘도폭 = 주사선 최대−최소" -f `
    $T.G4_StepMinLength, $FlatDelta, $T.G4_BoundaryMinDelta)
Write-Output ("  G-4  관측됨 = 휘도폭 ≥ {0} 그리고 계단 ≥ {1} 그리고 단차 ≥ {2} · 미관측 = 휘도폭은 있는데 계단·단차 부족" -f `
    $K.G4_MeasurableMinSpan, $T.G4_StepsMinPerFrame, $T.G4_BoundsMinPerFrame)
Write-Output ("       측정불가 = 휘도폭 < {0} — 평평한 언릿 면이거나 단색이라 계단이 **원리적으로** 없다 (§5.4)" -f $K.G4_MeasurableMinSpan)
Write-Output ''

# ── 블록 std 히스토그램 — 임계 4.0 이 실제로 두 집단을 가르는가 ────────────────
if (-not $NoHistogram) {
    Write-Output '────────── 블록 std 히스토그램 (G-1b 임계 검증) ──────────'
    Write-Output ''
    foreach ($scope in @('전장', '대표 8장')) {
        $src = if ($scope -eq '전장') { @($rows | ForEach-Object { $_.M }) } else { @($repRows | ForEach-Object { $_.M }) }
        if ($src.Count -eq 0) { continue }
        $hist = Get-CaptureBlockStdHistogram $src
        Write-Output ("[{0}] {1} 장" -f $scope, $src.Count)
        foreach ($ln in (Format-CaptureBlockStdHistogram -Hist $hist -BinWidth 0.5 -DetailMaxBin 24 `
                            -Marks @($K.G1b_TexturedBlockStd, $K.G1c_SharpBlockStd))) { Write-Output $ln }
        $valley = Get-CaptureHistogramValley -Hist $hist -BinWidth 0.5 -SearchMaxBin 40
        if ($null -ne $valley) {
            Write-Output ("  봉우리1 std {0:F1} ({1:N0} 블록) · 봉우리2 std {2:F1} ({3:N0} 블록) · 골 std {4:F1} ({5:N0} 블록)" -f `
                $valley.Peak1Value, $valley.Peak1Count, $valley.Peak2Value, $valley.Peak2Count, `
                $valley.ValleyValue, $valley.ValleyCount)
            if (-not $valley.IsBimodal) {
                Write-Output '  ⚠ 쌍봉이 아니다 — 임계 4.0 이 「두 집단을 가르는 선」이라는 전제가 이 세트에서는 성립하지 않는다.'
                Write-Output '    임계를 옮기지 말고 이 사실을 보고할 것. 임계를 결과에 맞추면 그 순간 근거가 사라진다.'
            } elseif ([Math]::Abs($valley.ValleyValue - $K.G1b_TexturedBlockStd) -gt 1.0) {
                Write-Output ("  ⚠ 골이 {0:F1} 인데 임계는 {1:F1} 이다 — 둘이 1.0 이상 어긋난다. 보고할 것 (임의로 옮기지 말 것)." -f `
                    $valley.ValleyValue, $K.G1b_TexturedBlockStd)
            }
        }
        Write-Output ''
    }
}

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

# ── 포스트 세트 경고 (GRAPHICS_TARGET §5.3) ──────────────────────────────────
# 실측 post ON 2.840 vs post OFF 2.466 — FilmGrain 이 G-1 을 부풀린다.
# 포스트를 켠 세트에서 G-1 을 판정하면 **그레인에 상을 주는 것**이다.
# 경고이지 실패가 아니다 — exit code 를 바꾸지 않는다.
$isNoPostSet = ($setPath -match '(?i)nopost')
$g1PostWarn = ''
if (-not $isNoPostSet) {
    $g1PostWarn = '⚠ 포스트가 켜진 세트다. FilmGrain 이 G-1 을 부풀린다 — 판정은 NoPost 세트에서 하라'
}

# G-1a — 대표 8장의 국소 분산 **전체** 중앙값 (직전 정의 그대로. 통과선 12.0 유지)
if ($repRows.Count -gt 0) {
    $v = Get-CaptureMedian @($repRows | ForEach-Object { [double]$_.M.LocalStdMedian })
    $g1Note = $repNote
    if ($g1PostWarn) { $g1Note = $(if ($g1Note) { "$g1Note / $g1PostWarn" } else { $g1PostWarn }) }
    Add-Gate 'G-1a' '국소 분산 중앙값 (대표·전체 블록)' ('{0:F2}' -f $v) ("≥ {0:F1}" -f $T.G1_LocalStdMedian) `
        (($v -ge $T.G1_LocalStdMedian) -and -not $repMissing) $g1Note
} else {
    Add-Gate 'G-1a' '국소 분산 중앙값 (대표·전체 블록)' '대표 0장' ("≥ {0:F1}" -f $T.G1_LocalStdMedian) $false '대표 8장을 찾지 못했다'
}

# ── G-1b · G-1c — **통과선이 없다.** 기록만 한다 ──────────────────────────────
# 「통과선은 실측을 보고 사람이 정한다」. 지금 숫자를 박아 넣으면 또 근거 없는 숫자가 된다.
# 그러므로 $gates 에 넣지 않는다 — 넣으면 exit code 를 바꾼다.
function Get-G1bSetMedian {
    <#
        세트 중앙값을 낼 때 「정의불가」(텍스처 블록 0개) 장은 **분모에서 뺀다.**
        0 으로 채워 넣으면 「텍스처가 아예 없는 장」이 「평평한 장」으로 둔갑한다.
    #>
    param($Rows)
    $vals = @($Rows | Where-Object { $_.M.TexturedBlockCount -gt 0 } |
                      ForEach-Object { [double]$_.M.TexturedBlockStdMedian })
    $undef = @($Rows | Where-Object { $_.M.TexturedBlockCount -le 0 }).Count
    if ($vals.Count -eq 0) {
        return [pscustomobject]@{ Median = [double]::NaN; N = 0; Undefined = $undef }
    }
    return [pscustomobject]@{ Median = (Get-CaptureMedian $vals); N = $vals.Count; Undefined = $undef }
}
$g1bAll = Get-G1bSetMedian $rows
$g1bRep = Get-G1bSetMedian $repRows
$g1cAll = Get-CaptureMedian @($rows    | ForEach-Object { [double]$_.M.SharpBlockPercent })
$g1cRep = if ($repRows.Count -gt 0) { Get-CaptureMedian @($repRows | ForEach-Object { [double]$_.M.SharpBlockPercent }) } else { [double]::NaN }
$texPctAll = Get-CaptureMedian @($rows | ForEach-Object { [double]$_.M.TexturedBlockPercent })
$texPctRep = if ($repRows.Count -gt 0) { Get-CaptureMedian @($repRows | ForEach-Object { [double]$_.M.TexturedBlockPercent }) } else { [double]::NaN }

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

# ── G-4 세 갈래 (2026-08-02 정정 — GRAPHICS_TARGET §5.4) ─────────────────────
#
# 관측됨 / 미관측 / 측정 불가. 통과선 자체는 **바꾸지 않았다** — 「관측됨 ≥ 12장」 그대로다.
# 바꾼 것은 「무엇을 미관측으로 셀 것인가」뿐이고, 방향은 더 엄격한 쪽이다.
# 비율 통과선(관측됨/(관측됨+미관측) ≥ 50%)은 **제안만 하고 적용하지 않는다.**
$g4Observed     = @($rows | Where-Object { $_.G4Verdict -eq 'OBSERVED' })
$g4Unobserved   = @($rows | Where-Object { $_.G4Verdict -eq 'UNOBSERVED' })
$g4Unmeasurable = @($rows | Where-Object { $_.G4Verdict -eq 'UNMEASURABLE' })
$stair = $g4Observed.Count
$g4Denom = $g4Observed.Count + $g4Unobserved.Count
$g4Ratio = if ($g4Denom -gt 0) { $g4Observed.Count / [double]$g4Denom } else { [double]::NaN }

# 직전 두 갈래 정의로 세면 몇 장인가 — 정정이 무엇을 바꿨는지 눈에 보이게 남긴다.
$g4LegacyCount = @($rows | Where-Object { $_.G4Legacy }).Count

# 무지 면 진단 — 계단이 1개 이하이면서 단차가 0 인 장은 사실상 한 값으로 칠해진 벽이다.
$blankFrames = @($rows | Where-Object { $_.M.StepCount -le 1 -and $_.M.BoundaryCount -eq 0 })
$g4Note = ("관측됨 {0} · 미관측 {1} · 측정불가 {2}" -f $g4Observed.Count, $g4Unobserved.Count, $g4Unmeasurable.Count)
if ($g4Unmeasurable.Count -gt 0) {
    $g4Note += " — 측정불가는 「고치면 되는 것」이 아니다 (주사선이 휘도폭 <$($K.G4_MeasurableMinSpan) 인 면 위)"
}
Add-Gate 'G-4' '계단이 관측되는 장 (전장)' "$stair/$($rows.Count) 장" `
    ("휘도폭 ≥ {0} 그리고 계단 ≥ {1}개(각 ≥ {2}px) 그리고 단차 ≥ {3}개(평균차 ≥ {4}) 인 장 ≥ {5}" -f `
        $K.G4_MeasurableMinSpan, $T.G4_StepsMinPerFrame, $T.G4_StepMinLength, `
        $T.G4_BoundsMinPerFrame, $T.G4_BoundaryMinDelta, $T.G4_StairFramesMin) `
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
Write-Output ($gfmt -f '기록', 'G-1b 텍스처 블록 중앙값 (대표)', `
    ("{0} · 표본 {1}/{2} 장" -f (Format-CaptureG1b $g1bRep.Median $g1bRep.N), $g1bRep.N, $repRows.Count), `
    '통과선 없음 — 실측을 보고 사람이 정한다', '기록')
Write-Output ($gfmt -f '기록', 'G-1b 텍스처 블록 중앙값 (전장)', `
    ("{0} · 표본 {1}/{2} 장 · 정의불가 {3} 장" -f (Format-CaptureG1b $g1bAll.Median $g1bAll.N), $g1bAll.N, $rows.Count, $g1bAll.Undefined), `
    '통과선 없음', '기록')
Write-Output ($gfmt -f '기록', 'G-1c 선명 블록 비율 (대표/전장)', `
    ("{0:F2}% / {1:F2}%" -f $g1cRep, $g1cAll), ("std ≥ {0:F1} · 통과선 없음" -f $K.G1c_SharpBlockStd), '기록')
Write-Output ($gfmt -f '기록', '텍스처 블록 비율 (대표/전장)', `
    ("{0:F2}% / {1:F2}%" -f $texPctRep, $texPctAll), ("std ≥ {0:F1} · G-1b 의 분모" -f $K.G1b_TexturedBlockStd), '기록')
Write-Output ($gfmt -f '기록', 'G-4 세 갈래 (전장)', `
    ("관측 {0} · 미관측 {1} · 측정불가 {2}" -f $g4Observed.Count, $g4Unobserved.Count, $g4Unmeasurable.Count), `
    '통과선은 「관측 ≥ 12장」 하나뿐이다', '기록')
Write-Output ($gfmt -f '기록', 'G-4 직전 두 갈래로 세면', "$g4LegacyCount/$($rows.Count) 장", `
    '측정 가능 여부를 묻지 않던 정의 (회귀 비교용)', '기록')
Write-Output ($gfmt -f '기록', '금색 화소 (통과선 없음)', "$goldTotal px · $goldFrames/$($rows.Count) 장", '—', '—')
Write-Output ($gfmt -f '기록', 'G-4 계단·단차 중앙값', ("계단 {0:F1} · 단차 {1:F1}" -f $stepMed, $boundMed), '통과선 없음 (판정은 장 수로 한다)', '—')
Write-Output ''

# ── G-1 정정 안내 ─────────────────────────────────────────────────────────────
Write-Output '── G-1 축 정정 (GRAPHICS_TARGET §5.1) ──'
Write-Output '  G-1a 는 「텍스처가 잘 보이는가」가 아니라 「화면의 몇 %가 텍스처인가」를 잰다.'
Write-Output ("  무지 표면 위 블록은 std 가 구조적으로 ≈1.75 에 고정되므로 전체 중앙값은 커버리지에 지배된다.")
Write-Output ("  이 세트의 텍스처 블록 비율은 대표 {0:F1}% · 전장 {1:F1}% 다 — 50%% 미만이면 G-1a 는" -f $texPctRep, $texPctAll)
Write-Output '  텍스처가 아무리 선명해도 무지 블록 값에 눌린다. G-1b 를 함께 볼 것.'
if ($g1PostWarn) {
    Write-Output ''
    Write-Output ('  ' + $g1PostWarn)
    Write-Output '  실측 post ON 2.840 vs post OFF 2.466 (GRAPHICS_TARGET §5.3). 경고이지 실패가 아니다.'
    Write-Output '  대조군은 **같은 커밋·같은 씬**에서 뽑아야 한다 — 구성만 다르고 나머지가 같아야 비교다.'
}
Write-Output ''

# ── G-4 통과선 제안 (적용하지 않는다) ─────────────────────────────────────────
Write-Output '── G-4 통과선 제안 (이 도구는 이 값으로 판정하지 않는다) ──'
Write-Output ("  현행  관측됨 ≥ {0}장 / 전 {1}장  →  실측 {2}장  [{3}]" -f `
    $T.G4_StairFramesMin, $rows.Count, $stair, $(if ($stair -ge $T.G4_StairFramesMin) { '통과' } else { '미달' }))
if ($g4Denom -gt 0) {
    Write-Output ("  제안  관측됨 / (관측됨 + 미관측) ≥ {0:P0}  →  실측 {1}/{2} = {3:P1}  [{4}]" -f `
        $PR.G4_ObservedRatioMin, $g4Observed.Count, $g4Denom, $g4Ratio, `
        $(if ($g4Ratio -ge $PR.G4_ObservedRatioMin) { '충족' } else { '미충족' }))
} else {
    Write-Output '  제안  관측됨 / (관측됨 + 미관측) — 분모가 0 이다 (측정 가능한 장이 없다)'
}
Write-Output ("  근거  측정불가 {0}장을 분모에 넣으면 「고칠 수 없는 것」이 「아직 못 고친 것」으로 세어진다." -f $g4Unmeasurable.Count)
Write-Output '        직전 11/24 는 9장이 측정 불가인 채로 세어진 숫자였다 (§5.4).'
Write-Output '  ⚠ 적용하지 않았다. 통과선 변경은 GRAPHICS_TARGET.md 를 먼저 고치는 사람의 결정이다.'
Write-Output ''

# ── G-4 세 갈래 장 목록 ───────────────────────────────────────────────────────
Write-Output '── G-4 세 갈래 분류 (전장) ──'
Write-Output ("  관측됨   {0,2} 장  {1}" -f $g4Observed.Count,     (($g4Observed     | ForEach-Object { $_.M.Name }) -join ', '))
Write-Output ("  미관측   {0,2} 장  {1}" -f $g4Unobserved.Count,   (($g4Unobserved   | ForEach-Object { $_.M.Name }) -join ', '))
Write-Output ("  측정불가 {0,2} 장  {1}" -f $g4Unmeasurable.Count, (($g4Unmeasurable | ForEach-Object { $_.M.Name }) -join ', '))
Write-Output ("  측정불가 = 주사선 y={0} · x {1}..{2} 의 휘도폭이 {3} 미만이다." -f `
    $sample.ScanY, $sample.ScanX0, ($sample.ScanX0 + $sample.ScanLen - 1), $K.G4_MeasurableMinSpan)
Write-Output '  조명이 없는 면(Unlit)에서 램버트 양자화는 일어나지 않는다 — 계단이 얕은 게 아니라 아예 없다.'
Write-Output ("  참고: _Steps=3 의 한 단은 byte +36.5(방향광) ~ +76.3(점광) 로 단차 임계 {0} 의 9~19배다." -f $T.G4_BoundaryMinDelta)
Write-Output '        즉 임계가 높아서 못 잡는 것이 아니다 (GRAPHICS_TARGET §5.4).'
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
Write-Output '  · G-1b · G-1c — 통과선이 아직 없다. 재기만 하고 판정하지 않는다'
Write-Output '  · G-4 「측정 가능」은 휘도폭 대리 판정이다 — 셰이더가 Unlit 인지는 PNG 로 알 수 없다'
Write-Output '  · 캡처 시점의 커밋 — manifest.txt 가 적지 않는다. 위에 찍은 것은 **측정 시점의** HEAD 다'
Write-Output ''

# ══════════════════════════════════════════════════════════════════════════════
# CSV
# ══════════════════════════════════════════════════════════════════════════════
$csvPath = Join-Path $setPath 'metrics.csv'
$gatePath = Join-Path $setPath 'metrics-gates.csv'
if (-not $NoCsv) {
    $lines = New-Object System.Collections.ArrayList
    # g1aLocalStdMedian 는 직전 열 이름 localStdMedian 을 그대로 둔다 — 기존 비교 스크립트가 깨지지 않게.
    # g1bTexturedStdMedian 은 텍스처 블록이 0개면 **빈 칸**이다 (0 을 쓰면 집계에 섞인다).
    $null = $lines.Add('file,width,height,totalPixels,representative,localStdMedian,g1bTexturedStdMedian,g1bTexturedBlocks,g1bTexturedPct,g1cSharpPct,g1cSharpBlocks,g1bBlockStdMin,g1cBlockStdMin,blockCount8,lumP5,lumP50,lumP95,glowPct,scanY,scanX0,scanLen,flatDelta,stepMinLength,boundaryMinDelta,stepCount,boundaryCount,stepLongest,flatRunCount,flatRunLongest,flatRunCountEq0,flatRunLongestEq0,scanSpan,g4Verdict,g4LegacyObserved,emptyPlanePct,goldPixels,magentaPixels,g1,g2,g3,g4,g5,magentaOk,missing')
    foreach ($r in $rows) {
        $m = $r.M
        $g1bCsv = if ($m.TexturedBlockCount -gt 0) { '{0:F4}' -f $m.TexturedBlockStdMedian } else { '' }
        $null = $lines.Add(('{0},{1},{2},{3},{4},{5:F4},{6},{7},{8:F4},{9:F4},{10},{11:F2},{12:F2},{13},{14},{15},{16},{17:F5},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32},{33},{34:F4},{35},{36},{37},{38},{39},{40},{41},{42},{43}' -f `
            $m.Name, $m.Width, $m.Height, $m.TotalPixels, $(if ($r.Rep) { 1 } else { 0 }),
            $m.LocalStdMedian,
            $g1bCsv, $m.TexturedBlockCount, $m.TexturedBlockPercent,
            $m.SharpBlockPercent, $m.SharpBlockCount,
            $m.G1bBlockStdMin, $m.G1cBlockStdMin, $m.BlockCount8,
            $m.LumP5, $m.LumP50, $m.LumP95, $m.GlowPercent,
            $m.ScanY, $m.ScanX0, $m.ScanLen, $m.FlatDeltaUsed,
            $m.StepMinLengthUsed, $m.BoundaryMinDeltaUsed, $m.StepCount, $m.BoundaryCount, $m.StepLongest,
            $m.FlatRunCount, $m.FlatRunLongest,
            $m.FlatRunCountEq, $m.FlatRunLongestEq, $m.ScanSpan,
            $r.G4Verdict, $(if ($r.G4Legacy) { 1 } else { 0 }),
            $m.EmptyPlanePercent, $m.GoldPixels, $m.MagentaPixels,
            $(if ($r.G1) { 1 } else { 0 }), $(if ($r.G2) { 1 } else { 0 }), $(if ($r.G3) { 1 } else { 0 }),
            $(if ($r.G4) { 1 } else { 0 }), $(if ($r.G5) { 1 } else { 0 }), $(if ($r.Magenta) { 1 } else { 0 }),
            ($r.Missing -join ' ')))
    }
    Write-Utf8Bom $csvPath $lines

    # 블록 std 히스토그램 CSV — 임계가 두 집단을 가르는가를 나중에도 다시 볼 수 있게 남긴다.
    $histPath = Join-Path $setPath 'metrics-blockstd-hist.csv'
    $hlines = New-Object System.Collections.ArrayList
    $null = $hlines.Add('scope,binLow,binHigh,blocks')
    foreach ($scope in @('all', 'representative')) {
        $src = if ($scope -eq 'all') { @($rows | ForEach-Object { $_.M }) } else { @($repRows | ForEach-Object { $_.M }) }
        if ($src.Count -eq 0) { continue }
        $h = Get-CaptureBlockStdHistogram $src
        for ($i = 0; $i -lt $h.Length; $i++) {
            $lo = $i * 0.5
            $hi = if ($i -eq $h.Length - 1) { 'inf' } else { '{0:F1}' -f ($lo + 0.5) }
            $null = $hlines.Add(('{0},{1:F1},{2},{3}' -f $scope, $lo, $hi, $h[$i]))
        }
    }
    Write-Utf8Bom $histPath $hlines

    $glines = New-Object System.Collections.ArrayList
    $null = $glines.Add('axis,metric,measured,threshold,verdict,note')
    foreach ($g in $gates) {
        $null = $glines.Add(('{0},"{1}","{2}","{3}",{4},"{5}"' -f `
            $g.Axis, $g.Name, $g.Measured, $g.Line, $(if ($g.Ok) { 'PASS' } else { 'FAIL' }), $g.Note))
    }
    $null = $glines.Add(('기록,"금색 화소","{0} px / {1} 장","—",INFO,""' -f $goldTotal, $goldFrames))
    $null = $glines.Add(('기록,"G-4 계단·단차 중앙값","계단 {0:F1} / 단차 {1:F1}","—",INFO,"계단 ≤1 · 단차 0 인 무지 면 {2}/{3} 장"' -f `
        $stepMed, $boundMed, $blankFrames.Count, $rows.Count))
    # ── 통과선 없는 기록 축 (G-1b·G-1c) 과 G-4 세 갈래 ────────────────────────
    $null = $glines.Add(('기록,"G-1b 텍스처 블록 중앙값 (대표)","{0} (표본 {1}/{2} 장)","통과선 없음",INFO,"std ≥ {3:F1} 인 블록만"' -f `
        (Format-CaptureG1b $g1bRep.Median $g1bRep.N), $g1bRep.N, $repRows.Count, $K.G1b_TexturedBlockStd))
    $null = $glines.Add(('기록,"G-1b 텍스처 블록 중앙값 (전장)","{0} (표본 {1}/{2} 장, 정의불가 {3})","통과선 없음",INFO,""' -f `
        (Format-CaptureG1b $g1bAll.Median $g1bAll.N), $g1bAll.N, $rows.Count, $g1bAll.Undefined))
    $null = $glines.Add(('기록,"G-1c 선명 블록 비율","대표 {0:F2}% / 전장 {1:F2}%","통과선 없음",INFO,"std ≥ {2:F1}"' -f `
        $g1cRep, $g1cAll, $K.G1c_SharpBlockStd))
    $null = $glines.Add(('기록,"텍스처 블록 비율","대표 {0:F2}% / 전장 {1:F2}%","통과선 없음",INFO,"G-1b 의 분모"' -f `
        $texPctRep, $texPctAll))
    $null = $glines.Add(('기록,"G-4 세 갈래","관측 {0} / 미관측 {1} / 측정불가 {2}","관측 ≥ {3}",INFO,"직전 두 갈래로는 {4} 장"' -f `
        $g4Observed.Count, $g4Unobserved.Count, $g4Unmeasurable.Count, $T.G4_StairFramesMin, $g4LegacyCount))
    $null = $glines.Add(('제안,"G-4 비율 통과선","{0:P1} (= {1}/{2})","관측/(관측+미관측) ≥ {3:P0}",PROPOSED,"적용하지 않았다 — 문서를 먼저 고칠 것"' -f `
        $g4Ratio, $g4Observed.Count, $g4Denom, $PR.G4_ObservedRatioMin))
    # ── 출처 (manifest 에 커밋 해시가 없어서 여기 남긴다) ─────────────────────
    $null = $glines.Add(('출처,"PNG mtime 범위","{0:yyyy-MM-dd HH:mm:ss} ~ {1:yyyy-MM-dd HH:mm:ss}","—",INFO,"폭 {2} 분"' -f `
        $mtMin, $mtMax, $mtSpanMin))
    $null = $glines.Add(('출처,"측정 시점 HEAD","{0}","—",INFO,"{1} / {2}"' -f `
        $headSha, $headBranch, ($headSubject -replace '"', "'")))
    $null = $glines.Add(('출처,"포스트 세트 여부","{0}","—",INFO,"{1}"' -f `
        $(if ($isNoPostSet) { 'NoPost' } else { '포스트 켜짐(추정)' }), `
        $(if ($g1PostWarn) { $g1PostWarn } else { 'G-1 판정에 적합한 세트' })))
    Write-Utf8Bom $gatePath $glines

    Write-Output "CSV       $csvPath"
    Write-Output "          $gatePath"
    Write-Output "          $histPath"
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
        provenance = [pscustomobject]@{
            pngMtimeMin = $mtMin.ToString('s'); pngMtimeMax = $mtMax.ToString('s')
            measuredAtHead = $headSha; branch = $headBranch
            isNoPostSet = $isNoPostSet
            note = 'HEAD 는 측정 시점이다. manifest.txt 는 캡처 시점 커밋을 적지 않는다.'
        }
        g1        = [pscustomobject]@{
            aRepMedian   = $(if ($repRows.Count -gt 0) { [Math]::Round((Get-CaptureMedian @($repRows | ForEach-Object { [double]$_.M.LocalStdMedian })), 4) } else { $null })
            bRepMedian   = $(if ($g1bRep.N -gt 0) { [Math]::Round($g1bRep.Median, 4) } else { $null })
            bRepSamples  = $g1bRep.N
            bAllMedian   = $(if ($g1bAll.N -gt 0) { [Math]::Round($g1bAll.Median, 4) } else { $null })
            bAllSamples  = $g1bAll.N
            cRepPct      = [Math]::Round($g1cRep, 4)
            cAllPct      = [Math]::Round($g1cAll, 4)
            texturedPctRep = [Math]::Round($texPctRep, 4)
            texturedPctAll = [Math]::Round($texPctAll, 4)
            texturedBlockStdMin = $K.G1b_TexturedBlockStd
            sharpBlockStdMin    = $K.G1c_SharpBlockStd
            postWarning  = $g1PostWarn
        }
        g4        = [pscustomobject]@{
            observed = $g4Observed.Count; unobserved = $g4Unobserved.Count; unmeasurable = $g4Unmeasurable.Count
            legacyObserved = $g4LegacyCount
            measurableMinSpan = $K.G4_MeasurableMinSpan
            observedRatio = $(if ($g4Denom -gt 0) { [Math]::Round($g4Ratio, 4) } else { $null })
            proposedRatioMin = $PR.G4_ObservedRatioMin
            proposalApplied = $false
        }
        gates     = @($gates | ForEach-Object { [pscustomobject]@{ axis = $_.Axis; ok = $_.Ok; measured = $_.Measured; threshold = $_.Line } })
        images    = @($rows | ForEach-Object {
                        [pscustomobject]@{
                            file = $_.M.Name; localStdMedian = [Math]::Round($_.M.LocalStdMedian, 4)
                            g1bTexturedStdMedian = $(if ($_.M.TexturedBlockCount -gt 0) { [Math]::Round($_.M.TexturedBlockStdMedian, 4) } else { $null })
                            g1bTexturedBlocks = $_.M.TexturedBlockCount
                            g1bTexturedPct = [Math]::Round($_.M.TexturedBlockPercent, 4)
                            g1cSharpPct = [Math]::Round($_.M.SharpBlockPercent, 4)
                            p5 = $_.M.LumP5; p50 = $_.M.LumP50; p95 = $_.M.LumP95
                            glowPct = [Math]::Round($_.M.GlowPercent, 5)
                            steps = $_.M.StepCount; boundaries = $_.M.BoundaryCount
                            stepLongest = $_.M.StepLongest
                            flatRuns = $_.M.FlatRunCount; flatLongest = $_.M.FlatRunLongest
                            flatRunsEq0 = $_.M.FlatRunCountEq; flatLongestEq0 = $_.M.FlatRunLongestEq
                            scanSpan = $_.M.ScanSpan
                            g4Verdict = $_.G4Verdict; g4LegacyObserved = $_.G4Legacy
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

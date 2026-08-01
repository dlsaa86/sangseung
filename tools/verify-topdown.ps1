<#
.SYNOPSIS
    탑다운 전체 완료 여부를 실제 파일과 결과물로만 판정하는 결정론적 검증기.

.DESCRIPTION
    이 스크립트는 대화 요약, 완료 선언, 커밋 메시지를 근거로 삼지 않는다.
    디스크에 있는 것만 본다 — 백로그의 상태 값, 테스트 산출물, 빌드 리포트,
    캡처 매니페스트, 독립 시각 평가 판정, git 작업 상태.

    성공: stdout 에 TOPDOWN_ALL_PASSES_COMPLETE 한 줄, exit 0.
    실패: stderr 에 남은 항목 전문, exit 2.

    exit 2 를 쓰는 이유: Claude Code 의 Stop hook 은 **exit code 2 일 때만** 종료를
    차단하고 stderr 를 모델에게 돌려준다. 다른 non-zero 는 사용자에게만 보이고
    종료를 막지 못한다. 사람이 직접 돌릴 때도 stderr 는 콘솔에 그대로 나온다.

.PARAMETER Root
    프로젝트 루트. 생략하면 CLAUDE_PROJECT_DIR, 그다음 스크립트 위치의 상위를 쓴다.

.PARAMETER Stats
    백로그 통계만 세어 출력하고 exit 0. 백로그 §6 표를 손으로 맞추지 않기 위한 것이다.

.PARAMETER Json
    판정 결과를 JSON 으로도 출력한다. agent 검증자가 파싱하기 위한 것이다.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-topdown.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-topdown.ps1 -Stats
#>

[CmdletBinding()]
param(
    [string] $Root,
    [switch] $Stats,
    [switch] $Json
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

# ── 루트 결정 ─────────────────────────────────────────────────────────────────
if ([string]::IsNullOrWhiteSpace($Root)) {
    if (-not [string]::IsNullOrWhiteSpace($env:CLAUDE_PROJECT_DIR)) {
        $Root = $env:CLAUDE_PROJECT_DIR
    } else {
        $Root = Split-Path -Parent $PSScriptRoot
    }
}
if (-not (Test-Path (Join-Path $Root 'Assets'))) {
    [Console]::Error.WriteLine("verify-topdown: 프로젝트 루트를 찾지 못했다: $Root")
    exit 2
}

# ── 탈출구 ────────────────────────────────────────────────────────────────────
# 탑다운 작업과 무관한 세션(설정 정비, 문서 작업, 조사)에서 Stop 게이트를 끈다.
# 프로젝트의 기존 관례와 같다: SKIP_SELFTEST_GATE, SKIP_UNITY_GUARD.
#   PowerShell:  $env:SKIP_TOPDOWN_GATE = "1"
#
# -Stats 는 게이트가 아니라 계수기다. 꺼져 있어도 항상 세어야 백로그 표를 맞출 수 있다.
if ($env:SKIP_TOPDOWN_GATE -eq '1' -and -not $Stats) {
    Write-Output 'TOPDOWN_GATE_SKIPPED (SKIP_TOPDOWN_GATE=1)'
    exit 0
}

# ── 경로 상수 ─────────────────────────────────────────────────────────────────
$P = @{
    Backlog    = Join-Path $Root 'docs\TOPDOWN_MASTER_BACKLOG.md'
    Progress   = Join-Path $Root 'docs\runtime\TOPDOWN_PROGRESS.md'
    Pending    = Join-Path $Root 'docs\runtime\PENDING_DECISIONS.md'
    Verdict    = Join-Path $Root 'docs\runtime\VISUAL_VERDICT.md'
    EditMode   = Join-Path $Root 'Logs\editmode_tests.txt'
    PlayMode   = Join-Path $Root 'Logs\tenfloor_playmode.txt'
    Build      = Join-Path $Root 'Logs\build_report.txt'
    Manifest   = Join-Path $Root 'Captures\TenFloor\manifest.txt'
    SelfTest   = Join-Path $Root '.claude\state\last-selftest.txt'
    Assembly   = Join-Path $Root 'Library\ScriptAssemblies\Assembly-CSharp.dll'
    Marker     = Join-Path $Root '.claude\state\topdown-verify.txt'
}
$SourceDirs = @(
    (Join-Path $Root 'Assets\Prototype_Elevator'),
    (Join-Path $Root 'Assets\Editor'),
    (Join-Path $Root 'Assets\CaptureHarness')
)

# ── 실패 수집 ─────────────────────────────────────────────────────────────────
$Failures = New-Object System.Collections.ArrayList
$Notes    = New-Object System.Collections.ArrayList

function Add-Failure {
    param([string] $Check, [string] $Message, [string[]] $Detail)
    $null = $Failures.Add([pscustomobject]@{
        Check   = $Check
        Message = $Message
        Detail  = @($Detail)
    })
}
function Add-Note { param([string] $Message) $null = $Notes.Add($Message) }

function Read-Utf8Lines {
    param([string] $Path)
    if (-not (Test-Path $Path)) { return @() }
    return @(Get-Content -LiteralPath $Path -Encoding UTF8 -ErrorAction SilentlyContinue)
}

# ══════════════════════════════════════════════════════════════════════════════
# C1 — 백로그 파싱
# ══════════════════════════════════════════════════════════════════════════════
# VISIBLE 은 2026-08-01 감사에서 추가됐다 — "씬이나 화면에 보이지만 게임과 연결되지 않음".
# 파일이 있고 오브젝트도 있으니 SKELETON 은 아니고, 규칙과 이어지지 않았으니 CONNECTED 도 아니다.
# 이 구간이 이름 없이 남아 있으면 죽은 연출이 구현으로 계상된다.
$ValidStates = @('NOT_STARTED','SKELETON','VISIBLE','CONNECTED','VERIFIED','DEFERRED','BLOCKED_EXTERNAL')
$Items = New-Object System.Collections.ArrayList
$PassState = @{}

if (-not (Test-Path $P.Backlog)) {
    Add-Failure 'C1 백로그' "docs/TOPDOWN_MASTER_BACKLOG.md 가 없다. 탑다운 구조가 설치되지 않았다."
} else {
    $lines = Read-Utf8Lines $P.Backlog
    $cur = $null
    foreach ($line in $lines) {
        if ($line -match '^###\s+(UP-[A-Z]+-\d+)\s+—\s*(.*)$') {
            if ($null -ne $cur) { $null = $Items.Add($cur) }
            $cur = [pscustomobject]@{
                Id       = $Matches[1]
                Title    = $Matches[2].Trim()
                Class    = ''
                State    = ''
                Evidence = @()
                Problem  = ''
            }
            continue
        }
        if ($null -eq $cur) {
            if ($line -match '^-\s+PASS_([1-4]):\s*([A-Z_]+)\s*$') {
                $PassState[[int]$Matches[1]] = $Matches[2]
            }
            continue
        }
        if ($line -match '^##\s') { $null = $Items.Add($cur); $cur = $null; continue }
        if ($line -match '^-\s+분류:\s*(Required|Deferred|Approval Required)\b') { $cur.Class = $Matches[1]; continue }
        if ($line -match '^-\s+상태:\s*([A-Z_]+)\b')                              { $cur.State = $Matches[1]; continue }
        if ($line -match '^-\s+증거:\s*(.+)$') {
            $raw = $Matches[1]
            if ($raw -notmatch '없음') {
                $paths = [regex]::Matches($raw, '`([^`]+)`') | ForEach-Object { $_.Groups[1].Value }
                $cur.Evidence = @($paths)
            }
            continue
        }
        if ($line -match '^-\s+남은 문제:\s*(.*)$') { $cur.Problem = $Matches[1]; continue }
    }
    if ($null -ne $cur) { $null = $Items.Add($cur) }

    if ($Items.Count -eq 0) {
        Add-Failure 'C1 백로그' "백로그에서 '### UP-XXX-NN — 제목' 형식의 항목을 하나도 찾지 못했다. 형식이 깨졌다."
    }
    $badState = @($Items | Where-Object { $ValidStates -notcontains $_.State })
    if ($badState.Count -gt 0) {
        Add-Failure 'C1 백로그' "상태 값이 규정 외인 항목이 있다 (허용: $($ValidStates -join ', '))" `
            (@($badState | ForEach-Object { "  $($_.Id)  상태='$($_.State)'" }))
    }
}

$Required = @($Items | Where-Object { $_.Class -eq 'Required' })

# ── -Stats 모드: 세기만 하고 끝낸다 ───────────────────────────────────────────
if ($Stats) {
    Write-Output "=== TOPDOWN 백로그 통계 ==="
    Write-Output ("추적 항목        : {0}" -f $Items.Count)
    Write-Output ("Required         : {0}" -f $Required.Count)
    foreach ($s in $ValidStates) {
        $n = @($Required | Where-Object { $_.State -eq $s }).Count
        Write-Output ("  {0,-18}: {1}" -f $s, $n)
    }
    foreach ($k in 1..4) {
        $v = 'NOT_STARTED'
        if ($PassState.ContainsKey($k)) { $v = $PassState[$k] }
        Write-Output ("PASS_{0}           : {1}" -f $k, $v)
    }
    exit 0
}

# ══════════════════════════════════════════════════════════════════════════════
# C2 — Required 항목에 NOT_STARTED / SKELETON / CONNECTED 가 남아 있지 않다
# ══════════════════════════════════════════════════════════════════════════════
$Unfinished = @($Required | Where-Object { @('NOT_STARTED','SKELETON','VISIBLE','CONNECTED') -contains $_.State })
if ($Unfinished.Count -gt 0) {
    $byState = @()
    foreach ($s in @('NOT_STARTED','SKELETON','VISIBLE','CONNECTED')) {
        $g = @($Unfinished | Where-Object { $_.State -eq $s })
        if ($g.Count -gt 0) {
            $byState += "  [$s] $($g.Count)건"
            foreach ($it in $g) { $byState += "      $($it.Id)  $($it.Title)" }
        }
    }
    Add-Failure 'C2 Required 미완료' `
        "Required $($Required.Count)건 중 $($Unfinished.Count)건이 아직 VERIFIED 가 아니다." $byState
}

# Required 인데 DEFERRED/BLOCKED_EXTERNAL 로 빠져나간 것은 별도로 드러낸다.
$Escaped = @($Required | Where-Object { $_.State -eq 'DEFERRED' })
if ($Escaped.Count -gt 0) {
    Add-Failure 'C2b 분류 모순' `
        "Required 로 분류된 항목이 DEFERRED 상태다. 범위 축소는 사용자 결정이다." `
        (@($Escaped | ForEach-Object { "  $($_.Id)  $($_.Title)" }))
}

# ══════════════════════════════════════════════════════════════════════════════
# C3 — Pass 1~4 완료
# ══════════════════════════════════════════════════════════════════════════════
$passIncomplete = @()
foreach ($k in 1..4) {
    $v = 'NOT_STARTED'
    if ($PassState.ContainsKey($k)) { $v = $PassState[$k] }
    if ($v -ne 'COMPLETE') { $passIncomplete += "  PASS_$k : $v" }
}
if ($passIncomplete.Count -gt 0) {
    Add-Failure 'C3 패스 미완료' "네 패스가 모두 COMPLETE 여야 한다." $passIncomplete
}

# ══════════════════════════════════════════════════════════════════════════════
# C4 — Required 항목의 증거 경로가 실제로 존재한다
# ══════════════════════════════════════════════════════════════════════════════
$missingEvidence = @()
$noEvidence = @()
foreach ($it in $Required) {
    if ($it.Evidence.Count -eq 0) {
        $noEvidence += "  $($it.Id)  $($it.Title)"
        continue
    }
    foreach ($rel in $it.Evidence) {
        $full = Join-Path $Root ($rel -replace '/', '\')
        if (-not (Test-Path -LiteralPath $full)) {
            $missingEvidence += "  $($it.Id)  →  $rel"
        }
    }
}
if ($noEvidence.Count -gt 0) {
    Add-Failure 'C4 증거 없음' "Required 항목에 증거 경로가 지정되지 않았다." $noEvidence
}
if ($missingEvidence.Count -gt 0) {
    Add-Failure 'C4 증거 부재' "백로그가 가리키는 증거 파일이 디스크에 없다." $missingEvidence
}

# ══════════════════════════════════════════════════════════════════════════════
# C5 — Unity 컴파일 오류 0
#
#   Unity 를 띄우지 않고 컴파일 성공을 확인하는 방법:
#   컴파일이 성공해야만 Library/ScriptAssemblies/Assembly-CSharp.dll 이 새로 쓰인다.
#   따라서 "가장 최근에 수정된 .cs 가 이 DLL 보다 새것이 아니다" 는 곧
#   "마지막 소스 변경이 실제로 컴파일을 통과했다" 는 뜻이다.
#   자체 검증 마커(fail=0)는 그 어셈블리로 테스트가 실제 실행됐음을 보탠다.
# ══════════════════════════════════════════════════════════════════════════════
$newestSrc = $null
foreach ($d in $SourceDirs) {
    if (-not (Test-Path $d)) { continue }
    $c = Get-ChildItem -LiteralPath $d -Filter *.cs -Recurse -File -ErrorAction SilentlyContinue |
         Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($null -ne $c) {
        if ($null -eq $newestSrc -or $c.LastWriteTimeUtc -gt $newestSrc.LastWriteTimeUtc) { $newestSrc = $c }
    }
}

if (-not (Test-Path $P.Assembly)) {
    Add-Failure 'C5 컴파일' "Library/ScriptAssemblies/Assembly-CSharp.dll 이 없다. 프로젝트가 한 번도 컴파일되지 않았다."
} elseif ($null -ne $newestSrc) {
    $dll = Get-Item -LiteralPath $P.Assembly
    if ($newestSrc.LastWriteTimeUtc -gt $dll.LastWriteTimeUtc) {
        $rel = $newestSrc.FullName.Substring($Root.Length).TrimStart('\')
        Add-Failure 'C5 컴파일' "컴파일 결과보다 새로운 소스가 있다 — 마지막 변경이 컴파일을 통과했다는 증거가 없다." `
            @("  최신 소스 : $rel  ($($newestSrc.LastWriteTime))",
              "  어셈블리  : Assembly-CSharp.dll  ($($dll.LastWriteTime))",
              "  조치      : Unity 에 포커스를 줘 컴파일시킨 뒤 다시 검증할 것")
    }
}

if (-not (Test-Path $P.SelfTest)) {
    Add-Failure 'C5 자체 검증' ".claude/state/last-selftest.txt 가 없다. 'Ascend/Run Self Tests' 를 실행할 것."
} else {
    $st = (Read-Utf8Lines $P.SelfTest) -join ' '
    if ($st -match 'fail=([0-9]+)') {
        if ([int]$Matches[1] -ne 0) {
            Add-Failure 'C5 자체 검증' "마지막 자체 검증이 실패로 기록되어 있다: $($st.Trim())"
        }
    } else {
        Add-Failure 'C5 자체 검증' "자체 검증 마커에서 fail= 을 읽지 못했다: $($st.Trim())"
    }
    if ($null -ne $newestSrc) {
        $marker = Get-Item -LiteralPath $P.SelfTest
        if ($newestSrc.LastWriteTimeUtc -gt $marker.LastWriteTimeUtc) {
            $rel = $newestSrc.FullName.Substring($Root.Length).TrimStart('\')
            Add-Failure 'C5 자체 검증' "자체 검증 이후 수정된 소스가 있다 — 테스트가 현재 코드를 검증하지 않았다." `
                @("  최신 소스 : $rel", "  마지막 검증: $($st.Trim())")
        }
    }
}

# ══════════════════════════════════════════════════════════════════════════════
# C6 — EditMode / PlayMode 결과 파일이 통과 상태
# ══════════════════════════════════════════════════════════════════════════════
if (-not (Test-Path $P.EditMode)) {
    Add-Failure 'C6 EditMode' "Logs/editmode_tests.txt 가 없다."
} else {
    $em = Read-Utf8Lines $P.EditMode
    $sum = @($em | Where-Object { $_ -match '합계:\s*(\d+)\s*PASS\s*/\s*(\d+)\s*FAIL' }) | Select-Object -Last 1
    if ($null -eq $sum) {
        Add-Failure 'C6 EditMode' "editmode_tests.txt 에서 '합계: N PASS / M FAIL' 줄을 찾지 못했다."
    } else {
        $null = $sum -match '합계:\s*(\d+)\s*PASS\s*/\s*(\d+)\s*FAIL'
        $pass = [int]$Matches[1]; $fail = [int]$Matches[2]
        if ($fail -ne 0) { Add-Failure 'C6 EditMode' "EditMode 실패 $fail 건 ($pass PASS)." }
        elseif ($pass -eq 0) { Add-Failure 'C6 EditMode' "EditMode 테스트가 0건이다." }
        else { Add-Note "EditMode $pass PASS / 0 FAIL" }
    }
}

if (-not (Test-Path $P.PlayMode)) {
    Add-Failure 'C6 PlayMode' "Logs/tenfloor_playmode.txt 가 없다."
} else {
    $pm = Read-Utf8Lines $P.PlayMode
    $res = @($pm | Where-Object { $_ -match '결과:\s*(\d+)\s*PASS\s*/\s*(\d+)\s*FAIL\s*/\s*콘솔오류\s*(\d+)' }) | Select-Object -Last 1
    if ($null -eq $res) {
        Add-Failure 'C6 PlayMode' "tenfloor_playmode.txt 에서 '결과: N PASS / M FAIL / 콘솔오류 K건' 줄을 찾지 못했다."
    } else {
        $null = $res -match '결과:\s*(\d+)\s*PASS\s*/\s*(\d+)\s*FAIL\s*/\s*콘솔오류\s*(\d+)'
        $pass = [int]$Matches[1]; $fail = [int]$Matches[2]; $err = [int]$Matches[3]
        if ($fail -ne 0) { Add-Failure 'C6 PlayMode' "PlayMode 실패 $fail 건 ($pass PASS)." }
        if ($err  -ne 0) { Add-Failure 'C6 PlayMode' "PlayMode 중 치명적 콘솔 오류 $err 건." }
        if ($fail -eq 0 -and $err -eq 0) { Add-Note "PlayMode $pass PASS / 0 FAIL / 콘솔오류 0" }
    }
}

# ══════════════════════════════════════════════════════════════════════════════
# C7 — Windows 빌드 성공 증거
# ══════════════════════════════════════════════════════════════════════════════
if (-not (Test-Path $P.Build)) {
    Add-Failure 'C7 빌드' "Logs/build_report.txt 가 없다. Windows 빌드를 돌린 적이 없다."
} else {
    $br = Read-Utf8Lines $P.Build
    $joined = $br -join "`n"
    if ($joined -notmatch 'result:\s*Succeeded') {
        Add-Failure 'C7 빌드' "빌드 리포트가 성공이 아니다." (@($br | Where-Object { $_ -match 'result:|totalErrors:' }))
    }
    if ($joined -match 'totalErrors:\s*(\d+)') {
        if ([int]$Matches[1] -ne 0) { Add-Failure 'C7 빌드' "빌드 오류 $($Matches[1]) 건." }
    }
    $exe = $null
    foreach ($l in $br) { if ($l -match '^\s*outputPath:\s*(.+)$') { $exe = $Matches[1].Trim() } }
    if ($null -eq $exe) {
        Add-Failure 'C7 빌드' "빌드 리포트에 outputPath 가 없다 — 실행 가능한 산출물을 확인할 수 없다."
    } elseif (-not (Test-Path -LiteralPath $exe)) {
        Add-Failure 'C7 빌드' "빌드 산출물이 디스크에 없다: $exe" `
            @("  PRD §17.6 은 '실행 가능한 빌드' 를 증거로 요구한다. 빌드를 다시 돌릴 것.")
    } else {
        Add-Note "Windows 빌드 산출물 확인: $exe"
    }
}

# ══════════════════════════════════════════════════════════════════════════════
# C8 — 10층 연속 런 증거
# ══════════════════════════════════════════════════════════════════════════════
if (Test-Path $P.PlayMode) {
    $pm = Read-Utf8Lines $P.PlayMode
    # 지역 변수 이름에 주의 — PowerShell 은 대소문자를 구분하지 않으므로
    # $required 라고 쓰면 위의 $Required(백로그 항목 목록)를 덮어쓴다.
    $tenFloorChecks = @(
        @{ Pattern = 'PASS.*10층 완주가 최소 3회';        Label = '10층 완주 최소 3회' },
        @{ Pattern = 'PASS.*방문 층이 연속이다';           Label = '방문 층 연속 (건너뛴 층 없음)' },
        @{ Pattern = 'PASS.*서로 다른 완주 시드가 최소 3개'; Label = '서로 다른 완주 시드 3개' }
    )
    $miss = @()
    foreach ($r in $tenFloorChecks) {
        if (-not (@($pm | Where-Object { $_ -match $r.Pattern }).Count -gt 0)) { $miss += "  없음: $($r.Label)" }
    }
    if ($miss.Count -gt 0) {
        Add-Failure 'C8 10층 런' "PlayMode 산출물에 10층 연속 런 증거가 없다." $miss
    } else {
        Add-Note "10층 연속 런 증거 3종 확인"
    }
}

# ══════════════════════════════════════════════════════════════════════════════
# C9 — 필수 캡처 매니페스트
# ══════════════════════════════════════════════════════════════════════════════
if (-not (Test-Path $P.Manifest)) {
    Add-Failure 'C9 캡처' "Captures/TenFloor/manifest.txt 가 없다."
} else {
    $mf = Read-Utf8Lines $P.Manifest
    if (($mf -join "`n") -notmatch 'machineFingerprint:') {
        Add-Failure 'C9 캡처' "매니페스트에 machineFingerprint 가 없다 — 기기 종속 비교의 근거가 사라진다."
    }
    $shots = @(Get-ChildItem -LiteralPath (Split-Path $P.Manifest -Parent) -Filter *.png -File -ErrorAction SilentlyContinue)
    if ($shots.Count -lt 9) {
        Add-Failure 'C9 캡처' "캡처가 $($shots.Count) 장이다. PRD §15.1 은 최소 9종을 요구한다."
    } else {
        Add-Note "고정 캡처 $($shots.Count) 장 + 매니페스트 확인"
    }
}

# ══════════════════════════════════════════════════════════════════════════════
# C10 — 독립 비주얼 평가가 최종 ACCEPT
#
#   오래된 ACCEPT 가 새 캡처를 통과시키지 못하도록, 판정 파일이 매니페스트보다
#   새것이어야 한다. 구현자가 캡처를 다시 뽑고 옛 판정을 재사용하는 것을 막는다.
# ══════════════════════════════════════════════════════════════════════════════
if (-not (Test-Path $P.Verdict)) {
    Add-Failure 'C10 시각 평가' "docs/runtime/VISUAL_VERDICT.md 가 없다. 독립 평가 기록이 존재하지 않는다." `
        @("  형식: 'VERDICT: ACCEPT' 또는 'VERDICT: REJECT' 한 줄을 포함할 것.",
          "  평가는 구현자와 분리된 평가자가 수행한다 (PRD §1.2).")
} else {
    $vd = Read-Utf8Lines $P.Verdict
    $verdictLines = @($vd | Where-Object { $_ -match '^\s*VERDICT:\s*(ACCEPT|REJECT|PENDING)\s*$' })
    if ($verdictLines.Count -eq 0) {
        Add-Failure 'C10 시각 평가' "VISUAL_VERDICT.md 에 'VERDICT: ACCEPT|REJECT|PENDING' 줄이 없다."
    } else {
        $last = $verdictLines | Select-Object -Last 1
        $null = $last -match '^\s*VERDICT:\s*([A-Z]+)'
        $v = $Matches[1]
        if ($v -ne 'ACCEPT') {
            Add-Failure 'C10 시각 평가' "독립 시각 평가 판정이 $v 다. ACCEPT 여야 한다." `
                @("  REJECT 는 작업 종료 사유가 아니다 — 백로그 §5 수정 백로그로 전환하고 계속할 것.")
        } elseif (Test-Path $P.Manifest) {
            $vFile = Get-Item -LiteralPath $P.Verdict
            $mFile = Get-Item -LiteralPath $P.Manifest
            if ($vFile.LastWriteTimeUtc -lt $mFile.LastWriteTimeUtc) {
                Add-Failure 'C10 시각 평가' "ACCEPT 판정이 현재 캡처보다 오래됐다 — 옛 판정으로 새 캡처를 통과시킬 수 없다." `
                    @("  판정   : $($vFile.LastWriteTime)", "  캡처   : $($mFile.LastWriteTime)")
            } else {
                Add-Note "독립 시각 평가 ACCEPT (현재 캡처 기준)"
            }
        }
    }
}

# ══════════════════════════════════════════════════════════════════════════════
# C11 — BLOCKED_EXTERNAL 은 사용자만 풀 수 있는 외부 차단임이 명시돼야 한다
# ══════════════════════════════════════════════════════════════════════════════
$blocked = @($Items | Where-Object { $_.State -eq 'BLOCKED_EXTERNAL' })
$badBlocked = @($blocked | Where-Object { $_.Problem -notmatch '외부 차단:' })
if ($badBlocked.Count -gt 0) {
    Add-Failure 'C11 외부 차단' `
        "BLOCKED_EXTERNAL 항목은 '남은 문제:' 에 '외부 차단: <누가 어떻게 푸는가>' 를 적어야 한다." `
        (@($badBlocked | ForEach-Object { "  $($_.Id)  $($_.Title)" }))
}
if ($blocked.Count -gt 0) {
    Add-Note "BLOCKED_EXTERNAL $($blocked.Count) 건 — 사용자 조치 필요"
}

# ══════════════════════════════════════════════════════════════════════════════
# C12 — 진행 문서와 git 작업 상태
# ══════════════════════════════════════════════════════════════════════════════
foreach ($doc in @(@{P=$P.Progress; N='docs/runtime/TOPDOWN_PROGRESS.md'},
                   @{P=$P.Pending;  N='docs/runtime/PENDING_DECISIONS.md'})) {
    if (-not (Test-Path $doc.P)) { Add-Failure 'C12 진행 문서' "$($doc.N) 가 없다." }
}

$gitBranch = ''; $gitHead = ''; $gitDirty = @()
Push-Location $Root
try {
    $gitBranch = (& git rev-parse --abbrev-ref HEAD) -join ''
    $gitHead   = (& git rev-parse --short HEAD) -join ''
    $gitDirty  = @(& git status --porcelain | Where-Object { $_ -match '\S' })
} catch {
    Add-Failure 'C12 git' "git 상태를 읽지 못했다: $($_.Exception.Message)"
} finally {
    Pop-Location
}

if ($gitDirty.Count -gt 0) {
    Add-Failure 'C12 git' "커밋되지 않은 변경이 $($gitDirty.Count) 건 있다 — 전체 완료는 하나의 커밋에 대응해야 한다." `
        (@($gitDirty | Select-Object -First 20 | ForEach-Object { "  $_" }))
}

if ($gitBranch -eq 'main' -or $gitBranch -eq 'master') {
    Add-Failure 'C12 git' "기본 브랜치($gitBranch)에서 자율 작업을 완료 처리할 수 없다. agent/<description> 브랜치를 쓸 것."
}

# ══════════════════════════════════════════════════════════════════════════════
# 판정
# ══════════════════════════════════════════════════════════════════════════════
$reqTotal    = $Required.Count
$reqVerified = @($Required | Where-Object { $_.State -eq 'VERIFIED' }).Count

if ($Json) {
    $obj = [pscustomobject]@{
        ok             = ($Failures.Count -eq 0)
        root           = $Root
        branch         = $gitBranch
        head           = $gitHead
        dirtyFiles     = $gitDirty.Count
        requiredTotal  = $reqTotal
        requiredDone   = $reqVerified
        passes         = @(1..4 | ForEach-Object { if ($PassState.ContainsKey($_)) { $PassState[$_] } else { 'NOT_STARTED' } })
        failures       = @($Failures | ForEach-Object { "$($_.Check): $($_.Message)" })
    }
    Write-Output ($obj | ConvertTo-Json -Depth 5 -Compress)
}

if ($Failures.Count -eq 0) {
    $stamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    $dir = Split-Path $P.Marker -Parent
    if (-not (Test-Path $dir)) { $null = New-Item -ItemType Directory -Path $dir -Force }
    "$stamp`tcommit=$gitHead`tbranch=$gitBranch`trequired=$reqVerified/$reqTotal" |
        Out-File -LiteralPath $P.Marker -Encoding utf8
    Write-Output 'TOPDOWN_ALL_PASSES_COMPLETE'
    exit 0
}

# 실패 보고 — stderr 로 내보내야 Stop hook 이 Claude 에게 돌려준다.
$sb = New-Object System.Text.StringBuilder
$null = $sb.AppendLine('')
$null = $sb.AppendLine('════════ TOPDOWN 검증 실패 — 종료할 수 없다 ════════')
$null = $sb.AppendLine("브랜치 $gitBranch @ $gitHead   ·   Required $reqVerified/$reqTotal VERIFIED   ·   미커밋 $($gitDirty.Count)건")
$null = $sb.AppendLine('')
$i = 0
foreach ($f in $Failures) {
    $i++
    $null = $sb.AppendLine("[$i] $($f.Check) — $($f.Message)")
    if ($f.Detail.Count -gt 0) {
        $shown = @($f.Detail | Select-Object -First 60)
        foreach ($d in $shown) { $null = $sb.AppendLine($d) }
        if ($f.Detail.Count -gt 60) { $null = $sb.AppendLine("      ... 외 $($f.Detail.Count - 60) 줄") }
    }
    $null = $sb.AppendLine('')
}
if ($Notes.Count -gt 0) {
    $null = $sb.AppendLine('── 통과한 것 ──')
    foreach ($n in $Notes) { $null = $sb.AppendLine("  · $n") }
    $null = $sb.AppendLine('')
}
$null = $sb.AppendLine('다음 작업: docs/TOPDOWN_MASTER_BACKLOG.md 의 미완료 Required 항목을 진행하고,')
$null = $sb.AppendLine('           docs/runtime/TOPDOWN_PROGRESS.md 를 갱신한 뒤 로컬 커밋할 것.')
$null = $sb.AppendLine('완료 선언이나 요약은 근거가 아니다 — 이 검증기가 0 을 반환할 때만 완료다.')

[Console]::Error.WriteLine($sb.ToString())
exit 2
</content>

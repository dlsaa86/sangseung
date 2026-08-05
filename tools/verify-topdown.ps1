<#
.SYNOPSIS
    탑다운 진행 상태를 실제 파일과 결과물로만 판정하는 결정론적 검증기.
    **현재 패스의 완료 기준으로만** 막는다.

.DESCRIPTION
    이 스크립트는 대화 요약, 완료 선언, 커밋 메시지를 근거로 삼지 않는다.
    디스크에 있는 것만 본다 — 백로그의 상태 값, 테스트 산출물, 빌드 리포트,
    캡처 매니페스트, 독립 시각 평가 판정, git 작업 상태.

    ── 2026-08-02 구조 변경 (사용자 지시) ─────────────────────────────────────
    직전 판본은 패스와 무관하게 **항상** "Required 전부 VERIFIED + 전체 테스트 +
    빌드 + 캡처 + 독립 평가"를 요구했다. 그 결과 Pass 1(범위를 빠르게 펼치는 단계)이
    사실상 최종 QA처럼 작동했고, 플레이스홀더 하나를 넣을 때마다 최종 증거를
    요구받아 Coverage 속도가 죽었다.

    이제 게이트는 **현재 패스에만** 적용된다.

      Pass 1 완료 : 모든 Required 가 최소 SKELETON 또는 VISIBLE
      Pass 2 완료 : 모든 Required 가 최소 CONNECTED
      Pass 3 완료 : 경험·비주얼 지정 항목 VERIFIED + 독립 시각 평가 ACCEPT
      Pass 4 완료 : 모든 Required VERIFIED + 전체 테스트·빌드·캡처·성능·독립 평가

    **모든 Required 의 VERIFIED 요구는 Pass 4 에서만 적용한다.**

    항상 적용되는 것은 셋뿐이다 — 컴파일이 통과했는가, 분류가 모순되지 않는가,
    작업 기록과 브랜치가 살아 있는가. 이 셋은 어느 패스에서도 깨진 채로 쌓으면
    원인 분리가 불가능해지기 때문이다 (`CLAUDE.md` 탑다운 규칙 4).

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
    [switch] $Json,
    # 배치 모드 — 「이 배치가 주장한 것을 실제로 했는가」만 막는다 (2026-08-02 사용자 승인).
    #
    # 왜 필요한가: Stop 훅이 매 턴 「Pass 4 전부 VERIFIED + 시각 ACCEPT」를 검사했다.
    # 그건 프로토타입 전체 완주를 재는 자인데, 한 배치는 한 축만 바꾼다. 그래서 진짜
    # 진전이 있는 턴에도 매번 「미완료」가 나왔고 그 판정을 해명하는 데 턴이 소모됐다.
    # 규칙이 품질을 만들었고, 게이트의 발동 시점이 낭비를 만들었다.
    #
    # **요구를 지우지 않는다.** 미루는 것은 $BatchDeferrable 목록뿐이고 전부
    # 「지금은 막지 않는다」 절에 ID 와 함께 출력된다. 최종 판정은 여전히 무인자 실행이다.
    [switch] $Batch
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
$Deferred = New-Object System.Collections.ArrayList

# 배치 모드에서만 「지금은 막지 않는다」로 내려가는 검사들.
# 공통점은 하나다 — **한 배치가 혼자 충족시킬 수 없는 것**이다.
#   C2   Pass 4 전부 VERIFIED : 승격은 독립 감사의 일이고 한 배치의 산출물이 아니다
#   C10  시각 판정 ACCEPT     : 평가자가 별도 라운드로 낸다
#   C10b Pass 4 지정 항목     : 위 둘의 파생
# 나머지는 배치 모드에서도 그대로 막는다 — 컴파일·자체검증 신선도·트리 청결·분류 모순·
# 진행 문서·PlayMode 결과. 그것들은 **이 배치가 책임질 수 있는 것**이다.
$BatchDeferrable = @('C2 ', 'C10 ', 'C10b')

function Add-Failure {
    param([string] $Check, [string] $Message, [string[]] $Detail)
    if ($Batch) {
        foreach ($p in $BatchDeferrable) {
            if ($Check.StartsWith($p)) {
                Add-Deferred "[$Check] $Message — 배치 모드라 지금은 막지 않는다 (최종 판정은 무인자 실행이 한다)"
                return
            }
        }
    }
    $null = $Failures.Add([pscustomobject]@{
        Check   = $Check
        Message = $Message
        Detail  = @($Detail)
    })
}
function Add-Note     { param([string] $Message) $null = $Notes.Add($Message) }
# 「지금은 안 막지만 나중 패스에서 막는다」를 눈에 보이게 남긴다.
# 이것이 없으면 게이트 완화가 곧 「사라진 요구사항」이 된다.
function Add-Deferred { param([string] $Message) $null = $Deferred.Add($Message) }

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
$ValidStates = @('NOT_STARTED','SKELETON','VISIBLE','CONNECTED','VERIFIED','DEFERRED','BLOCKED_EXTERNAL','OUT_OF_SESSION_SCOPE')

# 상태 서열 — 「최소 X 이상」을 숫자로 비교하기 위한 것이다.
# DEFERRED/BLOCKED_EXTERNAL/OUT_OF_SESSION_SCOPE 는 서열 밖이라 별도로 다룬다 (아래 $OutOfLadder).
#
# OUT_OF_SESSION_SCOPE 는 2026-08-02 에 사용자 결정으로 늘렸다. 그전에는 「제품에는
# 필요하지만 이번 세션 범위는 아닌」 항목을 표현할 값이 없었고, 그래서 오디오 5건을
# DEFERRED 로 내렸다가 되돌리는 일이 있었다 — DEFERRED 는 「PRD §4.2 명시적 제외」라
# 제품 요구를 지우는 뜻이 되고, Required 로 두면 손대지 않기로 한 항목이 Pass 4 를
# 영구히 막는다. **요구를 지우는 것보다 상태값을 늘리는 쪽이 싸다.**
#
# 분류는 Required 로 남고 출처 줄도 그대로다 — 사라지는 것은 이번 게이트의 요구뿐이다.
$StateRank = @{
    'NOT_STARTED' = 0
    'SKELETON'    = 1
    'VISIBLE'     = 1   # SKELETON 과 같은 층이다. Pass 1 은 둘 중 하나면 통과.
    'CONNECTED'   = 2
    'VERIFIED'    = 3
}
$OutOfLadder = @('DEFERRED','BLOCKED_EXTERNAL','OUT_OF_SESSION_SCOPE')

$Items = New-Object System.Collections.ArrayList
$PassState = @{}
$Pass3Gated = @()

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
                # 이 항목의 상태 바를 **어느 패스가 요구하는가**. 백로그의 `패스:` 필드에
                # 나열된 것 중 **가장 이른 패스**다. 0 이면 필드를 읽지 못한 것이다.
                GatePass = 0
                Evidence = @()
                Problem  = ''
            }
            continue
        }
        if ($null -eq $cur) {
            if ($line -match '^-\s+PASS_([1-4]):\s*([A-Z_]+)\s*$') {
                $PassState[[int]$Matches[1]] = $Matches[2]
            }
            # Pass 3 이 막는 「경험·비주얼 지정 항목」. 목록은 스크립트가 아니라
            # 백로그에 둔다 — 무엇을 막을지는 검증기의 구현 세부가 아니라 결정이다.
            if ($line -match '^-\s+PASS3_GATED:\s*(.+)$') {
                $Pass3Gated = @([regex]::Matches($Matches[1], 'UP-[A-Z]+-\d+') | ForEach-Object { $_.Value })
            }
            continue
        }
        if ($line -match '^##\s') { $null = $Items.Add($cur); $cur = $null; continue }
        if ($line -match '^-\s+분류:\s*(Required|Deferred|Approval Required)\b') { $cur.Class = $Matches[1]; continue }
        if ($line -match '^-\s+상태:\s*([A-Z_]+)\b') {
            $cur.State = $Matches[1]
            # 같은 줄의 `· 패스: P2 P3` 에서 **가장 이른** 패스를 뽑는다.
            # 항목마다 소유 패스가 다르다 — 비주얼 항목을 Pass 2 가 막으면
            # 「범위를 펼치는 단계」가 다시 최종 QA 가 된다 (2026-08-02 사용자 지시).
            $passNums = @([regex]::Matches($line, 'P([1-4])\b') | ForEach-Object { [int]$_.Groups[1].Value })
            if ($passNums.Count -gt 0) { $cur.GatePass = ($passNums | Measure-Object -Minimum).Minimum }
            continue
        }
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

# ── 현재 패스 ─────────────────────────────────────────────────────────────────
function Get-PassState { param([int] $k) if ($PassState.ContainsKey($k)) { return $PassState[$k] } return 'NOT_STARTED' }

$CurrentPass = 5
foreach ($k in 1..4) { if ((Get-PassState $k) -ne 'COMPLETE') { $CurrentPass = $k; break } }
$AllPassesComplete = ($CurrentPass -ge 5)
# 게이트를 적용할 패스. 네 패스가 다 COMPLETE 면 Pass 4 기준(=전부)으로 최종 확인한다.
$GatePass = [Math]::Min($CurrentPass, 4)

# 패스별 「이 상태면 아직 부족하다」 집합.
#   Pass 1 : NOT_STARTED 만 부족 (SKELETON·VISIBLE 이면 통과)
#   Pass 2 : CONNECTED 미만이 부족
#   Pass 3 : Pass 2 와 같은 바 + 지정 항목 VERIFIED + 시각 ACCEPT
#   Pass 4 : VERIFIED 미만이 전부 부족
$PassBarRank = @{ 1 = 1; 2 = 2; 3 = 2; 4 = 3 }
$PassBarName = @{ 1 = 'SKELETON 또는 VISIBLE'; 2 = 'CONNECTED'; 3 = 'CONNECTED'; 4 = 'VERIFIED' }

# 상태 바에 미달인 Required 항목.
#
# `$OwnedByPass` 를 주면 **그 패스가 소유한 항목만** 센다 — 백로그의 `패스:` 필드에서
# 가장 이른 패스가 그 이하인 것들이다. 소유권을 안 보면 Pass 2 가 비주얼 항목까지
# 요구하게 되고, 그것이 「범위를 펼치는 단계」를 다시 최종 QA 로 만든다.
#
# Pass 4 는 소유권과 무관하게 **전부** 요구한다 (게이트 4 는 모든 항목 게이트 이상이다).
function Get-Below {
    param([int] $BarRank, [int] $OwnedByPass = 0)
    return @($Required | Where-Object {
        if ($OutOfLadder -contains $_.State) { return $false }
        if ($OwnedByPass -gt 0 -and $_.GatePass -gt 0 -and $_.GatePass -gt $OwnedByPass) { return $false }
        if (-not $StateRank.ContainsKey($_.State)) { return $true }
        return ($StateRank[$_.State] -lt $BarRank)
    })
}

# 미달이지만 **아직 그 패스의 소유가 아니라서** 세지 않은 것.
# 사라진 요구가 되지 않도록 항상 함께 보고한다.
function Get-NotYetOwned {
    param([int] $BarRank, [int] $OwnedByPass)
    return @($Required | Where-Object {
        if ($OutOfLadder -contains $_.State) { return $false }
        if ($_.GatePass -le 0 -or $_.GatePass -le $OwnedByPass) { return $false }
        if (-not $StateRank.ContainsKey($_.State)) { return $true }
        return ($StateRank[$_.State] -lt $BarRank)
    })
}

# ── -Stats 모드: 세기만 하고 끝낸다 ───────────────────────────────────────────
if ($Stats) {
    Write-Output "=== TOPDOWN 백로그 통계 ==="
    Write-Output ("추적 항목        : {0}" -f $Items.Count)
    Write-Output ("Required         : {0}" -f $Required.Count)
    foreach ($s in $ValidStates) {
        $n = @($Required | Where-Object { $_.State -eq $s }).Count
        Write-Output ("  {0,-18}: {1}" -f $s, $n)
    }
    Write-Output ''
    foreach ($k in 1..4) { Write-Output ("PASS_{0}           : {1}" -f $k, (Get-PassState $k)) }
    Write-Output ''
    Write-Output ("현재 패스        : {0}" -f $(if ($AllPassesComplete) { '전부 COMPLETE' } else { "Pass $CurrentPass" }))
    foreach ($k in 1..4) {
        $owned = @(Get-Below $PassBarRank[$k] $k)
        $later = @(Get-NotYetOwned $PassBarRank[$k] $k)
        Write-Output ("  Pass {0} 바({1,-20}) 미달 : {2} 건  (후속 패스 소유 {3} 건 제외)" -f $k, $PassBarName[$k], $owned.Count, $later.Count)
    }
    Write-Output ''
    Write-Output "=== 항목별 게이트 패스 분포 (백로그 '패스:' 필드의 가장 이른 패스) ==="
    foreach ($k in 1..4) {
        $n = @($Required | Where-Object { $_.GatePass -eq $k }).Count
        Write-Output ("  P{0} 소유 : {1} 건" -f $k, $n)
    }
    $noGate = @($Required | Where-Object { $_.GatePass -le 0 })
    if ($noGate.Count -gt 0) {
        Write-Output ("  ⚠ 패스 표기를 읽지 못한 항목 : {0} 건 — {1}" -f $noGate.Count, (($noGate | ForEach-Object { $_.Id }) -join ', '))
    }
    exit 0
}

# ══════════════════════════════════════════════════════════════════════════════
# C2 — 현재 패스의 상태 바
#
#   **패스마다 요구가 다르다.** Pass 1 에서 VERIFIED 가 아니라는 이유로 막지 않는다.
#   막지 않는 것을 「사라진 요구」로 만들지 않기 위해, 나중 패스가 요구할 것을
#   Add-Deferred 로 항상 함께 보고한다.
# ══════════════════════════════════════════════════════════════════════════════
$barRank = $PassBarRank[$GatePass]
$below   = @(Get-Below $barRank $GatePass)
$notYet  = @(Get-NotYetOwned $barRank $GatePass)

$noGate = @($Required | Where-Object { $_.GatePass -le 0 })
if ($noGate.Count -gt 0) {
    Add-Failure 'C2c 패스 표기' "Required 항목의 '패스:' 표기를 읽지 못했다 — 소유 패스를 판정할 수 없다." `
        (@($noGate | ForEach-Object { "  $($_.Id)  $($_.Title)" }))
}

if ($below.Count -gt 0) {
    $byState = @()
    foreach ($s in @('NOT_STARTED','SKELETON','VISIBLE','CONNECTED')) {
        $g = @($below | Where-Object { $_.State -eq $s })
        if ($g.Count -gt 0) {
            $byState += "  [$s] $($g.Count)건"
            foreach ($it in $g) { $byState += "      $($it.Id)  $($it.Title)   (P$($it.GatePass) 소유)" }
        }
    }
    Add-Failure "C2 Pass $GatePass 상태 바" `
        "Pass $GatePass 이 소유한 Required 가 최소 $($PassBarName[$GatePass]) 여야 한다. $($below.Count)건 미달." $byState
} else {
    Add-Note "Pass $GatePass 상태 바 충족 — 이 패스가 소유한 Required 전부 $($PassBarName[$GatePass]) 이상"
}

# 미달이지만 **후속 패스 소유**라 지금 세지 않은 것. 사라진 요구가 되지 않게 항상 적는다.
if ($notYet.Count -gt 0) {
    $byOwner = @()
    foreach ($k in ($GatePass + 1)..4) {
        $g = @($notYet | Where-Object { $_.GatePass -eq $k })
        if ($g.Count -gt 0) { $byOwner += "P$k 소유 $($g.Count)건 (" + (($g | ForEach-Object { $_.Id }) -join ', ') + ")" }
    }
    Add-Deferred ("현재 바 미달이나 후속 패스 소유라 지금 막지 않음 — " + ($byOwner -join ' · '))
}

# 나중 패스가 요구할 것을 미리 보여 준다 (막지는 않는다).
for ($k = $GatePass + 1; $k -le 4; $k++) {
    $laterBelow = @(Get-Below $PassBarRank[$k] $k)
    if ($laterBelow.Count -gt 0) {
        Add-Deferred "Pass $k 바($($PassBarName[$k])) 미달 $($laterBelow.Count)건 — 지금은 막지 않는다"
    }
}

# Required 인데 DEFERRED 로 빠져나간 것은 어느 패스에서도 모순이다.
$Escaped = @($Required | Where-Object { $_.State -eq 'DEFERRED' })
if ($Escaped.Count -gt 0) {
    Add-Failure 'C2b 분류 모순' `
        "Required 로 분류된 항목이 DEFERRED 상태다. 범위 축소는 사용자 결정이다." `
        (@($Escaped | ForEach-Object { "  $($_.Id)  $($_.Title)" }))
}

# ══════════════════════════════════════════════════════════════════════════════
# C3 — 패스 진행
#
#   현재 패스의 바를 이미 넘었으면 「다음 패스로 올려라」가 다음 작업이다.
#   네 패스가 전부 COMPLETE 여야 전체 완료다.
# ══════════════════════════════════════════════════════════════════════════════
if (-not $AllPassesComplete) {
    if ($below.Count -eq 0) {
        Add-Failure 'C3 패스 승격' `
            "Pass $GatePass 의 상태 바를 이미 넘었다. 백로그 §1 의 'PASS_$GatePass' 를 COMPLETE 로 올리고 다음 패스로 갈 것." `
            @("  현재: PASS_$GatePass : $(Get-PassState $GatePass)",
              "  이 줄은 verify-topdown.ps1 이 직접 파싱한다 — 형식을 바꾸지 말 것.")
    } else {
        Add-Deferred "PASS_$GatePass 는 상태 바를 채운 뒤 COMPLETE 로 올린다"
    }
    $passLine = @()
    foreach ($k in 1..4) { $passLine += "  PASS_$k : $(Get-PassState $k)" }
    Add-Note ("패스 상태 — " + (($passLine | ForEach-Object { $_.Trim() }) -join '  |  '))
}

# ══════════════════════════════════════════════════════════════════════════════
# C4 — 증거 경로 (Pass 2 부터: 끊긴 경로 / Pass 4 부터: 경로 자체의 존재)
#
#   Pass 1 은 범위를 펼치는 단계라 증거를 요구하지 않는다. 다만 **가리키는데 없는**
#   경로는 데이터 손상에 가까우므로 Pass 2 부터 막는다 — 「증거가 있다」는 거짓 기록이
#   백로그에 남는 것이 미충족보다 나쁘다는 것을 이 저장소는 이미 여러 번 겪었다.
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

if ($GatePass -ge 2) {
    if ($missingEvidence.Count -gt 0) {
        Add-Failure 'C4 증거 부재' "백로그가 가리키는 증거 파일이 디스크에 없다." $missingEvidence
    }
} elseif ($missingEvidence.Count -gt 0) {
    Add-Deferred "끊긴 증거 경로 $($missingEvidence.Count)건 — Pass 2 부터 막는다"
}

if ($GatePass -ge 4) {
    if ($noEvidence.Count -gt 0) {
        Add-Failure 'C4 증거 없음' "Required 항목에 증거 경로가 지정되지 않았다." $noEvidence
    }
} elseif ($noEvidence.Count -gt 0) {
    Add-Deferred "증거 경로 미지정 $($noEvidence.Count)건 — Pass 4 에서 막는다"
}

# ══════════════════════════════════════════════════════════════════════════════
# C5 — Unity 컴파일 오류 0  【모든 패스에서 적용】
#
#   asmdef 이 없어 스크립트 하나가 전체를 막는다. 깨진 채로 쌓으면 원인 분리가
#   불가능해진다 (CLAUDE.md 탑다운 규칙 4). 이것은 Pass 1 에서도 예외가 아니다.
#
#   Unity 를 띄우지 않고 컴파일 성공을 확인하는 방법:
#   컴파일이 성공해야만 Library/ScriptAssemblies/ 의 어셈블리가 새로 쓰인다.
#   따라서 "가장 최근에 수정된 .cs 가 그 DLL 보다 새것이 아니다" 는 곧
#   "마지막 소스 변경이 실제로 컴파일을 통과했다" 는 뜻이다.
#   자체 검증 마커(fail=0)는 그 어셈블리로 테스트가 실제 실행됐음을 보탠다 —
#   이것이 Pass 1 이 배치마다 한 번 돌리는 「최소 스모크 테스트」의 강제 지점이다.
#
#   **어셈블리는 하나가 아니다.** `Assets/Editor/` 와 `*/Editor/` 아래의 스크립트는
#   `Assembly-CSharp-Editor.dll` 로, 나머지는 `Assembly-CSharp.dll` 로 간다.
#   둘을 구분하지 않고 전부 `Assembly-CSharp.dll` 과 비교하면 **에디터 스크립트를
#   건드리는 순간 C5 가 영원히 실패한다** — 그 파일은 런타임 어셈블리를 절대
#   갱신하지 않기 때문이다. (2026-08-01 발견)
# ══════════════════════════════════════════════════════════════════════════════
$EditorAssembly = Join-Path $Root 'Library\ScriptAssemblies\Assembly-CSharp-Editor.dll'

function Get-NewestSource {
    param([bool] $EditorScope)
    $newest = $null
    foreach ($d in $SourceDirs) {
        if (-not (Test-Path $d)) { continue }
        $found = Get-ChildItem -LiteralPath $d -Filter *.cs -Recurse -File -ErrorAction SilentlyContinue |
                 Where-Object { ($_.FullName -match '\\Editor\\') -eq $EditorScope } |
                 Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        if ($null -ne $found) {
            if ($null -eq $newest -or $found.LastWriteTimeUtc -gt $newest.LastWriteTimeUtc) { $newest = $found }
        }
    }
    return $newest
}

$newestRuntimeSrc = Get-NewestSource $false
$newestEditorSrc  = Get-NewestSource $true

# 자체 검증 비교는 범위를 가리지 않는다 — 어느 쪽이 바뀌었든 테스트를 다시 돌려야 한다.
$newestSrc = $newestRuntimeSrc
if ($null -ne $newestEditorSrc -and
    ($null -eq $newestSrc -or $newestEditorSrc.LastWriteTimeUtc -gt $newestSrc.LastWriteTimeUtc)) {
    $newestSrc = $newestEditorSrc
}

function Test-Compiled {
    param([string] $AssemblyPath, [string] $AssemblyName, $NewestSource, [string] $ScopeLabel)
    if (-not (Test-Path $AssemblyPath)) {
        Add-Failure 'C5 컴파일' "Library/ScriptAssemblies/$AssemblyName 이 없다. 프로젝트가 한 번도 컴파일되지 않았다."
        return
    }
    if ($null -eq $NewestSource) { return }
    $dll = Get-Item -LiteralPath $AssemblyPath
    if ($NewestSource.LastWriteTimeUtc -gt $dll.LastWriteTimeUtc) {
        $rel = $NewestSource.FullName.Substring($Root.Length).TrimStart('\')
        Add-Failure 'C5 컴파일' "$ScopeLabel 컴파일 결과보다 새로운 소스가 있다 — 마지막 변경이 컴파일을 통과했다는 증거가 없다." `
            @("  최신 소스 : $rel  ($($NewestSource.LastWriteTime))",
              "  어셈블리  : $AssemblyName  ($($dll.LastWriteTime))",
              "  조치      : Unity 에 포커스를 줘 컴파일시킨 뒤 다시 검증할 것")
    }
}

Test-Compiled $P.Assembly     'Assembly-CSharp.dll'        $newestRuntimeSrc '런타임'
Test-Compiled $EditorAssembly 'Assembly-CSharp-Editor.dll' $newestEditorSrc  '에디터'

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
            Add-Failure 'C5 자체 검증' "자체 검증 이후 수정된 소스가 있다 — 배치의 스모크 테스트가 현재 코드를 검증하지 않았다." `
                @("  최신 소스 : $rel", "  마지막 검증: $($st.Trim())")
        }
    }
}

# ══════════════════════════════════════════════════════════════════════════════
# C6 — EditMode / PlayMode 결과  【Pass 4 에서만 막는다】
#
#   Pass 1~3 에서도 **실패가 기록돼 있으면** 막는다. 실패를 방치하는 것과
#   아직 안 돌린 것은 다르다 — 전자는 회귀이고 후자는 순서다.
#   「전부 통과했는가」를 요구하는 것은 Pass 4 다.
# ══════════════════════════════════════════════════════════════════════════════
$emPass = -1; $emFail = -1
if (Test-Path $P.EditMode) {
    $em = Read-Utf8Lines $P.EditMode
    $sum = @($em | Where-Object { $_ -match '합계:\s*(\d+)\s*PASS\s*/\s*(\d+)\s*FAIL' }) | Select-Object -Last 1
    if ($null -ne $sum) {
        $null = $sum -match '합계:\s*(\d+)\s*PASS\s*/\s*(\d+)\s*FAIL'
        $emPass = [int]$Matches[1]; $emFail = [int]$Matches[2]
    }
}
if ($emFail -gt 0) {
    Add-Failure 'C6 EditMode' "EditMode 실패 $emFail 건 ($emPass PASS). 실패를 남긴 채 진행하지 않는다."
} elseif ($emFail -eq 0 -and $emPass -gt 0) {
    Add-Note "EditMode $emPass PASS / 0 FAIL"
} elseif ($GatePass -ge 4) {
    Add-Failure 'C6 EditMode' "Logs/editmode_tests.txt 가 없거나 '합계: N PASS / M FAIL' 줄을 찾지 못했다."
} else {
    Add-Deferred "EditMode 결과 파일 미확인 — Pass 4 에서 막는다"
}

$pmPass = -1; $pmFail = -1; $pmErr = -1
if (Test-Path $P.PlayMode) {
    $pm = Read-Utf8Lines $P.PlayMode
    $res = @($pm | Where-Object { $_ -match '결과:\s*(\d+)\s*PASS\s*/\s*(\d+)\s*FAIL\s*/\s*콘솔오류\s*(\d+)' }) | Select-Object -Last 1
    if ($null -ne $res) {
        $null = $res -match '결과:\s*(\d+)\s*PASS\s*/\s*(\d+)\s*FAIL\s*/\s*콘솔오류\s*(\d+)'
        $pmPass = [int]$Matches[1]; $pmFail = [int]$Matches[2]; $pmErr = [int]$Matches[3]
    }
}
if ($pmFail -gt 0) {
    Add-Failure 'C6 PlayMode' "PlayMode 실패 $pmFail 건 ($pmPass PASS). 실패를 남긴 채 진행하지 않는다."
}
if ($pmErr -gt 0) {
    if ($GatePass -ge 3) { Add-Failure 'C6 PlayMode' "PlayMode 중 치명적 콘솔 오류 $pmErr 건." }
    else { Add-Deferred "PlayMode 콘솔 오류 $pmErr 건 — Pass 3 부터 막는다" }
}
if ($pmFail -eq 0 -and $pmErr -eq 0) {
    Add-Note "PlayMode $pmPass PASS / 0 FAIL / 콘솔오류 0"
} elseif ($pmPass -lt 0) {
    if ($GatePass -ge 4) { Add-Failure 'C6 PlayMode' "Logs/tenfloor_playmode.txt 가 없거나 결과 줄을 찾지 못했다." }
    else { Add-Deferred "PlayMode 결과 파일 미확인 — Pass 4 에서 막는다" }
}

# ══════════════════════════════════════════════════════════════════════════════
# C7 — Windows 빌드 증거  【Pass 4】
# ══════════════════════════════════════════════════════════════════════════════
if ($GatePass -ge 4) {
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
} else {
    if (Test-Path $P.Build) {
        $joined = (Read-Utf8Lines $P.Build) -join "`n"
        if ($joined -match 'result:\s*Succeeded') { Add-Note "Windows 빌드 리포트 성공 (Pass 4 전이라 미강제)" }
    }
    Add-Deferred "Windows 빌드 증거 — Pass 4 에서 막는다"
}

# ══════════════════════════════════════════════════════════════════════════════
# C8 — 10층 연속 런 증거  【Pass 4】
#   Pass 2 는 「연결」이 목표이고 Pass 4 가 「증명」이다.
# ══════════════════════════════════════════════════════════════════════════════
$tenFloorMiss = @()
if (Test-Path $P.PlayMode) {
    $pm = Read-Utf8Lines $P.PlayMode
    # 지역 변수 이름에 주의 — PowerShell 은 대소문자를 구분하지 않으므로
    # $required 라고 쓰면 위의 $Required(백로그 항목 목록)를 덮어쓴다.
    $tenFloorChecks = @(
        @{ Pattern = 'PASS.*10층 완주가 최소 3회';        Label = '10층 완주 최소 3회' },
        @{ Pattern = 'PASS.*방문 층이 연속이다';           Label = '방문 층 연속 (건너뛴 층 없음)' },
        @{ Pattern = 'PASS.*서로 다른 완주 시드가 최소 3개'; Label = '서로 다른 완주 시드 3개' }
    )
    foreach ($r in $tenFloorChecks) {
        if (-not (@($pm | Where-Object { $_ -match $r.Pattern }).Count -gt 0)) { $tenFloorMiss += "  없음: $($r.Label)" }
    }
}
if ($tenFloorMiss.Count -gt 0) {
    if ($GatePass -ge 4) { Add-Failure 'C8 10층 런' "PlayMode 산출물에 10층 연속 런 증거가 없다." $tenFloorMiss }
    else { Add-Deferred "10층 런 증거 $($tenFloorMiss.Count)종 미확인 — Pass 4 에서 막는다" }
} elseif (Test-Path $P.PlayMode) {
    Add-Note "10층 연속 런 증거 3종 확인"
}

# ══════════════════════════════════════════════════════════════════════════════
# C9 — 필수 캡처 매니페스트  【Pass 3 부터】
#   Pass 3 이 비주얼 패스이므로 캡처 세트가 그 패스의 작업 재료다.
# ══════════════════════════════════════════════════════════════════════════════
$shotCount = 0
if (Test-Path $P.Manifest) {
    $shotCount = @(Get-ChildItem -LiteralPath (Split-Path $P.Manifest -Parent) -Filter *.png -File -ErrorAction SilentlyContinue).Count
}
if ($GatePass -ge 3) {
    if (-not (Test-Path $P.Manifest)) {
        Add-Failure 'C9 캡처' "Captures/TenFloor/manifest.txt 가 없다."
    } else {
        $mf = Read-Utf8Lines $P.Manifest
        if (($mf -join "`n") -notmatch 'machineFingerprint:') {
            Add-Failure 'C9 캡처' "매니페스트에 machineFingerprint 가 없다 — 기기 종속 비교의 근거가 사라진다."
        }
        if ($shotCount -lt 9) {
            Add-Failure 'C9 캡처' "캡처가 $shotCount 장이다. PRD §15.1 은 최소 9종을 요구한다."
        } else {
            Add-Note "고정 캡처 $shotCount 장 + 매니페스트 확인"
        }
    }
} else {
    if ($shotCount -gt 0) { Add-Note "고정 캡처 $shotCount 장 (Pass 3 전이라 미강제)" }
    Add-Deferred "고정 캡처 세트 — Pass 3 부터 막는다"
}

# ══════════════════════════════════════════════════════════════════════════════
# C10 — 독립 비주얼 평가  【Pass 3 부터】
#
#   REJECT 는 Pass 1·2 의 차단 조건이 아니다 (CLAUDE.md 탑다운 규칙 2·3).
#   지적은 백로그 §5 로 옮기고 계속한다. Pass 3 에서 소진한다.
#
#   오래된 ACCEPT 가 새 캡처를 통과시키지 못하도록, 판정 파일이 매니페스트보다
#   새것이어야 한다. 구현자가 캡처를 다시 뽑고 옛 판정을 재사용하는 것을 막는다.
# ══════════════════════════════════════════════════════════════════════════════
$verdictValue = ''
# 마크다운 헤딩(`## VERDICT: REJECT`)·평문(`VERDICT: ACCEPT`)·**굵게**(`VERDICT: **REJECT**`)를
# 모두 읽는다. 직전 판본은 굵게 표시를 못 읽어 4·5·6차 판정을 통째로 건너뛰었고,
# 게이트가 읽는 「마지막 판정」이 **3차(2026-08-04)** 에 멈춰 있었다. 같은 부류의 결함을
# 헤딩 형태로 한 번 고쳤는데 굵게 표시로 재발한 것이다 — 그래서 이번엔 장식을 통째로 허용한다.
$verdictRe = '^\s*#{0,6}\s*VERDICT:\s*\**\s*(ACCEPT|REJECT|PENDING)\b'
# 그리고 판정 파일은 하나가 아니다. 라운드별 파일(`VISUAL_VERDICT_R7.md`)이 실제로 쓰였는데
# 본 파일만 열던 판본은 그 라운드를 **존재하지 않는 것으로** 취급했다.
# 판정을 담은 파일 중 가장 최근에 수정된 것이 현재 판정이다.
$verdictFile = $null
foreach ($vf in @(Get-ChildItem -LiteralPath (Join-Path $Root 'docs\runtime') -Filter 'VISUAL_VERDICT*.md' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc)) {
    $lines = @(Read-Utf8Lines $vf.FullName | Where-Object { $_ -match $verdictRe })
    if ($lines.Count -gt 0) {
        $null = (@($lines) | Select-Object -Last 1) -match $verdictRe
        $verdictValue = $Matches[1]
        $verdictFile  = $vf
    }
}
if ($null -ne $verdictFile) { $P.Verdict = $verdictFile.FullName }

# 신선도는 **그 판정이 실제로 본 세트**와 비교해야 한다.
# 직전 판본은 `Captures\TenFloor` 에 박혀 있었는데, 4차 이후 독립 평가는
# `Captures\symbols_v*` 를 봤다 — 즉 **아무도 평가하지 않는 세트**로 신선도를 재고 있었고
# 「옛 판정으로 새 캡처를 통과시키지 못한다」가 조용히 무효였다.
# 판정 블록 형식이 `- 대상: Captures/<set>/manifest.txt` 를 요구하므로 그 줄에서 읽는다.
# 못 읽으면 고정 세트로 되돌아간다 — 모르는 채 통과시키지 않는다.
# (`$P.Manifest` 는 건드리지 않는다. C9 는 PRD §15.1 고정 세트를 검사하는 별개의 일이다.)
$P.VerdictManifest = $P.Manifest
if ($null -ne $verdictFile) {
    $targets = @(Read-Utf8Lines $verdictFile.FullName |
                 Where-Object { $_ -match '대상\s*:.*?(Captures\S+manifest\.txt)' })
    if ($targets.Count -gt 0) {
        $null = (@($targets) | Select-Object -Last 1) -match '대상\s*:.*?(Captures\S+manifest\.txt)'
        $cand = Join-Path $Root ($Matches[1] -replace '/', '\')
        if (Test-Path -LiteralPath $cand) { $P.VerdictManifest = $cand }
        else { Add-Note "판정문이 가리키는 캡처 매니페스트가 없다: $($Matches[1]) — 고정 세트로 신선도를 잰다" }
    }
}

# ── 2026-08-02 게이트 현실화 (사용자 결정) ─────────────────────────────────
#   이전에는 Pass 3 완료에 ACCEPT 를 요구했다. 그런데 ACCEPT 의 정의는
#   「판독성·스타일 평균 4.0 이상 · 2점 이하 없음」이고 그건 **VERIFIED 급 기준**이다.
#   Pass 3 의 바는 CONNECTED 이므로 두 층위가 어긋나 있었다 — 실제로 14라운드 동안
#   REJECT 였고 그동안 Pass 3 은 상태 바를 **이미 넘긴 채** 묶여 있었다.
#   게이트가 「연결됐는가」가 아니라 「완성됐는가」를 묻고 있었다.
#
#   그래서 나눈다 — Pass 3 은 **평가가 현재 그림에 대해 돌았는가**를 묻고,
#   4.0/4.0 ACCEPT 는 **Pass 4** 로 옮긴다. 기준을 낮추는 것이 아니라 묻는 시점을
#   바로잡는 것이다. ACCEPT 요구 자체는 사라지지 않는다.
if ($GatePass -ge 3) {
    if (-not (Test-Path $P.Verdict)) {
        Add-Failure 'C10 시각 평가' "docs/runtime/VISUAL_VERDICT.md 가 없다. 독립 평가 기록이 존재하지 않는다." `
            @("  형식: 'VERDICT: ACCEPT' 또는 'VERDICT: REJECT' 한 줄을 포함할 것.",
              "  평가는 구현자와 분리된 평가자가 수행한다 (PRD §1.2).")
    } elseif ($verdictValue -eq '') {
        Add-Failure 'C10 시각 평가' "VISUAL_VERDICT.md 에 'VERDICT: ACCEPT|REJECT|PENDING' 줄이 없다."
    } elseif ($GatePass -ge 4 -and $verdictValue -ne 'ACCEPT') {
        Add-Failure 'C10 시각 평가' "독립 시각 평가 판정이 $verdictValue 다. Pass 4 완료에는 ACCEPT 가 필요하다." `
            @("  REJECT 는 작업 종료 사유가 아니다 — 백로그 §5 수정 백로그로 전환하고 계속할 것.")
    } elseif (Test-Path $P.VerdictManifest) {
        $vFile = Get-Item -LiteralPath $P.Verdict
        $mFile = Get-Item -LiteralPath $P.VerdictManifest
        if ($vFile.LastWriteTimeUtc -lt $mFile.LastWriteTimeUtc) {
            # 이 검사는 Pass 3 에서도 살아 있다. 옛 판정으로 새 캡처를 통과시키는 것이
            # 이 게이트가 막아야 할 첫째다 — ACCEPT 든 REJECT 든 **현재 그림**에 대한 판정이어야 한다.
            Add-Failure 'C10 시각 평가' "독립 평가가 현재 캡처보다 오래됐다 — 옛 판정으로 새 캡처를 통과시킬 수 없다." `
                @("  판정   : $($vFile.LastWriteTime)", "  캡처   : $($mFile.LastWriteTime)",
                  "  캡처를 다시 뽑았으면 독립 평가도 다시 받는다.")
        } elseif ($verdictValue -eq 'ACCEPT') {
            Add-Note "독립 시각 평가 ACCEPT (현재 캡처 기준)"
        } else {
            Add-Note "독립 시각 평가 $verdictValue — 현재 캡처 기준 (ACCEPT 는 Pass 4 에서 막는다)"
            Add-Deferred "독립 시각 평가 ACCEPT — Pass 4 에서 막는다"
        }
    }
} else {
    if ($verdictValue -ne '') { Add-Note "독립 시각 평가 현재 판정: $verdictValue (Pass 3 전이라 미강제)" }
    Add-Deferred "독립 시각 평가 ACCEPT — Pass 3 부터 막는다"
}

# ══════════════════════════════════════════════════════════════════════════════
# C10b — Pass 3 이 막는 경험·비주얼 지정 항목  【Pass 3 부터】
#
#   목록은 백로그 §1 의 'PASS3_GATED:' 줄에서 온다. 스크립트에 박아 두면
#   무엇을 막을지가 코드 리뷰 없이는 안 보인다.
# ══════════════════════════════════════════════════════════════════════════════
if ($Pass3Gated.Count -gt 0) {
    $gatedItems = @($Required | Where-Object { $Pass3Gated -contains $_.Id })
    $gatedUnmet = @($gatedItems | Where-Object { $_.State -ne 'VERIFIED' -and ($OutOfLadder -notcontains $_.State) })
    $unknownIds = @($Pass3Gated | Where-Object { $g = $_; -not (@($Required | Where-Object { $_.Id -eq $g }).Count -gt 0) })
    if ($unknownIds.Count -gt 0) {
        Add-Failure 'C10b 지정 목록' "PASS3_GATED 가 Required 에 없는 ID 를 가리킨다." (@($unknownIds | ForEach-Object { "  $_" }))
    }
    # 같은 이유로 층위를 맞춘다 — Pass 3 의 바는 CONNECTED 다. 지정 항목에만
    # VERIFIED 를 요구하면 그 28건만 Pass 4 기준으로 채점하는 셈이 된다.
    $gatedBelowConnected = @($gatedItems | Where-Object {
        ($OutOfLadder -notcontains $_.State) -and
        ((-not $StateRank.ContainsKey($_.State)) -or ($StateRank[$_.State] -lt $StateRank['CONNECTED']))
    })
    if ($GatePass -ge 4) {
        if ($gatedUnmet.Count -gt 0) {
            Add-Failure 'C10b 경험·비주얼' `
                "Pass 4 지정 항목 $($gatedItems.Count)건 중 $($gatedUnmet.Count)건이 VERIFIED 가 아니다." `
                (@($gatedUnmet | ForEach-Object { "  [$($_.State)] $($_.Id)  $($_.Title)" }))
        } else {
            Add-Note "지정 경험·비주얼 항목 $($gatedItems.Count)건 전부 VERIFIED"
        }
    } elseif ($GatePass -ge 3) {
        if ($gatedBelowConnected.Count -gt 0) {
            Add-Failure 'C10b 경험·비주얼' `
                "Pass 3 지정 항목 $($gatedItems.Count)건 중 $($gatedBelowConnected.Count)건이 CONNECTED 미만이다." `
                (@($gatedBelowConnected | ForEach-Object { "  [$($_.State)] $($_.Id)  $($_.Title)" }))
        } else {
            Add-Note "Pass 3 지정 경험·비주얼 항목 $($gatedItems.Count)건 전부 CONNECTED 이상"
            Add-Deferred "지정 경험·비주얼 $($gatedItems.Count)건 VERIFIED — Pass 4 에서 막는다"
        }
    } else {
        Add-Deferred "Pass 3 지정 경험·비주얼 $($gatedItems.Count)건 중 $($gatedUnmet.Count)건 미충족 — Pass 3 부터 막는다"
    }
} elseif ($GatePass -ge 3) {
    Add-Failure 'C10b 지정 목록' "백로그 §1 에 'PASS3_GATED:' 줄이 없다 — Pass 3 이 무엇을 막는지 정의되지 않았다."
}

# ══════════════════════════════════════════════════════════════════════════════
# C11 — BLOCKED_EXTERNAL 은 사용자만 풀 수 있는 외부 차단임이 명시돼야 한다  【항상】
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
# C12 — 진행 문서와 git 작업 상태  【항상, 단 미커밋은 Pass 4 에서만 막는다】
#
#   Pass 1~3 은 45~90분 단위로 커밋한다. 작업 도중의 미커밋 변경은 정상이므로
#   막지 않는다. 「전체 완료가 하나의 커밋에 대응해야 한다」는 Pass 4 의 요구다.
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
    if ($GatePass -ge 4) {
        Add-Failure 'C12 git' "커밋되지 않은 변경이 $($gitDirty.Count) 건 있다 — 전체 완료는 하나의 커밋에 대응해야 한다." `
            (@($gitDirty | Select-Object -First 20 | ForEach-Object { "  $_" }))
    } else {
        Add-Deferred "미커밋 $($gitDirty.Count)건 — Pass 4 에서 막는다 (배치 종료 시 커밋할 것)"
    }
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
        currentPass    = $(if ($AllPassesComplete) { 0 } else { $CurrentPass })
        gatePass       = $GatePass
        gateBar        = $PassBarName[$GatePass]
        belowBar       = $below.Count
        requiredTotal  = $reqTotal
        requiredDone   = $reqVerified
        passes         = @(1..4 | ForEach-Object { Get-PassState $_ })
        failures       = @($Failures | ForEach-Object { "$($_.Check): $($_.Message)" })
        deferred       = @($Deferred)
    }
    Write-Output ($obj | ConvertTo-Json -Depth 5 -Compress)
}

if ($Failures.Count -eq 0) {
    $stamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    $dir = Split-Path $P.Marker -Parent
    if (-not (Test-Path $dir)) { $null = New-Item -ItemType Directory -Path $dir -Force }
    "$stamp`tcommit=$gitHead`tbranch=$gitBranch`trequired=$reqVerified/$reqTotal`tpass=$(if ($AllPassesComplete) { 'ALL' } else { $CurrentPass })" |
        Out-File -LiteralPath $P.Marker -Encoding utf8
    Write-Output 'TOPDOWN_ALL_PASSES_COMPLETE'
    exit 0
}

# 실패 보고 — stderr 로 내보내야 Stop hook 이 Claude 에게 돌려준다.
$passLabel = $(if ($AllPassesComplete) { '전 패스 COMPLETE — 최종 확인' } else { "Pass $CurrentPass (바: $($PassBarName[$GatePass]))" })
$sb = New-Object System.Text.StringBuilder
$null = $sb.AppendLine('')
$null = $sb.AppendLine('════════ TOPDOWN 검증 실패 — 종료할 수 없다 ════════')
$null = $sb.AppendLine("현재 $passLabel   ·   브랜치 $gitBranch @ $gitHead")
$null = $sb.AppendLine("Required $reqTotal 건 · 현재 바 미달 $($below.Count) 건 · VERIFIED $reqVerified 건 · 미커밋 $($gitDirty.Count) 건")
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
if ($Deferred.Count -gt 0) {
    $null = $sb.AppendLine('── 지금은 막지 않는다 (나중 패스가 요구한다) ──')
    foreach ($d in $Deferred) { $null = $sb.AppendLine("  · $d") }
    $null = $sb.AppendLine('')
}
$null = $sb.AppendLine("다음 작업: 현재 패스($passLabel)의 미달 항목만 처리한다.")
$null = $sb.AppendLine('           나중 패스의 요구(전체 테스트·빌드·캡처·독립 평가)를 지금 하지 않는다.')
$null = $sb.AppendLine('           배치가 끝나면 docs/runtime/TOPDOWN_PROGRESS.md 를 갱신하고 로컬 커밋할 것.')
$null = $sb.AppendLine('완료 선언이나 요약은 근거가 아니다 — 이 검증기가 0 을 반환할 때만 완료다.')

[Console]::Error.WriteLine($sb.ToString())
exit 2

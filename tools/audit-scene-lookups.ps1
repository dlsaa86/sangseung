<#
.SYNOPSIS
    씬 오브젝트를 이름으로 찾는 코드를 정적으로 감사한다 (UP-TECH-02).

.DESCRIPTION
    `MASTER_PRD.md` §13.5 와 `TECH_SPEC.md` §2 는 "Scene 오브젝트를 이름 검색으로 찾지
    않는다"를 요구한다. 지금까지 이 항목에는 **자동 검사가 없었고**, 그래서
    "0건이다"라는 주장을 아무도 확인할 수 없었다. 이 스크립트가 그 증거를 만든다.

    두 종류를 구분해서 센다 — 같은 것이 아니기 때문이다.

    [금지]  이름 기반 조회. PRD 가 직접 금지한 것이다. 0 이어야 한다.
            GameObject.Find / FindWithTag / FindGameObjectWithTag /
            FindGameObjectsWithTag / transform.Find

    [주의]  타입 기반 조회. 이름을 쓰지 않으므로 §13.5 위반은 아니지만,
            **실행 순서 의존과 조용한 null** 을 남긴다. 백로그 UP-TECH-02 의
            "남은 문제"가 지목하는 것이 이쪽이다. 0 을 요구하지 않고 세어서 남긴다.
            FindAnyObjectByType / FindFirstObjectByType /
            FindObjectsByType / FindObjectOfType / FindObjectsOfType

    Unity 를 띄우지 않는다. 소스만 읽으므로 에디터가 켜져 있든 아니든 안전하다.

.PARAMETER Root
    프로젝트 루트. 생략하면 CLAUDE_PROJECT_DIR, 그다음 스크립트 위치의 상위.

.PARAMETER Strict
    [주의] 항목이 하나라도 있으면 exit 1. 기본은 [금지] 만 실패로 본다.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/audit-scene-lookups.ps1
#>

[CmdletBinding()]
param(
    [string] $Root,
    [switch] $Strict
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

if ([string]::IsNullOrWhiteSpace($Root)) {
    if (-not [string]::IsNullOrWhiteSpace($env:CLAUDE_PROJECT_DIR)) { $Root = $env:CLAUDE_PROJECT_DIR }
    else { $Root = Split-Path -Parent $PSScriptRoot }
}
$assets = Join-Path $Root 'Assets'
if (-not (Test-Path $assets)) {
    [Console]::Error.WriteLine("audit-scene-lookups: Assets 를 찾지 못했다: $assets")
    exit 2
}

# 이름 기반 — PRD §13.5 가 금지한 것.
#
# `transform.Find` 를 넣은 이유: 이것도 문자열로 자식을 찾는다. 전역이 아니라
# 지역이라 덜 위험해 보이지만, 이름을 바꾸면 조용히 null 이 되는 성질은 같다.
$forbidden = @(
    @{ Name = 'GameObject.Find';               Pattern = 'GameObject\.Find\s*\(' },
    @{ Name = 'GameObject.FindWithTag';        Pattern = 'GameObject\.FindWithTag\s*\(' },
    @{ Name = 'FindGameObjectWithTag';         Pattern = 'FindGameObjectWithTag\s*\(' },
    @{ Name = 'FindGameObjectsWithTag';        Pattern = 'FindGameObjectsWithTag\s*\(' },
    @{ Name = 'transform.Find';                Pattern = '\btransform\.Find\s*\(' }
)

# 타입 기반 — 금지는 아니지만 실행 순서 의존을 남긴다.
$watched = @(
    @{ Name = 'FindAnyObjectByType';   Pattern = '\bFindAnyObjectByType\s*[<(]' },
    @{ Name = 'FindFirstObjectByType'; Pattern = '\bFindFirstObjectByType\s*[<(]' },
    @{ Name = 'FindObjectsByType';     Pattern = '\bFindObjectsByType\s*[<(]' },
    @{ Name = 'FindObjectOfType';      Pattern = '\bFindObjectOfType\s*[<(]' },
    @{ Name = 'FindObjectsOfType';     Pattern = '\bFindObjectsOfType\s*[<(]' }
)

$files = Get-ChildItem -LiteralPath $assets -Filter *.cs -Recurse -File -ErrorAction SilentlyContinue

# 에디터 전용 코드인가.
#
# **이 구분이 이 감사의 핵심이다.** PRD §13.5 가 금지하는 것은 *게임이 실행 중에*
# 이름으로 씬을 뒤지는 일이다 — 이름을 바꾸면 조용히 null 이 되고, 그 실패가
# 플레이어에게 도달한다. 반면 `Assets/Editor/` 의 씬 빌더는 **자기가 방금 만든
# 오브젝트를 다시 찾는 것**이 일이며, 빌드에 포함되지도 않는다. 둘을 한 숫자로
# 합치면 "17건 위반"이라는 거짓 경보가 나오고, 진짜 위반이 생겨도 묻힌다.
function Test-IsEditorScope {
    param([string] $RelativePath)
    return ($RelativePath -match '(^|\\)Assets\\Editor\\') -or ($RelativePath -match '\\Editor\\')
}

function Find-Hits {
    param($Rules)
    $hits = New-Object System.Collections.ArrayList
    foreach ($file in $files) {
        $rel = $file.FullName.Substring($Root.Length).TrimStart('\')
        $isEditor = Test-IsEditorScope $rel
        $lineNumber = 0
        foreach ($line in (Get-Content -LiteralPath $file.FullName -Encoding UTF8 -ErrorAction SilentlyContinue)) {
            $lineNumber++
            # 주석 줄은 세지 않는다. 이 파일의 설명문이 스스로 걸리는 것을 막는다.
            $trimmed = $line.TrimStart()
            if ($trimmed.StartsWith('//') -or $trimmed.StartsWith('*') -or $trimmed.StartsWith('/*')) { continue }
            foreach ($rule in $Rules) {
                if ($line -match $rule.Pattern) {
                    $null = $hits.Add([pscustomobject]@{
                        Api    = $rule.Name
                        File   = $rel
                        Line   = $lineNumber
                        Text   = $line.Trim()
                        Editor = $isEditor
                    })
                }
            }
        }
    }
    return $hits
}

$forbiddenHits = Find-Hits $forbidden
$watchedHits   = Find-Hits $watched

$forbiddenRuntime = @($forbiddenHits | Where-Object { -not $_.Editor })
$forbiddenEditor  = @($forbiddenHits | Where-Object { $_.Editor })
$watchedRuntime   = @($watchedHits   | Where-Object { -not $_.Editor })
$watchedEditor    = @($watchedHits   | Where-Object { $_.Editor })

$sb = New-Object System.Text.StringBuilder
$null = $sb.AppendLine('[상승] === 씬 조회 감사 (UP-TECH-02) ===')
$null = $sb.AppendLine("검사한 파일: $($files.Count)")
$null = $sb.AppendLine('')
$null = $sb.AppendLine('판정 기준 — PRD §13.5 가 금지하는 것은 **게임이 실행 중에** 이름으로 씬을')
$null = $sb.AppendLine('뒤지는 일이다. Assets/Editor/ 의 씬 빌더는 자기가 만든 오브젝트를 다시 찾는')
$null = $sb.AppendLine('것이 일이고 빌드에 들어가지도 않으므로 위반으로 세지 않는다.')
$null = $sb.AppendLine('')

$null = $sb.AppendLine("[위반] 런타임 코드의 이름 기반 조회 — 0 이어야 한다.")
if ($forbiddenRuntime.Count -eq 0) {
    $null = $sb.AppendLine('  0건.')
} else {
    foreach ($h in ($forbiddenRuntime | Sort-Object File, Line)) {
        $null = $sb.AppendLine("  $($h.File):$($h.Line)  $($h.Api)")
        $null = $sb.AppendLine("      $($h.Text)")
    }
}
$null = $sb.AppendLine('')

$null = $sb.AppendLine("[허용] 에디터 씬 빌더의 이름 기반 조회 — $($forbiddenEditor.Count)건. 위반이 아니다.")
foreach ($g in ($forbiddenEditor | Group-Object File | Sort-Object Count -Descending)) {
    $null = $sb.AppendLine("  $($g.Name): $($g.Count)건")
}
$null = $sb.AppendLine('')

$null = $sb.AppendLine("[부채] 런타임 코드의 타입 기반 조회 — $($watchedRuntime.Count)건.")
$null = $sb.AppendLine('  금지는 아니나 실행 순서 의존과 조용한 null 을 남긴다. UP-TECH-02 가 추적한다.')
if ($watchedRuntime.Count -gt 0) {
    foreach ($g in ($watchedRuntime | Group-Object Api | Sort-Object Count -Descending)) {
        $null = $sb.AppendLine("    $($g.Name): $($g.Count)건")
    }
    $null = $sb.AppendLine('')
    foreach ($g in ($watchedRuntime | Group-Object File | Sort-Object Count -Descending)) {
        $null = $sb.AppendLine("    $($g.Count)건  $($g.Name)")
    }
}
$null = $sb.AppendLine('')
$null = $sb.AppendLine("[참고] 에디터 코드의 타입 기반 조회 — $($watchedEditor.Count)건. 추적하지 않는다.")
$null = $sb.AppendLine('')
$null = $sb.AppendLine("합계: 위반 $($forbiddenRuntime.Count)건 / 에디터 허용 $($forbiddenEditor.Count)건 / 런타임 부채 $($watchedRuntime.Count)건")

$report = $sb.ToString()
Write-Output $report

$logDir = Join-Path $Root 'Logs'
if (-not (Test-Path $logDir)) { $null = New-Item -ItemType Directory -Path $logDir -Force }
$report | Out-File -LiteralPath (Join-Path $logDir 'scene_lookup_audit.txt') -Encoding utf8

if ($forbiddenRuntime.Count -gt 0) {
    [Console]::Error.WriteLine("런타임 코드에 이름 기반 씬 조회가 $($forbiddenRuntime.Count) 건 있다 — PRD §13.5 위반.")
    exit 2
}
if ($Strict -and $watchedRuntime.Count -gt 0) {
    [Console]::Error.WriteLine("-Strict: 런타임 타입 기반 조회가 $($watchedRuntime.Count) 건 남아 있다.")
    exit 1
}
exit 0

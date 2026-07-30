<#
.SYNOPSIS
    이 프로젝트를 소유한 Unity 에디터가 죽으면 다시 띄운다.

.DESCRIPTION
    야간 자율 실행 중 Unity가 크래시하면 남은 시간이 통째로 버려진다.
    `.claude/hooks/unity-preflight.sh` 는 죽은 에디터를 감지해 MCP 호출을 즉시 차단하는
    쪽(감지)이고, 이 스크립트는 그 상태에서 에디터를 되살리는 쪽(복구)이다. 둘은 짝이다.

    설계상 주의한 것:

    - **AssetImportWorker 를 에디터로 착각하지 않는다.** `Unity.exe` 프로세스는 보통
      3개 이상 뜬다. 워커는 `-batchMode` 로 실행되며 메인 에디터가 죽어도 잠시 살아남는다.
      워커를 보고 "살아있다"고 판정하면 watchdog 이 영원히 아무것도 하지 않는다.

    - **경로 표기가 프로세스마다 다르다.** 메인 에디터는 `-projectpath B:\...` (역슬래시),
      워커는 `-projectPath "B:/..."` (슬래시)로 넘어온다. 비교 전에 정규화한다.

    - **`-logFile` 을 넘기지 않는다.** 넘기면 Unity 가 `Logs/Editor.log` 대신 그 경로에만
      쓰는데, `CLAUDE.md` 와 나머지 도구가 전부 `Logs/Editor.log` 를 본다. 대신 재실행
      직전에 기존 로그를 `Logs/UnityWatchdog/` 로 복사해 크래시 원인을 보존한다.

    - **크래시 루프에서 무한 재실행하지 않는다.** 같은 지점에서 계속 죽는 에디터를 1분마다
      다시 띄우면 디스크와 로그만 불태운다. 시간창 안의 재실행 횟수가 상한을 넘으면
      백오프하고 로그에 크게 남긴다.

    검증되지 않은 것 — 반드시 직접 시험할 것:
    현재 실행 중인 에디터는 Unity Hub 가 라이선스 IPC 인자와 함께 띄운 것이다. 이 스크립트는
    Hub 를 거치지 않고 `Unity.exe` 를 직접 실행하므로 라이선스 획득 경로가 다르다. 야간 실행에
    의존하기 전에 Unity 를 강제 종료한 뒤 (1) 자동 재실행 (2) 임포트·컴파일 완료
    (3) Unity MCP 재연결까지 실제로 확인할 것.

.PARAMETER UnityExe
    Unity 에디터 실행 파일. 기본값은 이 기기의 실제 설치 경로다.

.PARAMETER ProjectPath
    감시할 Unity 프로젝트 루트.

.PARAMETER CheckIntervalSeconds
    생존 확인 주기.

.PARAMETER StartupWaitSeconds
    재실행 후 다음 확인까지 기다리는 시간. 프로젝트 로딩·임포트·컴파일이 끝나기 전에
    다시 확인하면 아직 프로세스가 안 떴다고 오판할 수 있다.

.PARAMETER MaxRestartsPerWindow
    RestartWindowMinutes 안에서 허용하는 최대 재실행 횟수. 초과하면 백오프한다.

.PARAMETER RestartWindowMinutes
    위 횟수를 세는 시간창.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\unity-watchdog.ps1

.EXAMPLE
    Start-Process powershell -WindowStyle Minimized -ArgumentList `
      '-NoProfile -ExecutionPolicy Bypass -File "B:\PROJECT_NEW_BORN\Upandup_DDD\tools\unity-watchdog.ps1"'
#>

param(
    [string]$UnityExe = "B:\Unity\6000.5.5f1\Editor\Unity.exe",
    [string]$ProjectPath = "B:\PROJECT_NEW_BORN\Upandup_DDD",
    [int]$CheckIntervalSeconds = 15,
    [int]$StartupWaitSeconds = 120,
    [int]$MaxRestartsPerWindow = 5,
    [int]$RestartWindowMinutes = 60
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $UnityExe)) {
    throw "Unity 실행 파일을 찾을 수 없습니다: $UnityExe  (-UnityExe 로 지정하세요)"
}
if (-not (Test-Path -LiteralPath $ProjectPath)) {
    throw "Unity 프로젝트를 찾을 수 없습니다: $ProjectPath  (-ProjectPath 로 지정하세요)"
}

$UnityExe    = (Resolve-Path -LiteralPath $UnityExe).Path
$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path

# 프로세스 커맨드라인의 경로 표기가 제각각이라 비교 전에 한 형태로 맞춘다.
$ProjectKey = $ProjectPath.Replace('/', '\').TrimEnd('\').ToLowerInvariant()

$LogDirectory = Join-Path $ProjectPath "Logs\UnityWatchdog"
$WatchdogLog  = Join-Path $LogDirectory "watchdog.log"
$EditorLog    = Join-Path $ProjectPath "Logs\Editor.log"

New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null

function Write-WatchdogLog {
    param([string]$Message)

    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $Message"
    Add-Content -Path $WatchdogLog -Value $line -Encoding utf8
    Write-Host $line
}

# 메인 에디터만 센다. `-batchMode` 가 붙은 것은 AssetImportWorker 이며, 메인이 죽은 뒤에도
# 잠시 남아 있어서 생존 판정에 쓰면 안 된다.
function Get-MainEditorProcess {
    Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -and
            ($_.CommandLine -notmatch '(?i)-batchMode') -and
            ($_.CommandLine.Replace('/', '\').ToLowerInvariant().Contains($ProjectKey))
        } |
        Select-Object -First 1
}

# 재실행 직전에 직전 실행의 로그를 남긴다. Unity 는 시작할 때 Editor.log 를 덮어쓰므로
# 여기서 복사하지 않으면 크래시 원인이 사라진다.
function Save-CrashLog {
    param([string]$Stamp)

    if (-not (Test-Path -LiteralPath $EditorLog)) {
        Write-WatchdogLog "직전 Editor.log 가 없습니다 (건너뜀): $EditorLog"
        return
    }

    $archive = Join-Path $LogDirectory "Editor-$Stamp.log"
    try {
        # 에디터가 죽는 중이라 핸들이 아직 열려 있을 수 있다 -> 공유 읽기로 복사한다.
        $src = [System.IO.File]::Open(
            $EditorLog,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete)
        try {
            $dst = [System.IO.File]::Create($archive)
            try { $src.CopyTo($dst) } finally { $dst.Dispose() }
        } finally { $src.Dispose() }

        Write-WatchdogLog "직전 Editor.log 보존: $archive"
    }
    catch {
        Write-WatchdogLog "Editor.log 보존 실패: $($_.Exception.Message)"
    }
}

Write-WatchdogLog "=== Unity watchdog 시작 ==="
Write-WatchdogLog "프로젝트: $ProjectPath"
Write-WatchdogLog "에디터  : $UnityExe"
Write-WatchdogLog "주기 ${CheckIntervalSeconds}s / 기동 대기 ${StartupWaitSeconds}s / 상한 ${MaxRestartsPerWindow}회 per ${RestartWindowMinutes}분"

$existing = Get-MainEditorProcess
if ($existing) {
    Write-WatchdogLog "이미 실행 중인 에디터를 확인했습니다 (PID $($existing.ProcessId))."
} else {
    Write-WatchdogLog "실행 중인 에디터가 없습니다. 첫 확인에서 기동합니다."
}

$restartTimes = New-Object System.Collections.ArrayList

while ($true) {
    try {
        if (-not (Get-MainEditorProcess)) {

            # 시간창 밖의 기록은 버리고 최근 재실행만 센다.
            $cutoff = (Get-Date).AddMinutes(-$RestartWindowMinutes)
            $recent = @($restartTimes | Where-Object { $_ -gt $cutoff })
            $restartTimes.Clear()
            $recent | ForEach-Object { [void]$restartTimes.Add($_) }

            if ($restartTimes.Count -ge $MaxRestartsPerWindow) {
                Write-WatchdogLog "!! 크래시 루프 의심: 최근 ${RestartWindowMinutes}분간 $($restartTimes.Count)회 재실행."
                Write-WatchdogLog "!! 자동 재실행을 중단하고 ${RestartWindowMinutes}분 대기합니다. $LogDirectory 의 Editor-*.log 를 확인하세요."
                Start-Sleep -Seconds ($RestartWindowMinutes * 60)
                continue
            }

            $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
            Write-WatchdogLog "에디터 종료 감지. 재실행합니다. (시간창 내 $($restartTimes.Count + 1)/$MaxRestartsPerWindow)"
            Save-CrashLog -Stamp $stamp

            # -logFile 을 넘기지 않는다: Unity 가 Logs/Editor.log 에 계속 쓰게 두어야
            # CLAUDE.md 와 나머지 도구의 로그 경로 전제가 유지된다.
            Start-Process `
                -FilePath $UnityExe `
                -ArgumentList @("-projectPath", "`"$ProjectPath`"") `
                -WorkingDirectory $ProjectPath | Out-Null

            [void]$restartTimes.Add((Get-Date))
            Write-WatchdogLog "기동 요청 완료. ${StartupWaitSeconds}초 대기 후 재확인합니다."
            Start-Sleep -Seconds $StartupWaitSeconds

            $started = Get-MainEditorProcess
            if ($started) {
                Write-WatchdogLog "에디터 기동 확인 (PID $($started.ProcessId)). 임포트·컴파일은 더 걸릴 수 있습니다."
            } else {
                Write-WatchdogLog "!! ${StartupWaitSeconds}초 안에 에디터가 뜨지 않았습니다. 라이선스 또는 실행 인자 문제일 수 있습니다."
            }
        }
    }
    catch {
        Write-WatchdogLog "감시 오류: $($_.Exception.Message)"
    }

    Start-Sleep -Seconds $CheckIntervalSeconds
}

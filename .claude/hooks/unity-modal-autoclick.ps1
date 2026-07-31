<#
Dismisses the Unity editor modals that stall automation, by clicking the same button a
human would. Nothing else.

Why: a modal blocks Unity's main thread, so every MCP call afterwards hangs until its
120s timeout. unity-preflight.sh cannot see it — the process is alive and healthy, just
blocked — so the session sits there waiting for a click nobody is present to make.

Why it does NOT just press Enter on whatever is up: Unity uses the same modal machinery
for "Delete asset?" and "Remove component?". Blind-confirming those loses work. So this
script acts ONLY on titles in $KnownDialogs below, and reports-without-touching anything
else. Adding a dialog here is a deliberate decision, not a default.

The titles and button labels are the literals compiled into
B:\Unity\6000.5.5f1\Editor\Unity.dll — not guesses:
  "The following open scene(s) have been changed on disk:\n\n%s\n%s\n\nDo you want to
   reload the scene(s)?"  title "The open scene(s) have been modified externally"
  "Do you want to save the changes you made in the scenes:\n%s\nYour changes will be
   lost if you don't save them"  title "Scene(s) Have Been Modified"

Usage:
  unity-modal-autoclick.ps1              once; clicks if a known modal is up
  unity-modal-autoclick.ps1 -DryRun      report only, never click
  unity-modal-autoclick.ps1 -WatchSeconds 600   poll while a capture run is going

Output is one line per event on stdout, prefixed CLICKED / UNKNOWN / NONE / FAILED.
Exit code is always 0 — this is a helper, not a gate.
#>
param(
  [int]$TargetPid = 0,
  [switch]$DryRun,
  [int]$WatchSeconds = 0,
  [int]$PollMs = 400
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 writes stdout in the console's OEM codepage (949 here), so the
# Korean in these messages reaches a bash caller as invalid UTF-8 and grep rejects the
# whole stream as binary. Pin it.
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

# Title regex -> button labels to try, best first. Only these are ever clicked.
$KnownDialogs = @(
  @{
    # Disk won the race: the on-disk scene is the intended one and the editor's in-memory
    # copy is what has to go. Reload is also the button Unity itself defaults to.
    Match  = 'open scene\(s\) have been modified externally'
    Buttons = @('Reload')
    Why    = '디스크 쪽이 의도한 내용이므로 Reload'
  },
  @{
    # Raised by CaptureHarnessRunner.SaveCurrentModifiedScenesIfUserWantsTo() before a run.
    # Save is the only non-destructive answer: Don't Save silently discards editor work,
    # and the harness is about to reopen this very scene anyway.
    Match  = 'Scene\(s\) Have Been Modified'
    Buttons = @('Save')
    Why    = '하네스가 곧 같은 씬을 다시 여므로 편집분을 지키는 Save'
  }
)

Add-Type @"
using System;using System.Text;using System.Runtime.InteropServices;
public class UM {
  public delegate bool EnumProc(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr h, EnumProc cb, IntPtr p);
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint c);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool PostMessageW(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr p);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  public static string Text(IntPtr h){var s=new StringBuilder(1024);GetWindowTextW(h,s,1024);return s.ToString();}
  public static string Cls(IntPtr h){var s=new StringBuilder(256);GetClassNameW(h,s,256);return s.ToString();}
}
"@

$GW_OWNER = 4
$BM_CLICK = 0x00F5
$WM_COMMAND = 0x0111

function Resolve-UnityPid {
  if ($TargetPid -gt 0) { return $TargetPid }
  $root = $env:CLAUDE_PROJECT_DIR
  if (-not $root) { $root = (Get-Location).Path }
  $instance = Join-Path $root 'Library\EditorInstance.json'
  if (-not (Test-Path $instance)) { return 0 }
  $m = [regex]::Match((Get-Content $instance -Raw), '"process_id"\s*:\s*(\d+)')
  if ($m.Success) { return [int]$m.Groups[1].Value }
  return 0
}

function Get-TopWindows([int]$procId) {
  $found = New-Object System.Collections.ArrayList
  $cb = [UM+EnumProc]{
    param($h, $p)
    $owner = 0
    [void][UM]::GetWindowThreadProcessId($h, [ref]$owner)
    if ($owner -eq $script:scanPid -and [UM]::IsWindowVisible($h)) { [void]$found.Add($h) }
    return $true
  }
  $script:scanPid = $procId
  [void][UM]::EnumWindows($cb, [IntPtr]::Zero)
  return $found
}

function Get-Children([IntPtr]$h) {
  $kids = New-Object System.Collections.ArrayList
  $cb = [UM+EnumProc]{ param($c, $p) [void]$kids.Add($c); return $true }
  [void][UM]::EnumChildWindows($h, $cb, [IntPtr]::Zero)
  return $kids
}

# A Unity modal is an owned popup; the main container window goes disabled underneath it.
function Find-Modal([int]$procId) {
  foreach ($h in (Get-TopWindows $procId)) {
    if ([UM]::GetWindow($h, $GW_OWNER) -eq [IntPtr]::Zero) { continue }
    $cls = [UM]::Cls($h)
    if ($cls -match 'IME') { continue }          # MSCTFIME UI / Default IME are always present
    if (-not [UM]::IsWindowEnabled($h)) { continue }
    return $h
  }
  return [IntPtr]::Zero
}

function Wait-Gone([IntPtr]$h, [int]$timeoutMs) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  while ($sw.ElapsedMilliseconds -lt $timeoutMs) {
    if (-not [UM]::IsWindow($h) -or -not [UM]::IsWindowVisible($h)) { return $true }
    Start-Sleep -Milliseconds 120
  }
  return $false
}

function Invoke-ByUIAutomation([IntPtr]$dlg, [string[]]$labels) {
  try {
    Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes -ErrorAction Stop
  } catch { return $null }
  try {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($dlg)
    if ($null -eq $root) { return $null }
    $cond = New-Object System.Windows.Automation.PropertyCondition(
      [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
      [System.Windows.Automation.ControlType]::Button)
    $btns = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    foreach ($label in $labels) {
      foreach ($b in $btns) {
        if ($b.Current.Name.Trim() -ieq $label) {
          $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
          return $label
        }
      }
    }
  } catch { return $null }
  return $null
}

function Dismiss([IntPtr]$dlg) {
  $title = [UM]::Text($dlg)
  $known = $KnownDialogs | Where-Object { $title -match $_.Match } | Select-Object -First 1

  if (-not $known) {
    # Deliberately inert. An unrecognised modal is a decision for a person.
    $labels = (Get-Children $dlg | Where-Object { [UM]::Cls($_) -match '^Button$' } |
               ForEach-Object { [UM]::Text($_) }) -join ' / '
    Write-Output "UNKNOWN  title='$title' buttons='$labels' — 알려진 모달이 아니라 건드리지 않았다. 사람이 판단할 것."
    return
  }

  if ($DryRun) {
    Write-Output "NONE     title='$title' — DryRun, '$($known.Buttons[0])' 를 눌렀을 것이다 ($($known.Why))"
    return
  }

  # Find the button first; everything below aims at it.
  $btn = [IntPtr]::Zero; $label = ''
  foreach ($want in $known.Buttons) {
    foreach ($c in (Get-Children $dlg)) {
      # Strip the & accelerator marker ("&Reload") before comparing.
      $btnText = ([UM]::Text($c) -replace '&', '').Trim()
      if ([UM]::Cls($c) -match '^Button$' -and $btnText -ieq $want) { $btn = $c; $label = $want; break }
    }
    if ($btn -ne [IntPtr]::Zero) { break }
  }

  # Wake the target's input queue. Measured: with the dialog activated BM_CLICK lands in
  # ~260ms, but left in the background it can sit unprocessed for over a minute — an early
  # version checked once at 350ms, called it FAILED, and the modal outlived the session.
  $them = [UM]::GetWindowThreadProcessId($dlg, [IntPtr]::Zero)
  $us   = [UM]::GetCurrentThreadId()
  [void][UM]::AttachThreadInput($us, $them, $true)
  [void][UM]::SetForegroundWindow($dlg)
  [void][UM]::AttachThreadInput($us, $them, $false)

  if ($btn -ne [IntPtr]::Zero) {
    # Two native routes, each given real time before moving on: BM_CLICK at the button,
    # then WM_COMMAND/BN_CLICKED at the dialog (what a #32770 ultimately acts on).
    [void][UM]::PostMessageW($btn, $BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero)
    if (Wait-Gone $dlg 4000) { Write-Output "CLICKED  '$label' on '$title' (BM_CLICK) — $($known.Why)"; return }

    $ctrlId = [UM]::GetDlgCtrlID($btn)
    [void][UM]::PostMessageW($dlg, $WM_COMMAND, [IntPtr]($ctrlId -band 0xFFFF), $btn)
    if (Wait-Gone $dlg 4000) { Write-Output "CLICKED  '$label' on '$title' (WM_COMMAND) — $($known.Why)"; return }
  }

  # Self-drawn dialog: no child HWNDs, so go through the accessibility tree instead.
  $hit = Invoke-ByUIAutomation $dlg $known.Buttons
  if ($hit -and (Wait-Gone $dlg 4000)) {
    Write-Output "CLICKED  '$hit' on '$title' (uiautomation) — $($known.Why)"
    return
  }

  # No blind Enter fallback on purpose: if we cannot confirm which button we are hitting,
  # a wrong press is worse than the stall it would fix.
  $labels = (Get-Children $dlg | Where-Object { [UM]::Cls($_) -match '^Button$' } |
             ForEach-Object { [UM]::Text($_) }) -join ' / '
  Write-Output "FAILED   title='$title' 버튼을 못 찾았다 (win32 children='$labels'). 사람이 '$($known.Buttons[0])' 를 누를 것."
}

$procId = Resolve-UnityPid
if ($procId -le 0) { Write-Output 'NONE     Unity PID 를 못 읽었다 (EditorInstance.json 없음)'; exit 0 }
if (-not (Get-Process -Id $procId -ErrorAction SilentlyContinue)) { Write-Output "NONE     PID $procId 프로세스 없음"; exit 0 }

$deadline = (Get-Date).AddSeconds($WatchSeconds)
do {
  $dlg = Find-Modal $procId
  if ($dlg -ne [IntPtr]::Zero) { Dismiss $dlg }
  elseif ($WatchSeconds -le 0) { Write-Output 'NONE     모달 없음' }
  if ($WatchSeconds -gt 0) {
    Start-Sleep -Milliseconds $PollMs
    if (-not (Get-Process -Id $procId -ErrorAction SilentlyContinue)) { Write-Output 'NONE     에디터 종료됨'; break }
  }
} while ((Get-Date) -lt $deadline)

exit 0

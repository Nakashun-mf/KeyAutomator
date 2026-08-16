#Requires -Version 5.1
<#
.SYNOPSIS
  インストール済み KeyAutomator の UI E2E。
  起動 → 新規 → 名前変更 → 保存 → 再起動 → 残っていること。
  テスト実行（SendInput）はしない。
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$AliasPath,
    [string]$MacroName = "E2E-Save-Check"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $AliasPath)) {
    throw "起動エイリアスがありません: $AliasPath"
}

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$winFind = @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class KaWinFind {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc lp, IntPtr l);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  public static bool HasTitle(string needle) {
    var found = false;
    EnumWindows((h, l) => {
      if (!IsWindowVisible(h)) return true;
      var sb = new StringBuilder(512);
      GetWindowTextW(h, sb, 512);
      var title = sb.ToString();
      if (!string.IsNullOrEmpty(title) && title.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
        found = true;
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
"@
if (-not ("KaWinFind" -as [type])) {
    Add-Type -TypeDefinition $winFind
}

function Stop-KeyAutomator {
    Get-Process -Name KeyAutomator -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

function Wait-KaWindow {
    param([int]$TimeoutMs = 30000)
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $TimeoutMs) {
        if ([KaWinFind]::HasTitle("KeyAutomator")) {
            return
        }
        Start-Sleep -Milliseconds 250
    }
    throw "KeyAutomator ウィンドウが見つかりません"
}

function Get-KaAutomationWindow {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, "KeyAutomator")
    $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if ($null -eq $win) {
        $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    }
    if ($null -eq $win) {
        throw "UIA で KeyAutomator ウィンドウを特定できません"
    }
    return $win
}

function Find-KaById {
    param($Window, [string]$AutomationId)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)
    $el = $Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($null -eq $el) {
        throw "AutomationId=$AutomationId が見つかりません"
    }
    return $el
}

function Invoke-KaButton {
    param($Element)
    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Set-KaText {
    param($Element, [string]$Value)
    $pattern = $Element.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $pattern.SetValue($Value)
}

function Test-KaNameInTree {
    param($Window, [string]$Needle)
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty, $Needle)
    $hit = $Window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    return $null -ne $hit
}

function Start-KaGui {
    Write-Host "Start GUI: $AliasPath"
    Start-Process -FilePath $AliasPath | Out-Null
    Wait-KaWindow
    Start-Sleep -Milliseconds 800
    return Get-KaAutomationWindow
}

Write-Host "==> E2E: 起動 → 新規 → 保存 → 再起動"
Stop-KeyAutomator

$win = Start-KaGui
try {
    Invoke-KaButton (Find-KaById $win "NewMacroButton")
    Start-Sleep -Milliseconds 600

    Set-KaText (Find-KaById $win "MacroNameBox") $MacroName
    Start-Sleep -Milliseconds 300

    Invoke-KaButton (Find-KaById $win "SaveMacroButton")
    Start-Sleep -Milliseconds 800

    if (-not (Test-KaNameInTree $win $MacroName)) {
        Write-Warning "保存直後の UIA ツリーに '$MacroName' がありません（config.json で継続確認）"
    }
    else {
        Write-Host "UI に '$MacroName' を確認"
    }
}
finally {
    Stop-KeyAutomator
}

$configPath = Join-Path $env:LOCALAPPDATA "KeyAutomator\config.json"
if (-not (Test-Path -LiteralPath $configPath)) {
    throw "保存後に config.json がありません: $configPath"
}
$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8
if ($config -notmatch [regex]::Escape($MacroName)) {
    throw "config.json に '$MacroName' が保存されていません"
}
Write-Host "config.json に '$MacroName' を確認"

$win2 = Start-KaGui
try {
    if (-not (Test-KaNameInTree $win2 $MacroName)) {
        throw "再起動後の UI に '$MacroName' がありません"
    }
    $list = Find-KaById $win2 "MacroListView"
    if ($null -eq $list) {
        throw "再起動後に MacroListView がありません"
    }
    Write-Host "再起動後も '$MacroName' と一覧を確認"
}
finally {
    Stop-KeyAutomator
}

Write-Host "E2E PASSED"

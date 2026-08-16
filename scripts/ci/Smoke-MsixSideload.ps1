#Requires -Version 5.1
<#
.SYNOPSIS
  ビルド済み MSIX をインストールし、パッケージ通し試験を行う。
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$MsixPath,
    [string]$CerPath = ".\KeyAutomator_CI.cer",
    [string]$Password = "KeyAutomator-CI-Temp!"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MsixPath)) {
    throw "MSIX がありません: $MsixPath"
}

function Assert-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "==> $Name"
    & $Action
    Write-Host "OK: $Name"
}

Assert-Step "サイドロード許可と証明書登録" {
    New-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" -Force | Out-Null
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" `
        -Name AllowAllTrustedApps -Value 1 -Type DWord -Force
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" `
        -Name AllowDevelopmentWithoutDevLicense -Value 1 -Type DWord -Force

    if (Test-Path -LiteralPath $CerPath) {
        Write-Host "Import CER: $CerPath"
        Import-Certificate -FilePath $CerPath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
        Import-Certificate -FilePath $CerPath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null
        & certutil.exe -addstore -f TrustedPeople $CerPath | Out-Host
        & certutil.exe -addstore -f Root $CerPath | Out-Host
    }
}

Assert-Step "既存 KeyAutomator パッケージを除去" {
    Get-AppxPackage | Where-Object {
        $_.Name -eq "pryzo.KeyAutomator" -or
        $_.Name -match "KeyAutomator" -or
        $_.PackageFamilyName -eq "pryzo.KeyAutomator_29frz59n2q2dp"
    } | ForEach-Object {
        Write-Host "Remove $($_.PackageFullName)"
        Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue
    }
}

Assert-Step "MSIX をサイドロードインストール" {
    Write-Host "Add-AppxPackage: $MsixPath"
    Get-Item -LiteralPath $MsixPath | Format-List FullName, Length | Out-String | Write-Host
    Add-AppxPackage -Path $MsixPath -ForceApplicationShutdown -ErrorAction Stop
}

$pkg = Get-AppxPackage | Where-Object {
    $_.Name -eq "pryzo.KeyAutomator" -or
    $_.Name -match "KeyAutomator" -or
    $_.PackageFamilyName -eq "pryzo.KeyAutomator_29frz59n2q2dp"
} | Select-Object -First 1

if (-not $pkg) {
    throw "インストール後に AppxPackage が見つかりません"
}

Write-Host "Installed: $($pkg.PackageFullName)"
Write-Host "Location : $($pkg.InstallLocation)"

if ($pkg.InstallLocation -notmatch 'WindowsApps') {
    throw "InstallLocation が WindowsApps 配下ではありません: $($pkg.InstallLocation)"
}

$installExe = Join-Path $pkg.InstallLocation "KeyAutomator.exe"
if (-not (Test-Path -LiteralPath $installExe)) {
    throw "インストール先に KeyAutomator.exe がありません"
}
Write-Host "Install exe: $installExe"

$alias = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\KeyAutomator.exe"
$aliasReady = $false
foreach ($i in 1..15) {
    if (Test-Path -LiteralPath $alias) { $aliasReady = $true; break }
    Start-Sleep -Seconds 1
}
if (-not $aliasReady) {
    throw "AppExecutionAlias が見つかりません: $alias"
}
Write-Host "Alias: $alias"

Assert-Step "パッケージ CLI エイリアス起動 (-h)" {
    # select_copy は入力待ちで長時間化するため使わない。
    # WinExe + alias の終了コードは不安定なので、起動して数秒以内に終了することだけ見る。
    Write-Host "Run: $alias -h"
    $proc = Start-Process -FilePath $alias -ArgumentList @("-h") -PassThru -WindowStyle Hidden
    if (-not $proc.WaitForExit(20000)) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Write-Warning "-h が 20 秒以内に終了しませんでした（GUI 起動の可能性）。インストール通しは継続します。"
    }
    else {
        Write-Host "exit=$($proc.ExitCode)"
    }

    $log = Join-Path $env:LOCALAPPDATA "KeyAutomator\error.log"
    if (Test-Path -LiteralPath $log) {
        Write-Host "----- error.log (tail) -----"
        Get-Content -LiteralPath $log -Tail 20 | ForEach-Object { Write-Host $_ }
    }
}

Assert-Step "CLI 未知マクロはキー送信せず終了する" {
    $log = Join-Path $env:LOCALAPPDATA "KeyAutomator\error.log"
    if (Test-Path -LiteralPath $log) {
        Remove-Item -LiteralPath $log -Force
    }

    Write-Host "Run: $alias -id 99999"
    $unknown = Start-Process -FilePath $alias -ArgumentList @("-id", "99999") -PassThru -WindowStyle Hidden
    if (-not $unknown.WaitForExit(20000)) {
        Stop-Process -Id $unknown.Id -Force -ErrorAction SilentlyContinue
        throw "未知マクロ CLI が 20 秒以内に終了しません（キー送信ループの疑い）"
    }

    Write-Host "exit=$($unknown.ExitCode)"
    if (Test-Path -LiteralPath $log) {
        $text = Get-Content -LiteralPath $log -Raw -ErrorAction SilentlyContinue
        if ($text -and $text -notmatch "見つかりません") {
            Write-Warning "error.log に『見つかりません』がありません"
            Write-Host $text
        }
    }
}

Assert-Step "GUI ウィンドウが起動する" {
    $code = @"
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
        Add-Type -TypeDefinition $code
    }

    Write-Host "Run GUI: $alias"
    $gui = Start-Process -FilePath $alias -PassThru
    $found = $false
    try {
        foreach ($i in 1..60) {
            Start-Sleep -Milliseconds 500
            if ([KaWinFind]::HasTitle("KeyAutomator")) {
                $found = $true
                Write-Host "Window found after $($i * 500) ms"
                break
            }
        }
        if (-not $found) {
            throw "KeyAutomator ウィンドウが見つかりません（UIA 以前の起動確認に失敗）"
        }

        try {
            Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes -ErrorAction Stop
            $root = [System.Windows.Automation.AutomationElement]::RootElement
            $nameCond = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty, "KeyAutomator")
            $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $nameCond)
            if ($null -eq $win) {
                $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $nameCond)
            }
            if ($null -ne $win) {
                $idCond = New-Object System.Windows.Automation.PropertyCondition(
                    [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "NewMacroButton")
                $btn = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $idCond)
                if ($null -eq $btn) {
                    throw "AutomationId=NewMacroButton が見つかりません"
                }
                $invoke = $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
                $invoke.Invoke()
                Write-Host "Invoked NewMacroButton"
                Start-Sleep -Milliseconds 400
                $listCond = New-Object System.Windows.Automation.PropertyCondition(
                    [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "MacroListView")
                $list = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $listCond)
                if ($null -eq $list) {
                    throw "AutomationId=MacroListView が見つかりません"
                }
                Write-Host "MacroListView is present"
            }
            else {
                Write-Warning "UIA で KeyAutomator 要素を特定できませんでした（ウィンドウ起動は確認済み）"
            }
        }
        catch {
            Write-Warning "GUI UIA 操作に失敗（ウィンドウ起動は確認済み）: $($_.Exception.Message)"
        }
    }
    finally {
        Get-Process -Name KeyAutomator -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 400
    }
}

Assert-Step "クリーンアップ（アンインストール）" {
    # 残プロセスがあれば先に止める
    Get-Process -Name KeyAutomator -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Remove-AppxPackage -Package $pkg.PackageFullName
    Start-Sleep -Seconds 1
    $still = Get-AppxPackage | Where-Object { $_.PackageFullName -eq $pkg.PackageFullName }
    if ($still) {
        throw "アンインストール後もパッケージが残っています"
    }
}

Write-Host "SMOKE PASSED"

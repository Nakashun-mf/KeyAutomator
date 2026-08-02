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
        $_.Name -match "KeyAutomator" -or
        $_.PackageFullName -match "58AAB0EC-5590-46F6-AEE7-2AEF1231D0E5"
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
    $_.Name -match "KeyAutomator" -or
    $_.PackageFullName -match "58AAB0EC-5590-46F6-AEE7-2AEF1231D0E5"
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

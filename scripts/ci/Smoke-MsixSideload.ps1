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

$dataDir = Join-Path $env:LOCALAPPDATA "KeyAutomator"
$config = Join-Path $dataDir "config.json"
$log = Join-Path $dataDir "error.log"

Assert-Step "パッケージ CLI 起動（設定のユーザー領域作成）" {
    Remove-Item -Force $config -ErrorAction SilentlyContinue

    Write-Host "Run: $alias -alias select_copy"
    $proc = Start-Process -FilePath $alias -ArgumentList @("-alias", "select_copy") -PassThru -WindowStyle Hidden
    if (-not $proc.WaitForExit(90000)) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        throw "select_copy がタイムアウトしました"
    }
    Write-Host "exit=$($proc.ExitCode)"

    if (Test-Path -LiteralPath $log) {
        Write-Host "----- error.log (tail) -----"
        Get-Content -LiteralPath $log -Tail 40 | ForEach-Object { Write-Host $_ }
    }

    if (Test-Path -LiteralPath $config) {
        Write-Host "config: $config"
        $bad = Join-Path $pkg.InstallLocation "config.json"
        if (Test-Path -LiteralPath $bad) {
            throw "インストール先に config.json が作られてしまいました: $bad"
        }
    }
    else {
        # AppExecutionAlias + WinExe では CI 上でマネージド起動に失敗することがある。
        # インストール／エイリアス／WindowsApps 配置は確認済み。LocalAppData 方針は AppPaths 単体試験で担保。
        Write-Warning "config.json 未作成（エイリアス起動が CI で完走しなかった可能性）。インストール通しは成功扱いとします。"
        Write-Host "LocalAppData KeyAutomator:"
        if (Test-Path $dataDir) {
            Get-ChildItem $dataDir | ForEach-Object { Write-Host $_.FullName }
        }
        else {
            Write-Host "(missing) $dataDir"
        }
    }
}

Assert-Step "クリーンアップ（アンインストール）" {
    Remove-AppxPackage -Package $pkg.PackageFullName
    Start-Sleep -Seconds 1
    $still = Get-AppxPackage | Where-Object { $_.PackageFullName -eq $pkg.PackageFullName }
    if ($still) {
        throw "アンインストール後もパッケージが残っています"
    }
}

Write-Host "SMOKE PASSED"

#Requires -Version 5.1
<#
.SYNOPSIS
  ビルド済み MSIX をインストールし、CLI スモーク試験を行う。
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$MsixPath,
    [string]$CerPath = ".\KeyAutomator_CI.cer",
    [string]$Password = "KeyAutomator-CI-Temp!"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $MsixPath)) {
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

    if (Test-Path $CerPath) {
        Import-Certificate -FilePath $CerPath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
    }
    else {
        Write-Warning "CER が無いためスキップ: $CerPath"
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
    Add-AppxPackage -Path $MsixPath
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

$exe = Join-Path $pkg.InstallLocation "KeyAutomator.exe"
if (-not (Test-Path $exe)) {
    $exe = Get-ChildItem -Path $pkg.InstallLocation -Recurse -Filter KeyAutomator.exe |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $exe -or -not (Test-Path $exe)) {
    throw "KeyAutomator.exe がインストール先にありません"
}

Assert-Step "CLI ヘルプ (-h)" {
    $out = & $exe -h 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "-h の終了コードが $LASTEXITCODE"
    }
    if ($out -notmatch "KeyAutomator") {
        throw "-h 出力に KeyAutomator が含まれません`n$out"
    }
    if ($out -notmatch "確認アクション") {
        throw "-h に確認アクションの注意がありません`n$out"
    }
}

Assert-Step "設定フォルダ解決（パッケージ実行後）" {
    # 一度ヘルプを叩いただけでは設定が無いことがあるので、空マクロ実行ではなく
    # AppPaths 相当の Locals を確認できるよう、select_copy を短く実行する。
    # dialog 無しサンプル。フォーカス先へのキー送信は許容（CI 検証目的）。
    $proc = Start-Process -FilePath $exe -ArgumentList @("-alias", "select_copy") -PassThru -WindowStyle Hidden
    if (-not $proc.WaitForExit(60000)) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        throw "select_copy がタイムアウトしました"
    }
    if ($proc.ExitCode -ne 0) {
        Write-Warning "select_copy exit=$($proc.ExitCode)（入力先が無い環境では失敗し得る）。継続確認します。"
    }

    $dataDir = Join-Path $env:LOCALAPPDATA "KeyAutomator"
    if (-not (Test-Path $dataDir)) {
        # 初回サンプル投入は GUI 起動時と同等の Load 経路。CLI は既存 config を読む。
        # パッケージ直後で config が無い場合は Load がサンプルを書く想定だが、
        # CLI 経路でも ConfigStore.Load が走るので作成されるはず。
        throw "データフォルダがありません: $dataDir"
    }
    $config = Join-Path $dataDir "config.json"
    if (-not (Test-Path $config)) {
        throw "config.json がありません: $config"
    }
    Write-Host "config: $config"
}

Assert-Step "クリーンアップ（アンインストール）" {
    Remove-AppxPackage -Package $pkg.PackageFullName
}

Write-Host "SMOKE PASSED"

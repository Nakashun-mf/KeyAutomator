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

Assert-Step "パッケージ実行で設定がユーザー領域へ作られる" {
    # dialog 無しサンプル。キー送信失敗は環境次第なので、exit code より
    # ConfigStore.Load 側で config.json がユーザー領域にできることを主検証にする。
    $proc = Start-Process -FilePath $exe -ArgumentList @("-alias", "select_copy") -PassThru -WindowStyle Hidden
    if (-not $proc.WaitForExit(60000)) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        throw "select_copy がタイムアウトしました"
    }
    if ($proc.ExitCode -ne 0) {
        Write-Warning "select_copy exit=$($proc.ExitCode)（入力先が無い環境では失敗し得る）"
    }

    $dataDir = Join-Path $env:LOCALAPPDATA "KeyAutomator"
    $config = Join-Path $dataDir "config.json"
    if (-not (Test-Path $config)) {
        throw "config.json がありません（パッケージ時は LocalAppData 期待）: $config"
    }

    # インストール先（書けない場所）に設定が散っていないこと
    $bad = Join-Path $pkg.InstallLocation "config.json"
    if (Test-Path $bad) {
        throw "インストール先に config.json が作られてしまいました: $bad"
    }

    Write-Host "config: $config"
}

Assert-Step "クリーンアップ（アンインストール）" {
    Remove-AppxPackage -Package $pkg.PackageFullName
}

Write-Host "SMOKE PASSED"

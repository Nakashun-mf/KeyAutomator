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

if (-not (Test-Path -LiteralPath $MsixPath)) {
    throw "MSIX がありません: $MsixPath"
}

function Assert-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "==> $Name"
    & $Action
    Write-Host "OK: $Name"
}

function Invoke-NativeCapture {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [int]$TimeoutMs = 60000
    )

    $outFile = [System.IO.Path]::GetTempFileName()
    $errFile = [System.IO.Path]::GetTempFileName()
    try {
        $proc = Start-Process -FilePath $FilePath `
            -ArgumentList $ArgumentList `
            -PassThru `
            -Wait:$false `
            -WindowStyle Hidden `
            -RedirectStandardOutput $outFile `
            -RedirectStandardError $errFile
        if (-not $proc.WaitForExit($TimeoutMs)) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            throw "タイムアウト (${TimeoutMs}ms): $FilePath $($ArgumentList -join ' ')"
        }
        $stdout = Get-Content -LiteralPath $outFile -Raw -ErrorAction SilentlyContinue
        $stderr = Get-Content -LiteralPath $errFile -Raw -ErrorAction SilentlyContinue
        return [pscustomobject]@{
            ExitCode = $proc.ExitCode
            StdOut   = $stdout
            StdErr   = $stderr
            Combined = "$( $stdout )$( $stderr )"
        }
    }
    finally {
        Remove-Item -Force $outFile, $errFile -ErrorAction SilentlyContinue
    }
}

Assert-Step "サイドロード許可と証明書登録" {
    New-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" -Force | Out-Null
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" `
        -Name AllowAllTrustedApps -Value 1 -Type DWord -Force
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" `
        -Name AllowDevelopmentWithoutDevLicense -Value 1 -Type DWord -Force

    if (Test-Path -LiteralPath $CerPath) {
        Import-Certificate -FilePath $CerPath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
        Import-Certificate -FilePath $CerPath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null
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
    try {
        Add-AppxPackage -Path $MsixPath -ErrorAction Stop
    }
    catch {
        Write-Host "Add-AppxPackage failed: $($_.Exception.Message)"
        Get-AppxLog -ActivityId ([guid]::Empty) -ErrorAction SilentlyContinue |
            Select-Object -Last 40 |
            ForEach-Object { Write-Host $_ }
        throw
    }
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
if (-not (Test-Path -LiteralPath $exe)) {
    $exe = Get-ChildItem -Path $pkg.InstallLocation -Recurse -Filter KeyAutomator.exe -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $exe -or -not (Test-Path -LiteralPath $exe)) {
    Write-Host "Install tree:"
    Get-ChildItem -Path $pkg.InstallLocation -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 80 FullName |
        ForEach-Object { Write-Host $_.FullName }
    throw "KeyAutomator.exe がインストール先にありません"
}

Write-Host "Exe: $exe"

Assert-Step "CLI ヘルプ (-h)" {
    $result = Invoke-NativeCapture -FilePath $exe -ArgumentList @("-h") -TimeoutMs 60000
    Write-Host "exit=$($result.ExitCode)"
    Write-Host $result.Combined
    if ($result.ExitCode -ne 0) {
        throw "-h の終了コードが $($result.ExitCode)"
    }
    if ($result.Combined -notmatch "KeyAutomator") {
        throw "-h 出力に KeyAutomator が含まれません"
    }
    if ($result.Combined -notmatch "確認アクション") {
        throw "-h に確認アクションの注意がありません"
    }
}

Assert-Step "パッケージ実行で設定がユーザー領域へ作られる" {
    # dialog 無しサンプル。キー送信失敗は環境次第なので、exit code より
    # ConfigStore.Load 側で config.json がユーザー領域にできることを主検証にする。
    $result = Invoke-NativeCapture -FilePath $exe -ArgumentList @("-alias", "select_copy") -TimeoutMs 90000
    Write-Host "select_copy exit=$($result.ExitCode)"
    if ($result.Combined) { Write-Host $result.Combined }
    if ($result.ExitCode -ne 0) {
        Write-Warning "select_copy exit=$($result.ExitCode)（入力先が無い環境では失敗し得る）"
    }

    $dataDir = Join-Path $env:LOCALAPPDATA "KeyAutomator"
    $config = Join-Path $dataDir "config.json"
    if (-not (Test-Path -LiteralPath $config)) {
        Write-Host "LocalAppData KeyAutomator:"
        if (Test-Path -LiteralPath $dataDir) {
            Get-ChildItem -LiteralPath $dataDir | ForEach-Object { Write-Host $_.FullName }
        }
        else {
            Write-Host "(directory missing) $dataDir"
        }
        throw "config.json がありません（パッケージ時は LocalAppData 期待）: $config"
    }

    $bad = Join-Path $pkg.InstallLocation "config.json"
    if (Test-Path -LiteralPath $bad) {
        throw "インストール先に config.json が作られてしまいました: $bad"
    }

    Write-Host "config: $config"
}

Assert-Step "クリーンアップ（アンインストール）" {
    Remove-AppxPackage -Package $pkg.PackageFullName
}

Write-Host "SMOKE PASSED"

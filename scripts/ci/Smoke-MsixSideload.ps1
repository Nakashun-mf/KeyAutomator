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

function Wait-File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$TimeoutSec = 60
    )
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $TimeoutSec) {
        if (Test-Path -LiteralPath $Path) {
            Start-Sleep -Milliseconds 300
            return $true
        }
        Start-Sleep -Milliseconds 400
    }
    return $false
}

function Invoke-InPackageCli {
    param(
        [Parameter(Mandatory = $true)]$Package,
        [Parameter(Mandatory = $true)][string]$CliArgs,
        [Parameter(Mandatory = $true)][string]$OutFile,
        [Parameter(Mandatory = $true)][string]$ExitFile,
        [int]$TimeoutSec = 90
    )

    Remove-Item -Force $OutFile, $ExitFile -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path (Split-Path $OutFile -Parent) | Out-Null

    # AppId は Package.appxmanifest の Application Id（"App"）
    # エイリアス直起動は WinExe の終了コードが化けやすいので、パッケージ文脈で cmd 経由する。
    $cmdArgs = "/c KeyAutomator.exe $CliArgs > `"$OutFile`" 2>&1 & echo %ERRORLEVEL%> `"$ExitFile`""
    Write-Host "Invoke-CommandInDesktopPackage AppId=App Args=$cmdArgs"

    if (-not (Get-Command Invoke-CommandInDesktopPackage -ErrorAction SilentlyContinue)) {
        throw "Invoke-CommandInDesktopPackage がありません"
    }

    Invoke-CommandInDesktopPackage `
        -PackageFamilyName $Package.PackageFamilyName `
        -AppId "App" `
        -Command "cmd.exe" `
        -Args $cmdArgs `
        -PreventBreakaway | Out-Null

    if (-not (Wait-File -Path $ExitFile -TimeoutSec $TimeoutSec)) {
        throw "CLI 終了コードファイルがタイムアウト: $ExitFile"
    }

    $codeText = (Get-Content -LiteralPath $ExitFile -Raw).Trim()
    $code = 1
    [void][int]::TryParse($codeText, [ref]$code)
    $output = ""
    if (Test-Path -LiteralPath $OutFile) {
        $output = Get-Content -LiteralPath $OutFile -Raw -ErrorAction SilentlyContinue
    }
    return [pscustomobject]@{ ExitCode = $code; Output = $output }
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
    Write-Host "Add-AppxPackage: $MsixPath"
    Get-Item -LiteralPath $MsixPath | Format-List FullName, Length, LastWriteTime | Out-String | Write-Host
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
Write-Host "Family   : $($pkg.PackageFamilyName)"

$installExe = Join-Path $pkg.InstallLocation "KeyAutomator.exe"
if (-not (Test-Path -LiteralPath $installExe)) {
    throw "インストール先に KeyAutomator.exe がありません: $installExe"
}

$alias = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\KeyAutomator.exe"
if (Test-Path -LiteralPath $alias) {
    Write-Host "Alias present: $alias"
}
else {
    Write-Warning "AppExecutionAlias 未検出（インストール直後の遅延の可能性）"
}

$dataDir = Join-Path $env:LOCALAPPDATA "KeyAutomator"
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

Assert-Step "CLI ヘルプ (-h)" {
    $helpOut = Join-Path $dataDir "ci-help-out.txt"
    $helpExit = Join-Path $dataDir "ci-help-exit.txt"
    $result = Invoke-InPackageCli -Package $pkg -CliArgs "-h" -OutFile $helpOut -ExitFile $helpExit -TimeoutSec 90
    Write-Host "exit=$($result.ExitCode)"
    Write-Host $result.Output
    if ($result.ExitCode -ne 0) {
        throw "-h の終了コードが $($result.ExitCode)"
    }
    if ($result.Output -notmatch "KeyAutomator") {
        throw "-h 出力に KeyAutomator が含まれません"
    }
    if ($result.Output -notmatch "確認アクション") {
        throw "-h に確認アクションの注意がありません"
    }
}

Assert-Step "パッケージ実行で設定がユーザー領域へ作られる" {
    $runOut = Join-Path $dataDir "ci-alias-out.txt"
    $runExit = Join-Path $dataDir "ci-alias-exit.txt"
    $result = Invoke-InPackageCli -Package $pkg -CliArgs "-alias select_copy" -OutFile $runOut -ExitFile $runExit -TimeoutSec 90
    Write-Host "select_copy exit=$($result.ExitCode)"
    if ($result.Output) { Write-Host $result.Output }
    if ($result.ExitCode -ne 0) {
        Write-Warning "select_copy exit=$($result.ExitCode)（入力先が無い環境では失敗し得る）"
    }

    $config = Join-Path $dataDir "config.json"
    if (-not (Test-Path -LiteralPath $config)) {
        Write-Host "LocalAppData KeyAutomator:"
        Get-ChildItem -LiteralPath $dataDir -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_.FullName }
        $log = Join-Path $dataDir "error.log"
        if (Test-Path -LiteralPath $log) {
            Write-Host "----- error.log -----"
            Get-Content -LiteralPath $log -Tail 40 | ForEach-Object { Write-Host $_ }
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

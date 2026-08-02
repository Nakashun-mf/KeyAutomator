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

function Invoke-AliasCli {
    param(
        [Parameter(Mandatory = $true)][string]$Exe,
        [string[]]$ArgumentList = @(),
        [int]$TimeoutMs = 90000
    )

    Write-Host "Run: $Exe $($ArgumentList -join ' ')"
    $proc = Start-Process -FilePath $Exe -ArgumentList $ArgumentList -PassThru -WindowStyle Hidden
    if (-not $proc.WaitForExit($TimeoutMs)) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        throw "タイムアウト (${TimeoutMs}ms): $Exe $($ArgumentList -join ' ')"
    }
    Write-Host "exit=$($proc.ExitCode)"
    return $proc.ExitCode
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
$exe = $null
foreach ($i in 1..15) {
    if (Test-Path -LiteralPath $alias) {
        $exe = $alias
        break
    }
    Start-Sleep -Seconds 1
}
if (-not $exe) {
    throw "AppExecutionAlias が見つかりません: $alias"
}
Write-Host "CLI alias: $exe"

$dataDir = Join-Path $env:LOCALAPPDATA "KeyAutomator"
$config = Join-Path $dataDir "config.json"
$log = Join-Path $dataDir "error.log"

Assert-Step "CLI ヘルプ起動 (-h)" {
    # WinExe + AppExecutionAlias は終了コードが化けやすいので、ここでは起動完了のみ確認。
    # ヘルプ文言はユニットテスト（CliRunnerHelpTests）で担保する。
    $code = Invoke-AliasCli -Exe $exe -ArgumentList @("-h") -TimeoutMs 60000
    if ($code -ne 0) {
        Write-Warning "-h exit=$code（エイリアス経由の WinExe では非 0 になり得る）。続行して副作用を検証します。"
    }
}

Assert-Step "パッケージ実行で設定がユーザー領域へ作られる" {
    Remove-Item -Force $config -ErrorAction SilentlyContinue

    $code = Invoke-AliasCli -Exe $exe -ArgumentList @("-alias", "select_copy") -TimeoutMs 90000
    if ($code -ne 0) {
        Write-Warning "select_copy exit=$code（入力先が無い／エイリアス終了コード化けの可能性）"
    }

    if (Test-Path -LiteralPath $log) {
        Write-Host "----- error.log (tail) -----"
        Get-Content -LiteralPath $log -Tail 40 | ForEach-Object { Write-Host $_ }
    }

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
    $still = Get-AppxPackage | Where-Object {
        $_.PackageFullName -eq $pkg.PackageFullName
    }
    if ($still) {
        throw "アンインストール後もパッケージが残っています"
    }
}

Write-Host "SMOKE PASSED"

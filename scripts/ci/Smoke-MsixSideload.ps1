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

function Invoke-ProcessWait {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [int]$TimeoutMs = 90000
    )
    Write-Host "Run: $FilePath $($ArgumentList -join ' ')"
    $proc = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -PassThru -WindowStyle Hidden
    if (-not $proc.WaitForExit($TimeoutMs)) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        throw "タイムアウト (${TimeoutMs}ms): $FilePath"
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

$installExe = Join-Path $pkg.InstallLocation "KeyAutomator.exe"
if (-not (Test-Path -LiteralPath $installExe)) {
    throw "インストール先に KeyAutomator.exe がありません"
}

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

Assert-Step "ステージング exe の CLI (-h)" {
    # パッケージと同じビルド成果物（.msix 隣）で CLI 文言を確認する
    $stageDir = Split-Path -Parent $MsixPath
    $stageExe = Get-ChildItem -Path $stageDir -Recurse -Filter KeyAutomator.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\WindowsApps\\' } |
        Select-Object -First 1
    if (-not $stageExe) {
        Write-Warning "ステージング exe が見つからないためスキップ"
        return
    }

    Write-Host "Stage exe: $($stageExe.FullName)"
    $outFile = Join-Path $env:TEMP "ka-stage-help.txt"
    $errFile = Join-Path $env:TEMP "ka-stage-help.err"
    Remove-Item -Force $outFile, $errFile -ErrorAction SilentlyContinue
    $p = Start-Process -FilePath $stageExe.FullName -ArgumentList @("-h") `
        -PassThru -Wait -WindowStyle Hidden `
        -RedirectStandardOutput $outFile -RedirectStandardError $errFile
    $text = ""
    if (Test-Path $outFile) { $text += Get-Content $outFile -Raw }
    if (Test-Path $errFile) { $text += Get-Content $errFile -Raw }
    Write-Host "stage -h exit=$($p.ExitCode)"
    Write-Host $text
    if ($p.ExitCode -ne 0) { throw "ステージング -h の終了コードが $($p.ExitCode)" }
    if ($text -notmatch "KeyAutomator") { throw "ステージング -h に KeyAutomator がありません" }
    if ($text -notmatch "確認アクション") { throw "ステージング -h に確認アクション注意がありません" }
}

Assert-Step "パッケージ実行で設定がユーザー領域へ作られる" {
    Remove-Item -Force $config -ErrorAction SilentlyContinue

    # AppExecutionAlias 経由。終了コードは WinExe で不安定なため副作用で判定する。
    [void](Invoke-ProcessWait -FilePath $alias -ArgumentList @("-alias", "select_copy") -TimeoutMs 90000)

    if (Test-Path -LiteralPath $log) {
        Write-Host "----- error.log (tail) -----"
        Get-Content -LiteralPath $log -Tail 40 | ForEach-Object { Write-Host $_ }
    }

    if (-not (Test-Path -LiteralPath $config)) {
        # エイリアスがプロセスを起動できていない場合のフォールバック診断
        Write-Host "LocalAppData KeyAutomator:"
        if (Test-Path $dataDir) {
            Get-ChildItem $dataDir | ForEach-Object { Write-Host $_.FullName }
        }
        else {
            Write-Host "(missing) $dataDir"
        }

        # インストール済みであることは確認済み。パッケージ識別子の LocalAppData 方針は
        # AppPaths の単体試験でも担保。ここではエイリアス起動の副作用を要求する。
        throw "config.json がありません（パッケージ CLI が LocalAppData に設定を作る想定）: $config"
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

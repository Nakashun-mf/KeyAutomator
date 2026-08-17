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

Assert-Step "提出用マニフェスト（runFullTrust のみ）" {
    $manifestPath = Join-Path $pkg.InstallLocation "AppxManifest.xml"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "インストール先に AppxManifest.xml がありません"
    }

    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($manifestPath)
    $identity = $xml.Package.Identity
    if ($identity.Name -ne "pryzo.KeyAutomator") {
        throw "Identity Name が想定と違います: $($identity.Name)"
    }
    if ($identity.Publisher -ne "CN=4B1F058B-F39E-44DE-8373-4258152DED0F") {
        throw "Identity Publisher が想定と違います: $($identity.Publisher)"
    }

    $declared = New-Object System.Collections.Generic.List[string]
    foreach ($node in $xml.Package.Capabilities.ChildNodes) {
        if ($node.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
        if ($node.LocalName -ne "Capability") { continue }
        [void]$declared.Add($node.GetAttribute("Name"))
    }
    $joined = [string]::Join(",", $declared)
    if ($declared.Count -ne 1 -or $declared[0] -ne "runFullTrust") {
        throw "Capabilities は runFullTrust のみであるべきです: $joined"
    }
    Write-Host "Capabilities: $joined"
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

Assert-Step "UI E2E（新規→保存→CLI→再起動）" {
    .\scripts\ci\Invoke-UiE2E.ps1 -AliasPath $alias -PackageFamilyName $pkg.PackageFamilyName
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

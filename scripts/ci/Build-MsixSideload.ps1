#Requires -Version 5.1
<#
.SYNOPSIS
  サイドロード用 MSIX をビルドする（単一 exe 既定設定は変えない）。
#>
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$PfxPath = ".\KeyAutomator_CI.pfx",
    [string]$Password = "KeyAutomator-CI-Temp!",
    [string]$OutDir = ".\AppPackages\CI"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $root

if (-not (Test-Path $PfxPath)) {
    & "$PSScriptRoot\New-CiSigningCertificate.ps1" -PfxPath $PfxPath -Password $Password
}

$pfxFull = (Resolve-Path $PfxPath).Path
$outFull = Join-Path $root $OutDir
Remove-Item -Recurse -Force $outFull -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $outFull | Out-Null

$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe `
    -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $msbuild) {
    # GitHub windows-latest 向けフォールバック
    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )
    $msbuild = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $msbuild) {
    throw "MSBuild.exe が見つかりません。"
}

Write-Host "Using MSBuild: $msbuild"

& $msbuild .\KeyAutomator.csproj `
    /restore `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /p:KeyAutomatorPackaged=true `
    /p:AppxPackageSigningEnabled=true `
    /p:PackageCertificateKeyFile="$pfxFull" `
    /p:PackageCertificatePassword="$Password" `
    /p:AppxPackageDir="$outFull\" `
    /p:UapAppxPackageBuildMode=SideLoadOnly `
    /p:AppxBundle=Never `
    /p:GenerateAppxPackageOnBuild=true `
    /p:AppxSymbolPackageEnabled=false `
    /verbosity:minimal

if ($LASTEXITCODE -ne 0) {
    throw "MSIX ビルドに失敗しました (exit $LASTEXITCODE)"
}

$msix = Get-ChildItem -Path $outFull -Recurse -Filter *.msix | Select-Object -First 1
if (-not $msix) {
    throw "MSIX ファイルが見つかりません: $outFull"
}

Write-Host "MSIX: $($msix.FullName)"
Write-Output $msix.FullName

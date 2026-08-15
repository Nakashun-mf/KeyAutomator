#Requires -Version 5.1
<#
.SYNOPSIS
  ユニットテストをビルドして実行する。

  WinUI / Windows App SDK の Pri タスクは Visual Studio の MSBuild 拡張に依存する。
  VS Developer Shell を入れてから `dotnet test` する。
#>
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $root

$testProj = Join-Path $root "KeyAutomator.Tests\KeyAutomator.Tests.csproj"
if (-not (Test-Path -LiteralPath $testProj)) {
    throw "テストプロジェクトが見つかりません: $testProj"
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw "vswhere.exe が見つかりません。Visual Studio 付きの Windows runner が必要です。"
}

$vsInstall = & $vswhere -latest -products * -property installationPath | Select-Object -First 1
if (-not $vsInstall) {
    throw "Visual Studio のインストール先が見つかりません"
}

$devShell = Join-Path $vsInstall "Common7\Tools\Microsoft.VisualStudio.DevShell.dll"
if (-not (Test-Path -LiteralPath $devShell)) {
    throw "Microsoft.VisualStudio.DevShell.dll が見つかりません: $devShell"
}

Write-Host "Enter-VsDevShell: $vsInstall"
Import-Module $devShell
Enter-VsDevShell -VsInstallPath $vsInstall -SkipAutomaticLocation -DevCmdArguments "-arch=amd64"
Set-Location $root

$resultsDir = Join-Path $root "TestResults"
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

Write-Host "dotnet test $testProj ($Configuration|$Platform)"
dotnet test $testProj `
    -c $Configuration `
    -p:Platform=$Platform `
    -p:SelfContained=false `
    -p:PublishSingleFile=false `
    -p:WindowsAppSDKSelfContained=false `
    -p:EnableCoreMrtTooling=false `
    -p:GenerateAppxPackageOnBuild=false `
    --logger "trx;LogFileName=KeyAutomator.Tests.trx" `
    --results-directory $resultsDir
if ($LASTEXITCODE -ne 0) {
    throw "ユニットテストが失敗しました (exit $LASTEXITCODE)"
}

Write-Host "Unit tests passed."

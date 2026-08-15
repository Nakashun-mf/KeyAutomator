#Requires -Version 5.1
<#
.SYNOPSIS
  ユニットテストをビルドして実行する。

  WinUI 参照プロジェクトは `dotnet test` の再ビルドで ExpandPriContent が欠ける。
  ビルドは Visual Studio MSBuild、実行は出力 DLL への vstest に分ける。
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

Write-Host "MSBuild restore+build: $testProj ($Configuration|$Platform)"
msbuild $testProj `
    /restore `
    /t:Build `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /p:GenerateAppxPackageOnBuild=false `
    /p:WindowsPackageType=None `
    /p:SelfContained=false `
    /p:PublishSingleFile=false `
    /p:WindowsAppSDKSelfContained=false `
    /m `
    /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "テストプロジェクトのビルドに失敗しました (exit $LASTEXITCODE)"
}

$outRoot = Join-Path $root "KeyAutomator.Tests\bin\$Platform\$Configuration"
$dll = Get-ChildItem -Path $outRoot -Recurse -Filter "KeyAutomator.Tests.dll" |
    Select-Object -First 1
if (-not $dll) {
    throw "KeyAutomator.Tests.dll が見つかりません: $outRoot"
}

$dir = $dll.DirectoryName
Write-Host "Test assembly: $($dll.FullName)"
Get-ChildItem -LiteralPath $dir -Filter "*.runtimeconfig.json" | ForEach-Object { Write-Host "  $($_.Name)" }

$nugetHost = Join-Path $env:USERPROFILE ".nuget\packages\microsoft.testplatform.testhost"
if (-not (Test-Path -LiteralPath $nugetHost)) {
    throw "microsoft.testplatform.testhost がありません: $nugetHost"
}
$pkg = Get-ChildItem -LiteralPath $nugetHost -Directory | Sort-Object Name -Descending | Select-Object -First 1
$libRoot = Join-Path $pkg.FullName "lib"
$tfmDir = @(
    (Join-Path $libRoot "net8.0"),
    (Join-Path $libRoot "netcoreapp3.1")
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $tfmDir) {
    throw "testhost の lib フォルダが見つかりません: $libRoot"
}
Write-Host "Copy testhost from $tfmDir"
Copy-Item -Path (Join-Path $tfmDir "*") -Destination $dir -Force

$appCfg = Join-Path $dir "KeyAutomator.Tests.runtimeconfig.json"
$hostCfg = Join-Path $dir "testhost.runtimeconfig.json"
if ((Test-Path -LiteralPath $appCfg) -and -not (Test-Path -LiteralPath $hostCfg)) {
    Copy-Item -LiteralPath $appCfg -Destination $hostCfg
    Write-Host "Copied testhost.runtimeconfig.json"
}

$resultsDir = Join-Path $root "TestResults"
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw "vswhere.exe が見つかりません。Visual Studio 付きの Windows runner が必要です。"
}
$vstest = & $vswhere -latest -products * -find "Common7\IDE\Extensions\TestPlatform\vstest.console.exe" |
    Select-Object -First 1
if (-not $vstest) {
    $vstest = & $vswhere -latest -products * -find "**\vstest.console.exe" | Select-Object -First 1
}
if (-not $vstest) {
    throw "vstest.console.exe が見つかりません"
}

Write-Host "vstest: $vstest"
& $vstest $dll.FullName `
    /Logger:"trx;LogFileName=KeyAutomator.Tests.trx" `
    /ResultsDirectory:$resultsDir
if ($LASTEXITCODE -ne 0) {
    throw "ユニットテストが失敗しました (exit $LASTEXITCODE)"
}

Write-Host "Unit tests passed."

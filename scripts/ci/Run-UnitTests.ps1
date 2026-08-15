#Requires -Version 5.1
<#
.SYNOPSIS
  ユニットテストをビルドして実行する。

  WinUI 参照プロジェクトは dotnet SDK 単体だと Pri タスクが欠けることがあるため、
  ビルドは MSBuild、実行は dotnet test --no-build に分ける。
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
    /m `
    /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "テストプロジェクトのビルドに失敗しました (exit $LASTEXITCODE)"
}

$resultsDir = Join-Path $root "TestResults"
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

Write-Host "dotnet test --no-build"
dotnet test $testProj `
    -c $Configuration `
    -p:Platform=$Platform `
    --no-build `
    --logger "trx;LogFileName=KeyAutomator.Tests.trx" `
    --results-directory $resultsDir
if ($LASTEXITCODE -ne 0) {
    throw "ユニットテストが失敗しました (exit $LASTEXITCODE)"
}

Write-Host "Unit tests passed."

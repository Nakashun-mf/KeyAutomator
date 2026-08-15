#Requires -Version 5.1
<#
.SYNOPSIS
  ユニットテストをビルドして実行する。

  WinUI の Pri タスクは VS Developer Shell 上の MSBuild で解決する。
  testhost は NuGet から一式を出力フォルダへコピーしてから実行する。
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

$commonProps = @(
    "-p:Platform=$Platform",
    "-p:SelfContained=false",
    "-p:PublishSingleFile=false",
    "-p:WindowsAppSDKSelfContained=false",
    "-p:EnableCoreMrtTooling=false",
    "-p:GenerateAppxPackageOnBuild=false"
)

Write-Host "dotnet build $testProj ($Configuration|$Platform)"
dotnet build $testProj -c $Configuration @commonProps
if ($LASTEXITCODE -ne 0) {
    throw "テストプロジェクトのビルドに失敗しました (exit $LASTEXITCODE)"
}

$outRoot = Join-Path $root "KeyAutomator.Tests\bin\$Platform\$Configuration"
$dll = Get-ChildItem -Path $outRoot -Recurse -Filter "KeyAutomator.Tests.dll" | Select-Object -First 1
if (-not $dll) {
    throw "KeyAutomator.Tests.dll が見つかりません: $outRoot"
}
$dir = $dll.DirectoryName
Write-Host "Test assembly: $($dll.FullName)"

function Copy-NuGetLib {
    param([string]$Package, [string]$Destination)
    $pkgRoot = Join-Path $env:USERPROFILE ".nuget\packages\$Package"
    if (-not (Test-Path -LiteralPath $pkgRoot)) {
        Write-Host "skip $Package (not restored)"
        return
    }
    $pkg = Get-ChildItem -LiteralPath $pkgRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
    $lib = Join-Path $pkg.FullName "lib"
    foreach ($tfm in @("net8.0", "net6.0", "netstandard2.0", "netcoreapp3.1")) {
        $tfmDir = Join-Path $lib $tfm
        if (Test-Path -LiteralPath $tfmDir) {
            Copy-Item -Path (Join-Path $tfmDir "*") -Destination $Destination -Force
            Write-Host "Copied $Package ($tfm)"
            return
        }
    }
    Write-Host "skip $Package (no matching lib TFM)"
}

Copy-NuGetLib "microsoft.testplatform.testhost" $dir
Copy-NuGetLib "microsoft.testplatform.objectmodel" $dir
Copy-NuGetLib "microsoft.testplatform.communicationutilities" $dir
Copy-NuGetLib "mstest.testframework" $dir
Copy-NuGetLib "mstest.testadapter" $dir
Copy-NuGetLib "newtonsoft.json" $dir

$appCfg = Join-Path $dir "KeyAutomator.Tests.runtimeconfig.json"
$hostCfg = Join-Path $dir "testhost.runtimeconfig.json"
if ((Test-Path -LiteralPath $appCfg) -and -not (Test-Path -LiteralPath $hostCfg)) {
    Copy-Item -LiteralPath $appCfg -Destination $hostCfg
    Write-Host "Copied testhost.runtimeconfig.json"
}

$resultsDir = Join-Path $root "TestResults"
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

Write-Host "dotnet test --no-build"
dotnet test $testProj `
    -c $Configuration `
    @commonProps `
    --no-build `
    --logger "trx;LogFileName=KeyAutomator.Tests.trx" `
    --results-directory $resultsDir
if ($LASTEXITCODE -ne 0) {
    throw "ユニットテストが失敗しました (exit $LASTEXITCODE)"
}

Write-Host "Unit tests passed."

#Requires -Version 5.1
<#
.SYNOPSIS
  ユニットテストをビルドして実行する。

  WinUI の Pri タスクは VS Developer Shell 上の MSBuild で解決する。
  testhost は NuGet から一式を出力フォルダへコピーしてから実行する。
  本体の WASDK 自動初期化はオフにして、testhost がランタイム無しで DLL を読めるようにする。
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
    "-p:WindowsAppSdkBootstrapInitialize=false",
    "-p:WindowsAppSdkDeploymentManagerInitialize=false",
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

function Copy-NuGetFiles {
    param(
        [string]$Package,
        [string]$Destination,
        [string[]]$RelativeRoots = @("lib", "build", "buildTransitive")
    )
    $pkgRoot = Join-Path $env:USERPROFILE ".nuget\packages\$Package"
    if (-not (Test-Path -LiteralPath $pkgRoot)) {
        Write-Host "skip $Package (not restored)"
        return
    }
    $pkg = Get-ChildItem -LiteralPath $pkgRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
    $copied = $false
    foreach ($rel in $RelativeRoots) {
        $baseDir = Join-Path $pkg.FullName $rel
        if (-not (Test-Path -LiteralPath $baseDir)) {
            continue
        }
        $tfms = @(
            "net8.0",
            "net8.0-windows10.0.26100.0",
            "net6.0",
            "net6.0-windows10.0.17763.0",
            "netstandard2.0",
            "netcoreapp3.1",
            "_common"
        )
        foreach ($tfm in $tfms) {
            $tfmDir = Join-Path $baseDir $tfm
            if (Test-Path -LiteralPath $tfmDir) {
                Copy-Item -Path (Join-Path $tfmDir "*") -Destination $Destination -Force -ErrorAction SilentlyContinue
                Write-Host "Copied $Package ($rel/$tfm)"
                $copied = $true
                break
            }
        }
        if ($copied) {
            return
        }
    }
    $commonDir = Join-Path $pkg.FullName "build\_common"
    if (Test-Path -LiteralPath $commonDir) {
        Copy-Item -Path (Join-Path $commonDir "*") -Destination $Destination -Force -ErrorAction SilentlyContinue
        Write-Host "Copied $Package (build/_common)"
        return
    }
    Write-Host "skip $Package (no matching lib/build TFM)"
}

function Copy-NuGetAssembly {
    param(
        [string]$Package,
        [string]$FileName,
        [string]$Destination
    )
    $pkgRoot = Join-Path $env:USERPROFILE ".nuget\packages\$Package"
    if (-not (Test-Path -LiteralPath $pkgRoot)) {
        Write-Host "skip $FileName ($Package not restored)"
        return
    }
    $hit = Get-ChildItem -LiteralPath $pkgRoot -Recurse -Filter $FileName -File -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($hit) {
        Copy-Item -LiteralPath $hit.FullName -Destination (Join-Path $Destination $FileName) -Force
        Write-Host "Copied $FileName from $($hit.FullName)"
        return
    }
    Write-Host "skip $FileName (not in $Package)"
}

$appSearchRoots = @(
    (Join-Path $root "bin\$Platform\$Configuration"),
    (Join-Path $root "bin"),
    $dir
)
$appDll = $null
foreach ($search in $appSearchRoots) {
    if (-not (Test-Path -LiteralPath $search)) {
        continue
    }
    $appDll = Get-ChildItem -Path $search -Recurse -Filter "KeyAutomator.dll" -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -ne $dir } |
        Select-Object -First 1
    if ($appDll) {
        break
    }
}
if ($appDll) {
    Write-Host "Copy app output from $($appDll.DirectoryName)"
    Copy-Item -Path (Join-Path $appDll.DirectoryName "*") -Destination $dir -Force
} else {
    Write-Host "KeyAutomator.dll output not found beside the test assembly; using NuGet copies"
}

Copy-NuGetFiles "microsoft.testplatform.testhost" $dir
Copy-NuGetFiles "microsoft.testplatform.objectmodel" $dir
Copy-NuGetFiles "microsoft.testplatform.communicationutilities" $dir
Copy-NuGetFiles "mstest.testframework" $dir
Copy-NuGetFiles "mstest.testadapter" $dir
Copy-NuGetFiles "newtonsoft.json" $dir
Copy-NuGetFiles "communitytoolkit.mvvm" $dir

Copy-NuGetAssembly "communitytoolkit.mvvm" "CommunityToolkit.Mvvm.dll" $dir
Copy-NuGetAssembly "microsoft.windowsappsdk" "Microsoft.WindowsAppRuntime.Bootstrap.Net.dll" $dir
Copy-NuGetAssembly "microsoft.windows.sdk.net.ref" "Microsoft.Windows.SDK.NET.dll" $dir
Copy-NuGetAssembly "microsoft.windows.sdk.net.ref" "WinRT.Runtime.dll" $dir

$appCfg = Join-Path $dir "KeyAutomator.Tests.runtimeconfig.json"
$hostCfg = Join-Path $dir "testhost.runtimeconfig.json"
if ((Test-Path -LiteralPath $appCfg) -and -not (Test-Path -LiteralPath $hostCfg)) {
    Copy-Item -LiteralPath $appCfg -Destination $hostCfg
    Write-Host "Copied testhost.runtimeconfig.json"
}

foreach ($name in @(
        "CommunityToolkit.Mvvm.dll",
        "Microsoft.Windows.SDK.NET.dll",
        "WinRT.Runtime.dll",
        "Microsoft.WindowsAppRuntime.Bootstrap.Net.dll",
        "testhost.dll",
        "Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.dll"
    )) {
    $present = Test-Path -LiteralPath (Join-Path $dir $name)
    Write-Host ("dep {0}: {1}" -f $name, $present)
}

$resultsDir = Join-Path $root "TestResults"
New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

Write-Host "dotnet test --no-build"
dotnet test $testProj `
    -c $Configuration `
    @commonProps `
    --no-build `
    --test-adapter-path $dir `
    --logger "trx;LogFileName=KeyAutomator.Tests.trx" `
    --results-directory $resultsDir
if ($LASTEXITCODE -ne 0) {
    throw "ユニットテストが失敗しました (exit $LASTEXITCODE)"
}

Write-Host "Unit tests passed."

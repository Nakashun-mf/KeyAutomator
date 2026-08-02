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

function Resolve-MsBuildPath {
    # microsoft/setup-msbuild@v2 が設定するパスを最優先
    if ($env:MSBUILD_PATH -and (Test-Path -LiteralPath $env:MSBUILD_PATH)) {
        return $env:MSBUILD_PATH
    }

    $fromPath = Get-Command msbuild.exe -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Source -First 1
    if ($fromPath -and (Test-Path -LiteralPath $fromPath)) {
        return $fromPath
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        # PowerShell のグロブ展開を避けるため単一引用符で渡す
        $found = & $vswhere -latest -requires Microsoft.Component.MSBuild `
            -find 'MSBuild\Current\Bin\MSBuild.exe' 2>$null |
            Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
            Select-Object -First 1
        if ($found) { return $found }
    }

    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )
    return $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

$msbuild = Resolve-MsBuildPath
if (-not $msbuild) {
    throw "MSBuild.exe が見つかりません。"
}

Write-Host "Using MSBuild: $msbuild"

$logPath = Join-Path $outFull "msbuild-msix.log"
$pathFile = Join-Path $outFull "msix-path.txt"

# stderr をパイプしない（ErrorActionPreference=Stop 下で警告が失敗扱いになるため）
# 成功ストリーム汚染を避けるため、結果パスはファイルに書く
$msbuildArgs = @(
    ".\KeyAutomator.csproj"
    "/restore"
    "/p:Configuration=$Configuration"
    "/p:Platform=$Platform"
    "/p:KeyAutomatorPackaged=true"
    "/p:AppxPackageSigningEnabled=true"
    "/p:PackageCertificateKeyFile=$pfxFull"
    "/p:PackageCertificatePassword=$Password"
    "/p:AppxPackageDir=$outFull\"
    "/p:UapAppxPackageBuildMode=SideLoadOnly"
    "/p:AppxBundle=Never"
    "/p:GenerateAppxPackageOnBuild=true"
    "/p:AppxSymbolPackageEnabled=false"
    "/verbosity:minimal"
    "/nologo"
    "/flp:LogFile=$logPath;Verbosity=normal"
)

$proc = Start-Process -FilePath $msbuild -ArgumentList $msbuildArgs -WorkingDirectory $root -Wait -PassThru -NoNewWindow
if ($proc.ExitCode -ne 0) {
    if (Test-Path -LiteralPath $logPath) {
        Write-Host "----- msbuild-msix.log (tail) -----"
        Get-Content -LiteralPath $logPath -Tail 80 | ForEach-Object { Write-Host $_ }
    }
    throw "MSIX ビルドに失敗しました (exit $($proc.ExitCode))"
}

$msix = Get-ChildItem -Path $outFull -Recurse -Filter *.msix | Select-Object -First 1
if (-not $msix) {
    # .msixbundle だけの場合もある
    $bundle = Get-ChildItem -Path $outFull -Recurse -Filter *.msixbundle | Select-Object -First 1
    if ($bundle) {
        $msix = $bundle
    }
}

if (-not $msix) {
    if (Test-Path -LiteralPath $logPath) {
        Write-Host "----- msbuild-msix.log (tail) -----"
        Get-Content -LiteralPath $logPath -Tail 80 | ForEach-Object { Write-Host $_ }
    }
    throw "MSIX ファイルが見つかりません: $outFull"
}

Set-Content -LiteralPath $pathFile -Value $msix.FullName -NoNewline -Encoding utf8
Write-Host "MSIX: $($msix.FullName)"
# 呼び出し側が代入しても壊れないよう、明示的にパスだけ返す
return $msix.FullName

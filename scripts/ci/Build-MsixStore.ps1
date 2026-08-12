#Requires -Version 5.1
<#
.SYNOPSIS
  Microsoft Store 提出用パッケージ（.msixupload / .msixbundle）をビルドする。

.DESCRIPTION
  -p:KeyAutomatorStore=true で StoreUpload モードの MSIX を生成します。
  単一 exe 配布の既定設定は変えません。

  事前条件（Windows 上）:
  1. Partner Center でアプリ名を予約済み
  2. Package.appxmanifest の Identity / Publisher が
     Partner Center「製品の ID」の値になっていること（VS 不要・手書き可）
  3. 署名用証明書（自己署名で可。Store 認定後に Microsoft が再署名）

  詳細: docs/microsoft-store/README.md
#>
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$AppxBundlePlatforms = "x64",
    [string]$PfxPath = ".\KeyAutomator_CI.pfx",
    [string]$CerPath = ".\KeyAutomator_CI.cer",
    [string]$ThumbprintPath = ".\KeyAutomator_CI.thumbprint",
    [string]$Password = "KeyAutomator-CI-Temp!",
    [string]$OutDir = ".\AppPackages\Store"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $root

$manifestPath = Join-Path $root "Package.appxmanifest"
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Package.appxmanifest が見つかりません。"
}

[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$identity = $manifest.Package.Identity
$publisher = [string]$identity.Publisher
$expectedPublisher = "CN=4B1F058B-F39E-44DE-8373-4258152DED0F"
if ($publisher -ne $expectedPublisher) {
    Write-Warning @"
Package.appxmanifest の Publisher が Partner Center 値と一致しません。
  現在: $publisher
  期待: $expectedPublisher
docs/microsoft-store/product-identity.md を確認してください。
"@
}

if (-not (Test-Path -LiteralPath $ThumbprintPath)) {
    & "$PSScriptRoot\New-CiSigningCertificate.ps1" `
        -PfxPath $PfxPath `
        -CerPath $CerPath `
        -ThumbprintPath $ThumbprintPath `
        -Password $Password
}

$thumbprint = (Get-Content -LiteralPath $ThumbprintPath -Raw).Trim()
if (-not $thumbprint) {
    throw "Thumbprint が空です: $ThumbprintPath"
}

$storeCert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Thumbprint -eq $thumbprint } |
    Select-Object -First 1
if (-not $storeCert) {
    if (-not (Test-Path -LiteralPath $PfxPath)) {
        throw "署名証明書がストアにも PFX にもありません。"
    }
    $secure = ConvertTo-SecureString -String $Password -Force -AsPlainText
    $storeCert = Import-PfxCertificate `
        -FilePath $PfxPath `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -Password $secure
    $thumbprint = $storeCert.Thumbprint
    Set-Content -LiteralPath $ThumbprintPath -Value $thumbprint -NoNewline -Encoding ascii
}

Write-Host "Signing thumbprint: $thumbprint"
Write-Host "Signing subject   : $($storeCert.Subject)"
Write-Host "Bundle platforms  : $AppxBundlePlatforms"

if ([System.IO.Path]::IsPathRooted($OutDir)) {
    $outFull = $OutDir
}
else {
    $outFull = Join-Path $root $OutDir
}
Remove-Item -Recurse -Force $outFull -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $outFull | Out-Null

function Resolve-MsBuildPath {
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

$logPath = Join-Path $outFull "msbuild-store.log"
$pathFile = Join-Path $outFull "store-package-path.txt"

$msbuildArgs = @(
    ".\KeyAutomator.csproj"
    "/restore"
    "/p:Configuration=$Configuration"
    "/p:Platform=$Platform"
    "/p:KeyAutomatorStore=true"
    "/p:AppxPackageSigningEnabled=true"
    "/p:PackageCertificateThumbprint=$thumbprint"
    "/p:AppxPackageDir=$outFull\"
    "/p:UapAppxPackageBuildMode=StoreUpload"
    "/p:AppxBundle=Always"
    "/p:AppxBundlePlatforms=$AppxBundlePlatforms"
    "/p:GenerateAppxPackageOnBuild=true"
    "/p:AppxSymbolPackageEnabled=true"
    "/verbosity:minimal"
    "/nologo"
    "/flp:LogFile=$logPath;Verbosity=normal"
)

$proc = Start-Process -FilePath $msbuild -ArgumentList $msbuildArgs -WorkingDirectory $root -Wait -PassThru -NoNewWindow
if ($proc.ExitCode -ne 0) {
    if (Test-Path -LiteralPath $logPath) {
        Write-Host "----- msbuild-store.log (tail) -----"
        Get-Content -LiteralPath $logPath -Tail 80 | ForEach-Object { Write-Host $_ }
    }
    throw "Store MSIX ビルドに失敗しました (exit $($proc.ExitCode))"
}

Write-Host "----- package outputs -----"
Get-ChildItem -Path $outFull -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -match '\.(msixupload|appxupload|msixbundle|appxbundle|msix|appx)$' } |
    ForEach-Object { Write-Host ("{0:N1} MB  {1}" -f ($_.Length / 1MB), $_.FullName) }

$upload = Get-ChildItem -Path $outFull -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -match '\.(msixupload|appxupload)$' } |
    Sort-Object Length -Descending |
    Select-Object -First 1

if (-not $upload) {
    $upload = Get-ChildItem -Path $outFull -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -match '\.(msixbundle|appxbundle)$' } |
        Sort-Object Length -Descending |
        Select-Object -First 1
}

if (-not $upload) {
    if (Test-Path -LiteralPath $logPath) {
        Write-Host "----- msbuild-store.log (tail) -----"
        Get-Content -LiteralPath $logPath -Tail 80 | ForEach-Object { Write-Host $_ }
    }
    throw "Store 提出用パッケージ（.msixupload / .msixbundle）が見つかりません: $outFull"
}

Set-Content -LiteralPath $pathFile -Value $upload.FullName -NoNewline -Encoding utf8
Write-Host "STORE_PACKAGE: $($upload.FullName)"
return $upload.FullName

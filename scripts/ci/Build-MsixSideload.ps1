#Requires -Version 5.1
<#
.SYNOPSIS
  サイドロード用 MSIX をビルドする（単一 exe 既定設定は変えない）。
#>
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$PfxPath = ".\KeyAutomator_CI.pfx",
    [string]$CerPath = ".\KeyAutomator_CI.cer",
    [string]$ThumbprintPath = ".\KeyAutomator_CI.thumbprint",
    [string]$Password = "KeyAutomator-CI-Temp!",
    [string]$OutDir = ".\AppPackages\CI"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $root

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
    # ストアに無い場合は PFX から戻す
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

$outFull = Join-Path $root $OutDir
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

$logPath = Join-Path $outFull "msbuild-msix.log"
$pathFile = Join-Path $outFull "msix-path.txt"

# PackageCertificatePassword 付き PFX は未サポートのため Thumbprint で署名する
$msbuildArgs = @(
    ".\KeyAutomator.csproj"
    "/restore"
    "/p:Configuration=$Configuration"
    "/p:Platform=$Platform"
    "/p:KeyAutomatorPackaged=true"
    "/p:AppxPackageSigningEnabled=true"
    "/p:PackageCertificateThumbprint=$thumbprint"
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

Write-Host "----- package outputs -----"
Get-ChildItem -Path $outFull -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -match '\.(msix|msixbundle)$' } |
    ForEach-Object { Write-Host ("{0:N1} MB  {1}" -f ($_.Length / 1MB), $_.FullName) }

# 依存ランタイム等の小さいパッケージではなく、本体（KeyAutomator / 最大）を選ぶ
$candidates = @(Get-ChildItem -Path $outFull -Recurse -Filter *.msix -ErrorAction SilentlyContinue)
$msix = $candidates |
    Where-Object { $_.Name -match 'KeyAutomator' } |
    Sort-Object Length -Descending |
    Select-Object -First 1
if (-not $msix) {
    $msix = $candidates | Sort-Object Length -Descending | Select-Object -First 1
}
if (-not $msix) {
    $msix = Get-ChildItem -Path $outFull -Recurse -Filter *.msixbundle -ErrorAction SilentlyContinue |
        Sort-Object Length -Descending |
        Select-Object -First 1
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
return $msix.FullName

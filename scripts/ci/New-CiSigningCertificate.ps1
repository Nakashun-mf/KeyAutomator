#Requires -Version 5.1
<#
.SYNOPSIS
  CI / ローカル検証用の自己署名証明書を作成する（Publisher = CN=KeyAutomator）。
#>
param(
    [string]$PfxPath = ".\KeyAutomator_CI.pfx",
    [string]$CerPath = ".\KeyAutomator_CI.cer",
    [string]$Password = "KeyAutomator-CI-Temp!"
)

$ErrorActionPreference = "Stop"

if (Test-Path $PfxPath) { Remove-Item -Force $PfxPath }
if (Test-Path $CerPath) { Remove-Item -Force $CerPath }

$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject "CN=KeyAutomator" `
    -KeyUsage DigitalSignature `
    -FriendlyName "KeyAutomator CI Sideload" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @(
        "2.5.29.37={text}1.3.6.1.5.5.7.3.3",
        "2.5.29.19={text}"
    )

$secure = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $secure | Out-Null
Export-Certificate -Cert $cert -FilePath $CerPath | Out-Null

# ストアから一時証明書を削除（ファイル側をビルドに使う）
Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)" -ErrorAction SilentlyContinue

Write-Host "PFX: $((Resolve-Path $PfxPath).Path)"
Write-Host "CER: $((Resolve-Path $CerPath).Path)"
Write-Host "PASSWORD=$Password"

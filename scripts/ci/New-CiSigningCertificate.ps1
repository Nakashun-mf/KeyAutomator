#Requires -Version 5.1
<#
.SYNOPSIS
  CI / ローカル検証用の自己署名コード署名証明書を作成する。

  Subject は Package.appxmanifest の Identity/@Publisher と一致させること。
  既定は Partner Center の Publisher（Store 提出用）。

  MSBuild の PackageCertificateKeyFile はパスワード付き PFX を扱えないため、
  証明書は CurrentUser\My に残し、Thumbprint で署名する。
#>
param(
    [string]$PfxPath = ".\KeyAutomator_CI.pfx",
    [string]$CerPath = ".\KeyAutomator_CI.cer",
    [string]$ThumbprintPath = ".\KeyAutomator_CI.thumbprint",
    [string]$Password = "KeyAutomator-CI-Temp!",
    # Partner Center Package/Identity/Publisher と一致させる
    [string]$Subject = "CN=4B1F058B-F39E-44DE-8373-4258152DED0F"
)

$ErrorActionPreference = "Stop"

if (Test-Path $PfxPath) { Remove-Item -Force $PfxPath }
if (Test-Path $CerPath) { Remove-Item -Force $CerPath }
if (Test-Path $ThumbprintPath) { Remove-Item -Force $ThumbprintPath }

# 既存の同 Subject 証明書を掃除
Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $Subject } |
    ForEach-Object { Remove-Item -Path $_.PSPath -Force -ErrorAction SilentlyContinue }

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -FriendlyName "KeyAutomator CI / Store package signing" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -HashAlgorithm SHA256 `
    -NotAfter (Get-Date).AddYears(2)

$secure = ConvertTo-SecureString -String $Password -Force -AsPlainText

# バックアップ用に PFX も出す（ビルド署名には Thumbprint を使う）
Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $secure | Out-Null
Export-Certificate -Cert $cert -FilePath $CerPath | Out-Null
Set-Content -LiteralPath $ThumbprintPath -Value $cert.Thumbprint -NoNewline -Encoding ascii

Write-Host "Subject: $Subject"
Write-Host "PFX: $((Resolve-Path $PfxPath).Path)"
Write-Host "CER: $((Resolve-Path $CerPath).Path)"
Write-Host "THUMBPRINT=$($cert.Thumbprint)"
Write-Host "PASSWORD=$Password"
Write-Host "NOTE: 証明書は Cert:\CurrentUser\My に残しています（MSBuild 署名用）。"

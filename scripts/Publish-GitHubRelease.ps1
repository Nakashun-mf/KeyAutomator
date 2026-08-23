#Requires -Version 7
<#
.SYNOPSIS
  main 上のタグから GitHub Release を作成する（zip 添付は release.yml が行う）。
.EXAMPLE
  .\scripts\Publish-GitHubRelease.ps1 -Tag v2.8.8
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^v\d+\.\d+\.\d+$')]
    [string]$Tag,

    [string]$NotesFile = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $NotesFile) {
    $NotesFile = Join-Path $PSScriptRoot "..\docs\releases\$Tag.md"
}

if (-not (Test-Path -LiteralPath $NotesFile)) {
    throw "リリースノートがありません: $NotesFile"
}

$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne "main") {
    throw "main で実行してください（現在: $branch）。feature ブランチにタグを付けないでください。"
}

git tag $Tag
git push origin $Tag
gh release create $Tag --title "KeyAutomator $Tag" --notes-file $NotesFile
Write-Host "Release を作成しました。Actions の Release ワークフローが zip を添付します。"

#Requires -Version 5.1
<#
.SYNOPSIS
  ビルド済み MSIX をインストールし、CLI スモーク試験を行う。
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$MsixPath,
    [string]$CerPath = ".\KeyAutomator_CI.cer",
    [string]$Password = "KeyAutomator-CI-Temp!"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $MsixPath)) {
    throw "MSIX がありません: $MsixPath"
}

function Assert-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "==> $Name"
    & $Action
    Write-Host "OK: $Name"
}

function Resolve-CliExe {
    param([Parameter(Mandatory = $true)]$Package)

    # WindowsApps 実体パスは ACL で直接起動できないことがある。
    # App Execution Alias（%LocalAppData%\Microsoft\WindowsApps\KeyAutomator.exe）を優先する。
    $aliasCandidates = @(
        (Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\KeyAutomator.exe")
        (Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\$($Package.PackageFamilyName)\KeyAutomator.exe")
    )
    foreach ($c in $aliasCandidates) {
        if (Test-Path -LiteralPath $c) {
            Write-Host "CLI via alias: $c"
            return $c
        }
    }

    $fromPath = Get-Command KeyAutomator.exe -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Source -First 1
    if ($fromPath) {
        Write-Host "CLI via PATH: $fromPath"
        return $fromPath
    }

    $fallback = Join-Path $Package.InstallLocation "KeyAutomator.exe"
    Write-Warning "エイリアス未検出。InstallLocation を試します: $fallback"
    return $fallback
}

function Invoke-NativeCapture {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [int]$TimeoutMs = 60000
    )

    $outFile = [System.IO.Path]::GetTempFileName()
    $errFile = [System.IO.Path]::GetTempFileName()
    $argString = ($ArgumentList | ForEach-Object {
            if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
        }) -join ' '

    try {
        $cmdLine = "`"$FilePath`" $argString > `"$outFile`" 2> `"$errFile`""
        Write-Host "Run: cmd /c $cmdLine"
        $proc = Start-Process -FilePath "cmd.exe" `
            -ArgumentList @("/c", $cmdLine) `
            -PassThru `
            -WindowStyle Hidden
        if (-not $proc.WaitForExit($TimeoutMs)) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            throw "タイムアウト (${TimeoutMs}ms): $FilePath $argString"
        }
        $stdout = Get-Content -LiteralPath $outFile -Raw -ErrorAction SilentlyContinue
        $stderr = Get-Content -LiteralPath $errFile -Raw -ErrorAction SilentlyContinue
        return [pscustomobject]@{
            ExitCode = $proc.ExitCode
            StdOut   = $stdout
            StdErr   = $stderr
            Combined = "$( $stdout )$( $stderr )"
        }
    }
    finally {
        Remove-Item -Force $outFile, $errFile -ErrorAction SilentlyContinue
    }
}

Assert-Step "サイドロード許可と証明書登録" {
    New-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" -Force | Out-Null
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" `
        -Name AllowAllTrustedApps -Value 1 -Type DWord -Force
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" `
        -Name AllowDevelopmentWithoutDevLicense -Value 1 -Type DWord -Force

    if (Test-Path -LiteralPath $CerPath) {
        Write-Host "Import CER: $CerPath"
        Import-Certificate -FilePath $CerPath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
        Import-Certificate -FilePath $CerPath -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null
        & certutil.exe -addstore -f TrustedPeople $CerPath | Out-Host
        & certutil.exe -addstore -f Root $CerPath | Out-Host
    }
    else {
        Write-Warning "CER が無いためスキップ: $CerPath"
    }
}

Assert-Step "既存 KeyAutomator パッケージを除去" {
    Get-AppxPackage | Where-Object {
        $_.Name -match "KeyAutomator" -or
        $_.PackageFullName -match "58AAB0EC-5590-46F6-AEE7-2AEF1231D0E5"
    } | ForEach-Object {
        Write-Host "Remove $($_.PackageFullName)"
        Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue
    }
}

Assert-Step "MSIX をサイドロードインストール" {
    Write-Host "Add-AppxPackage: $MsixPath"
    Get-Item -LiteralPath $MsixPath | Format-List FullName, Length, LastWriteTime | Out-String | Write-Host
    Add-AppxPackage -Path $MsixPath -ForceApplicationShutdown -ErrorAction Stop
}

$pkg = Get-AppxPackage | Where-Object {
    $_.Name -match "KeyAutomator" -or
    $_.PackageFullName -match "58AAB0EC-5590-46F6-AEE7-2AEF1231D0E5"
} | Select-Object -First 1

if (-not $pkg) {
    throw "インストール後に AppxPackage が見つかりません"
}

Write-Host "Installed: $($pkg.PackageFullName)"
Write-Host "Location : $($pkg.InstallLocation)"
Write-Host "Family   : $($pkg.PackageFamilyName)"

# エイリアス作成待ち（インストール直後は少し遅れることがある）
$exe = $null
foreach ($i in 1..10) {
    $exe = Resolve-CliExe -Package $pkg
    if ($exe -and (Test-Path -LiteralPath $exe) -and ($exe -notmatch '\\WindowsApps\\58AAB0EC')) {
        break
    }
    Start-Sleep -Seconds 1
}
if (-not $exe -or -not (Test-Path -LiteralPath $exe)) {
    throw "CLI 起動用 exe / エイリアスが見つかりません"
}

Assert-Step "CLI ヘルプ (-h)" {
    $result = Invoke-NativeCapture -FilePath $exe -ArgumentList @("-h") -TimeoutMs 60000
    Write-Host "exit=$($result.ExitCode)"
    Write-Host $result.Combined

    $log = Join-Path $env:LOCALAPPDATA "KeyAutomator\error.log"
    if (Test-Path -LiteralPath $log) {
        Write-Host "----- error.log -----"
        Get-Content -LiteralPath $log -Tail 40 | ForEach-Object { Write-Host $_ }
    }

    if ($result.ExitCode -ne 0) {
        throw "-h の終了コードが $($result.ExitCode)"
    }
    if ($result.Combined -notmatch "KeyAutomator") {
        throw "-h 出力に KeyAutomator が含まれません"
    }
    if ($result.Combined -notmatch "確認アクション") {
        throw "-h に確認アクションの注意がありません"
    }
}

Assert-Step "パッケージ実行で設定がユーザー領域へ作られる" {
    $result = Invoke-NativeCapture -FilePath $exe -ArgumentList @("-alias", "select_copy") -TimeoutMs 90000
    Write-Host "select_copy exit=$($result.ExitCode)"
    if ($result.Combined) { Write-Host $result.Combined }
    if ($result.ExitCode -ne 0) {
        Write-Warning "select_copy exit=$($result.ExitCode)（入力先が無い環境では失敗し得る）"
    }

    $dataDir = Join-Path $env:LOCALAPPDATA "KeyAutomator"
    $config = Join-Path $dataDir "config.json"
    if (-not (Test-Path -LiteralPath $config)) {
        Write-Host "LocalAppData KeyAutomator:"
        if (Test-Path -LiteralPath $dataDir) {
            Get-ChildItem -LiteralPath $dataDir | ForEach-Object { Write-Host $_.FullName }
        }
        else {
            Write-Host "(directory missing) $dataDir"
        }
        $log = Join-Path $dataDir "error.log"
        if (Test-Path -LiteralPath $log) {
            Write-Host "----- error.log -----"
            Get-Content -LiteralPath $log -Tail 40 | ForEach-Object { Write-Host $_ }
        }
        throw "config.json がありません（パッケージ時は LocalAppData 期待）: $config"
    }

    $bad = Join-Path $pkg.InstallLocation "config.json"
    if (Test-Path -LiteralPath $bad) {
        throw "インストール先に config.json が作られてしまいました: $bad"
    }

    Write-Host "config: $config"
}

Assert-Step "クリーンアップ（アンインストール）" {
    Remove-AppxPackage -Package $pkg.PackageFullName
}

Write-Host "SMOKE PASSED"

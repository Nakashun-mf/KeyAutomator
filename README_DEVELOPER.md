# KeyAutomator — 開発者向け README

## 概要

C# / .NET 8 / **WinUI 3**（Windows App SDK）製のキー入力自動化ツールです。  
キー送信は Win32 `SendInput`（Unicode / Virtual-Key）を使用します。

**バージョン:** 2.8.4

## 開発環境

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022（推奨）または Cursor + .NET SDK

## プロジェクト構成

```
vb_auto-key/
├── Program.cs                 # CLI / GUI 分岐（カスタム Main）
├── App.xaml(.cs)
├── MainWindow.xaml(.cs)       # Fluent UI 管理画面
├── ViewModels/MainViewModel.cs
├── Models/MacroModels.cs
├── Services/                  # Config / Settings / Paths / SendInput / Repeat / CLI / Log / Dialog
├── KeyAutomator.Tests/        # MSTest ユニットテスト
├── Assets/
├── SPEC.md
├── config.sample.json
├── README.md
└── README_DEVELOPER.md
```

## ビルド

```powershell
cd C:\00_coding\vb_auto-key
dotnet restore
dotnet build -c Release -p:Platform=x64
```

実行:

```powershell
dotnet run -c Release -p:Platform=x64
```

CLI:

```powershell
dotnet run -c Release -p:Platform=x64 -- -1
```

## 品質ゲート（公開前に必ず通す）

公開アプリとして、次の 2 系統をどちらも通す。

| ゲート | 何を守るか | 実行場所 |
|---|---|---|
| ユニットテスト | CLI 解決、config/settings の読み書き、繰り返し、エイリアス検証など。実データの `config.json` は触らない | Windows（`KeyAutomator.Tests`） |
| MSIX サイドロード スモーク | パッケージのビルド〜インストール通し | GitHub Actions `windows-latest` |

Linux 上では WinUI の XAML コンパイラが動かないため、`dotnet test` / `dotnet build` は失敗する（想定どおり）。検証は Windows か CI で行う。

### 自動テストで守る範囲 / 守らない範囲

法人向けに出す前提で、**ロジックとキーエンコードと GUI 起動は CI で落とす。** 実フォーカス先や IME の見た目だけが実機確認になる。

| 自動（CI / `KeyAutomator.Tests` + MSIX スモーク） | まだ実機が必要なもの |
|---|---|
| CLI 解決の全記法、破損 JSON、エイリアスの文字コード網羅（BMP 先頭〜かな） | IME 変換中の入力、実アプリへの貼り付け結果 |
| キー／ホットキー／マウス／テキストの **SendInput 内容**（差し替えキャプチャ。実キーは送らない） | アクティブウィンドウが意図したアプリか（フォーカス競合） |
| GUI 起動（ウィンドウタイトル）。UIA で新規ボタンを押せる場合は押す | 高 DPI・複数ディスプレイ・スクリーンリーダーの聞き取り |
| 未知マクロ CLI がキー送信せず終わる | SmartScreen / 証明書 UI / Store 審査そのもの |
| サンプル JSON とバージョン番号の一致 | |

件数は「メソッドを 5000 個書く」ではなく、**文字・キー・CLI の組み合わせを機械生成**する。同じ関数を 5000 回コピペしても品質は上がらない。会社がやるのもこの方式（プロパティテスト／データ駆動）である。

`SendInput` の中身は CI で検証する。本物のキーを GitHub runner に流し込むのはしない（他ジョブを壊す）。ウィンドウ起動と UIA は **MSIX スモーク（windows-latest）** で自動実行する。

### ユニットテスト

ローカル（Windows）:

```powershell
$Platform = $env:PROCESSOR_ARCHITECTURE
# CI と同じ経路（MSBuild でビルド → dotnet test --no-build）
.\scripts\ci\Run-UnitTests.ps1 -Configuration Debug -Platform $Platform

# または SDK 直接（Visual Studio / Windows App SDK が揃っている場合）
dotnet test .\KeyAutomator.Tests\KeyAutomator.Tests.csproj -c Debug -p:Platform=$Platform
```

テストは一時フォルダへデータディレクトリを差し替える。開発者の `%LocalAppData%\KeyAutomator` や exe 横の設定は書き換えない。

### CI

- `Unit Tests`（`.github/workflows/unit-tests.yml`）: 上記スクリプトを Windows runner で実行。失敗すると成果物 `TestResults/` を残す
- `MSIX Sideload Smoke`（`.github/workflows/msix-sideload.yml`）: インストール通し。単体テストは重複実行しない

## 配布用 publish（単一 exe・正式手段）

WinUI 3（非パッケージ）は `PublishSingleFile` + `IncludeAllContentForSelfExtract` で **単一 exe** にできます。  
初回起動時に一時フォルダへ自己展開します。設定ファイルは `AppPaths` により **書き込み可能なデータフォルダ** に書きます（exe 横が書けるならそこ。保護フォルダ・パッケージ実行時は `%LocalAppData%\KeyAutomator`。展開先 temp には書きません）。

```powershell
# 実行中の KeyAutomator を終了してから
# ※ 出力先は空のフォルダにする（古い exe が残っているとバンドルが肥大化する）
Get-Process KeyAutomator -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item -Recurse -Force .\publish-sf -ErrorAction SilentlyContinue
dotnet publish -c Release -p:Platform=x64 -r win-x64 --self-contained true -o .\publish-sf
```

成果物: `publish-sf\KeyAutomator.exe`（目安 **約 66MB**。配布はこの 1 ファイルで可）

サイズ削減の要点:

- `EnableCompressionInSingleFile=true`
- `PublishReadyToRun=false`（サイズ優先。起動速度より軽さを取る）
- Windows App SDK はバージョン固定（`*` は巨大な新メジャーを拾うことがある）
- 出力先を空にしてから publish（既存の巨大 exe を再バンドルしない）
- `dist/` / `publish*/` を `DefaultItemExcludes` で除外

持ち運び用 zip の例:

```powershell
$ver = "2.8.4"
$distName = "KeyAutomator-v$ver-win-x64-single"
$distDir = ".\dist\$distName"
Remove-Item -Recurse -Force .\dist -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Copy-Item -Force .\publish-sf\KeyAutomator.exe $distDir\
Copy-Item -Force .\config.sample.json $distDir\
Copy-Item -Force .\使い方.txt $distDir\
Copy-Item -Force .\README.md $distDir\
Copy-Item -Force .\PRIVACY.md $distDir\
Compress-Archive -Path $distDir -DestinationPath ".\dist\$distName.zip" -Force
```

主な csproj 設定（単一 exe・既定）:

- `WindowsPackageType=None`（非パッケージ）
- `WindowsAppSDKSelfContained=true` / `SelfContained=true`
- `PublishSingleFile=true`
- `IncludeAllContentForSelfExtract=true`
- `IncludeNativeLibrariesForSelfExtract=true`
- `PublishTrimmed=false`（WinUI は Trim で壊れやすい）

- `publish-sf/` / `dist/` は `.gitignore` 対象（zip はリポジトリに含めない）

## MSIX サイドロード（ローカルインストール用）

単一 exe 以外に、PC へインストールする形（MSIX）でもビルドできます。  
設定ファイルはパッケージ実行時 `%LocalAppData%\KeyAutomator` に保存されます（`AppPaths`）。

### Visual Studio から（推奨）

1. Windows で Developer Mode を有効化
2. ソリューションを開き、プロジェクトを右クリック → **Package and Publish** → **Create App Packages...**
3. **Sideloading** を選ぶ（ローカルインストール用）
4. 証明書は開発用の自動作成で可（自分の PC / 検証用）
5. アーキテクチャは当面 **x64** を選択
6. 出力された `.msix` / `.msixbundle` を登録

```powershell
Add-AppxPackage -Path .\AppPackages\...\KeyAutomator_*.msixbundle
```

アンインストール例:

```powershell
Get-AppxPackage *KeyAutomator* | Remove-AppxPackage
```

### コマンドラインから（実験的）

既定の単一 exe 設定を崩さないよう、フラグで切り替えます。

```powershell
$Platform = $env:PROCESSOR_ARCHITECTURE
dotnet build .\KeyAutomator.csproj -c Release -p:Platform=$Platform -p:KeyAutomatorPackaged=true
```

環境や SDK によっては Visual Studio のウィザードの方が安定します。失敗時はウィザード経路を使ってください。

### Microsoft Store 提出用（VS なし可）

**Visual Studio は不要です。** Partner Center の Identity をマニフェストに手書きし、Release の `.msix`（または `.msixupload`）をアップロードします。

1. Partner Center でアプリ名を予約 → **製品の管理 → 製品の ID** を開く
2. 表示された Name / Publisher / PublisherDisplayName を `Package.appxmanifest` に転記（現状は転記済み。詳細は [docs/microsoft-store/product-identity.md](docs/microsoft-store/product-identity.md)）
3. Windows 上で次のいずれか:

```powershell
# A. Release .msix をそのまま提出（シンプル）
.\scripts\ci\New-CiSigningCertificate.ps1
.\scripts\ci\Build-MsixSideload.ps1 -Configuration Release -Platform x64

# B. .msixupload（推奨・シンボル付き）
.\scripts\ci\New-CiSigningCertificate.ps1
.\scripts\ci\Build-MsixStore.ps1 -Platform x64 -AppxBundlePlatforms "x64"
```

認定後、Microsoft がパッケージを再署名します。詳細は [docs/microsoft-store/README.md](docs/microsoft-store/README.md)。

### CI（GitHub Actions）

ワークフロー `MSIX Sideload Smoke`（`.github/workflows/msix-sideload.yml`）が Windows runner 上で次を行います。

1. 自己署名証明書の作成（Publisher と同一 Subject、署名は Thumbprint 方式）
2. MSIX サイドロードビルド（成果物は `$RUNNER_TEMP`）
3. インストール → `WindowsApps` 配置 / `AppExecutionAlias` / アンインストールを確認
4. 可能ならパッケージ CLI で `%LocalAppData%\KeyAutomator\config.json` 作成も確認  
   （AppExecutionAlias + WinExe は CI 上で終了コードや起動が不安定なため、未作成でもインストール通し成功扱い）
5. 巨大 MSIX の Artifact アップロードは、スモーク CI では行わない（runner 切断防止）
6. **GitHub Release**（`.github/workflows/release.yml`）では単一 exe zip に加え、MSIX zip（`.msix` + 署名用 `.cer` + 入れ方）も添付する
7. **Unit Tests**（`.github/workflows/unit-tests.yml`）が PR / main で `scripts/ci/Run-UnitTests.ps1` を実行する（MSIX スモークとは別ジョブ）

手動実行:

```powershell
# ユニットテスト（Windows）
.\scripts\ci\Run-UnitTests.ps1 -Configuration Release -Platform x64

# MSIX スモーク
.\scripts\ci\New-CiSigningCertificate.ps1
# 署名は CurrentUser\My の Thumbprint 経由（パスワード付き PFX 直指定は MSBuild 未サポート）
$msix = .\scripts\ci\Build-MsixSideload.ps1
.\scripts\ci\Smoke-MsixSideload.ps1 -MsixPath $msix
```

## パッケージマニフェスト

`Package.appxmanifest` は MSIX / サイドロード / Store 用の定義です（単一 exe 配布では使いません）。

- **Version** は本体（`.csproj` の `Version`）と揃える（4 部。Store では末尾 `0` 推奨）
- **Identity / Publisher** はサイドロード用の仮値。Store 提出前に VS の「ストアに関連付ける」で Partner Center の値へ更新する
- 権限は必要最小限（`runFullTrust` のみ。キー送信に使用）
- 未使用の Capability は追加しない
- CLI 用に `AppExecutionAlias`（`KeyAutomator.exe`）を定義。インストール後は `%LocalAppData%\Microsoft\WindowsApps` 経由で `-h` / `-alias` を呼べる（`WindowsApps` 実体パスの直実行は ACL で失敗し得る）
- Store 提出の手順・文言: [docs/microsoft-store/README.md](docs/microsoft-store/README.md)

## アーキテクチャ

- UI: WinUI 3 + MVVM（CommunityToolkit.Mvvm）
- 設定: `config.json`（スキーマは SPEC.md）／パス解決は `Services/AppPaths.cs`
- CLI: `Program.Main` で引数がある場合はウィンドウを出さず実行

## Git

- リポジトリ: https://github.com/Nakashun-mf/KeyAutomator （Public / MIT）
- 配布バイナリは GitHub Releases に添付
- タグ `v*` の Release 公開時（または Actions の `Release` ワークフロー手動実行）に Windows 上で次をビルドして添付する
  - `KeyAutomator-v*-win-x64-single.zip`（単一 exe）
  - `KeyAutomator-v*-win-x64-msix.zip`（サイドロード用 MSIX + 署名証明書 + 入れ方）
- コミットメッセージは日本語（ファイル経由推奨）
- `bin/`, `obj/`, `publish/`, `publish-sf/`, `dist/`, `config.json`, `settings.json`, `error.log` は `.gitignore` 対象

## バージョン更新

`KeyAutomator.csproj` の Version 系と README の表記を揃えて更新してください。

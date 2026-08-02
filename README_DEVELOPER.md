# KeyAutomator — 開発者向け README

## 概要

C# / .NET 8 / **WinUI 3**（Windows App SDK）製のキー入力自動化ツールです。  
キー送信は Win32 `SendInput`（Unicode / Virtual-Key）を使用します。

**バージョン:** 2.8.0

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

## ユニットテスト

```powershell
$Platform = $env:PROCESSOR_ARCHITECTURE
dotnet test .\KeyAutomator.Tests\KeyAutomator.Tests.csproj -c Debug -p:Platform=$Platform
```

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
$ver = "2.8.0"
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

## パッケージマニフェスト

`Package.appxmanifest` は MSIX / サイドロード用の定義です（単一 exe 配布では使いません）。

- **Version** は本体（`.csproj` の `Version`）と揃える
- 権限は必要最小限（`runFullTrust` のみ。キー送信に使用）
- 未使用の Capability は追加しない

## アーキテクチャ

- UI: WinUI 3 + MVVM（CommunityToolkit.Mvvm）
- 設定: `config.json`（スキーマは SPEC.md）／パス解決は `Services/AppPaths.cs`
- CLI: `Program.Main` で引数がある場合はウィンドウを出さず実行

## Git

- リポジトリ: https://github.com/Nakashun-mf/KeyAutomator （Public / MIT）
- 配布バイナリは GitHub Releases に添付
- タグ `v*` の Release 公開時（または Actions の `Release` ワークフロー手動実行）に Windows 上で単一 exe をビルドし zip を添付
- コミットメッセージは日本語（ファイル経由推奨）
- `bin/`, `obj/`, `publish/`, `publish-sf/`, `dist/`, `config.json`, `settings.json`, `error.log` は `.gitignore` 対象

## バージョン更新

`KeyAutomator.csproj` の Version 系と README の表記を揃えて更新してください。

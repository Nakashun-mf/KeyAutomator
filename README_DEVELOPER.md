# KeyAutomator — 開発者向け README

## 概要

C# / .NET 8 / **WinUI 3**（Windows App SDK）製のキー入力自動化ツールです。  
キー送信は Win32 `SendInput`（Unicode / Virtual-Key）を使用します。

**バージョン:** 2.6.0

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
├── Services/                  # Config / Settings / Paths / SendInput / CLI / Log / Dialog
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
初回起動時に一時フォルダへ自己展開します。設定ファイルは `AppPaths` により **exe と同じフォルダ** に書きます（展開先 temp には書きません）。

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
$ver = "2.6.0"
$distName = "KeyAutomator-v$ver-win-x64-single"
$distDir = ".\dist\$distName"
Remove-Item -Recurse -Force .\dist -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Copy-Item -Force .\publish-sf\KeyAutomator.exe $distDir\
Copy-Item -Force .\config.sample.json $distDir\
# 使い方.txt は dist 作成時に別途配置
Compress-Archive -Path $distDir -DestinationPath ".\dist\$distName.zip" -Force
```

主な csproj 設定:

- `WindowsPackageType=None`（非 MSIX）
- `WindowsAppSDKSelfContained=true` / `SelfContained=true`
- `PublishSingleFile=true`
- `IncludeAllContentForSelfExtract=true`
- `IncludeNativeLibrariesForSelfExtract=true`
- `PublishTrimmed=false`（WinUI は Trim で壊れやすい）

- `publish-sf/` / `dist/` は `.gitignore` 対象（zip はリポジトリに含めない）

MSIX パッケージ化が必要な場合は `Package.appxmanifest` を利用し、プロジェクトの Package and Publish から作成できます。

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

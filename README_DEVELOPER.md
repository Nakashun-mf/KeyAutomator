# KeyAutomator — 開発者向け README

## 概要

C# / .NET 8 / **WinUI 3**（Windows App SDK）製のキー入力自動化ツールです。  
キー送信は Win32 `SendInput`（Unicode / Virtual-Key）を使用します。

**バージョン:** 2.0.7

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
├── Services/                  # Config / Settings / SendInput / CLI / Log
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

## 配布用 publish（自己完結フォルダ）

WinUI は単一ファイル化が難しいため、**フォルダ配布**を正式手段とします。

```powershell
dotnet publish -c Release -p:Platform=x64 -r win-x64 --self-contained true -o .\publish
```

成果物: `publish\KeyAutomator.exe` と依存ファイル一式。

- `WindowsPackageType=None`（非 MSIX）
- `WindowsAppSDKSelfContained=true`（ランタイム同梱）

MSIX パッケージ化が必要な場合は `Package.appxmanifest` を利用し、プロジェクトの Package and Publish から作成できます。

## アーキテクチャ

- UI: WinUI 3 + MVVM（CommunityToolkit.Mvvm）
- 設定: `config.json`（スキーマは SPEC.md）
- CLI: `Program.Main` で引数がある場合はウィンドウを出さず実行

## Git

- コミットメッセージは日本語（ファイル経由推奨）
- `bin/`, `obj/`, `publish/`, `config.json`, `error.log` は `.gitignore` 対象

## バージョン更新

`KeyAutomator.csproj` の Version 系と README の表記を揃えて更新してください。

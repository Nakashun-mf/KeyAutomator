# KeyAutomator — 開発者向け README

## 概要

VB.NET / .NET 8 / WinForms 製のキー入力自動化ツールです。  
キー送信は Win32 `SendInput`（Unicode / Virtual-Key）を使用します。

**バージョン:** 1.0.0

## 開発環境

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 または Cursor / VS Code + .NET SDK

仮想環境相当として、SDK のローカルビルドと自己完結 publish を利用します（ランタイム同梱の単体 exe）。

## プロジェクト構成

```
vb_auto-key/
├── Program.vb                 # GUI / CLI 分岐
├── Forms/
│   ├── MainForm.vb            # 管理画面
│   └── HotkeyCaptureForm.vb   # ショートカット記録
├── Models/
│   ├── MacroModels.vb         # データモデル
│   └── ConfigStore.vb         # config.json I/O
├── Services/
│   ├── NativeMethods.vb       # SendInput P/Invoke
│   ├── KeySender.vb           # アクション実行
│   ├── CliRunner.vb           # CLI 解釈
│   └── ErrorLogger.vb         # error.log
├── SPEC.md                    # 基本設計仕様
├── config.sample.json         # 設定サンプル
├── README.md                  # 利用者向け
└── README_DEVELOPER.md        # 本ファイル
```

## ビルド

```powershell
cd C:\00_coding\vb_auto-key
dotnet restore
dotnet build -c Release
```

デバッグ実行:

```powershell
dotnet run -c Debug
```

CLI テスト例:

```powershell
dotnet run -c Release -- -1
```

## 単体 exe の公開（配布用）

exe 1 ファイルで配布できる自己完結・単一ファイル公開:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o .\publish
```

成果物: `publish\KeyAutomator.exe`

- .NET ランタイム不要
- `config.json` は exe と同じディレクトリに配置（初回自動生成可）

フレームワーク依存の軽量版が必要な場合:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true `
  -o .\publish-fd
```

（この場合は対象マシンに .NET 8 Desktop Runtime が必要）

## 設定スキーマ

`SPEC.md` および `config.sample.json` を参照。アクション種別:

| type | value 例 |
|---|---|
| text | `Hello` |
| key | `ENTER`, `TAB` |
| hotkey | `CTRL+S`, `CTRL+SHIFT+A` |
| wait | `0.5`（秒） |

## Git

- コミットメッセージは日本語（ファイル経由推奨）
- `bin/`, `obj/`, `publish/`, `config.json`, `error.log` は `.gitignore` 対象

## バージョン更新

`KeyAutomator.vbproj` の `Version` / `AssemblyVersion` / `FileVersion` / `InformationalVersion` を揃えて更新し、README のバージョン表記も合わせてください。

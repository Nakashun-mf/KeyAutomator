# KeyAutomator — 開発者向け README

## 概要

C# / .NET 8 / **WinUI 3**（Windows App SDK）製のキー入力自動化ツールです。  
キー送信は Win32 `SendInput`（Unicode / Virtual-Key）を使用します。

**バージョン:** 2.8.7

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
├── Strings/en-US/Resources.resw
├── Strings/ja-JP/Resources.resw
├── Services/                  # Config / Settings / Paths / Loc / SendInput / Repeat / CLI / Log / Dialog
├── KeyAutomator.Tests/        # MSTest ユニットテスト
├── Assets/
├── SPEC.md
├── config.sample.json
├── config.sample.en.json
├── README.md
├── PRIVACY.md
├── PRIVACY.en.md
├── docs/releases/             # GitHub Release 用ノート
├── docs/microsoft-store/      # Partner Center 文言・掲載画像
└── README_DEVELOPER.md
```

## 表示言語（日本語 / English）

- 既定は Windows の表示言語。日本語以外は English にフォールバックする。
- 画面右下の「言語」で `Windows に合わせる` / `日本語` / `English` を固定できる（`settings.json` の `ui_language`）。
- UI 文字列は `Strings/en-US/Resources.resw` と `Strings/ja-JP/Resources.resw`。XAML は `x:Uid`、C# / CLI は `Loc.Get` / `Loc.Format`。
- 初回サンプル名・確認ダイアログ、`error.log`、例外メッセージも同じテーブル。`config.sample.json`（日本語）と `config.sample.en.json`（English）を同梱し、読み込み時に UI 言語で上書きする。
- ユニットテストは `TestStartup` で `ja-JP` に固定する（既存の日本語アサートを維持）。英語は `LocTests` で別途確認。

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

Microsoft Store に出す前提で、次を **両方** 通す。件数を増やすより、壊れ方と提出契約を固定する。

| ゲート | 何を守るか | 実行場所 |
|---|---|---|
| ユニットテスト | CLI・設定・破損耐性・SendInput キャプチャ・**Store 提出契約**（権限・通信なし・Identity・ダミーサンプル） | Windows CI `Unit Tests` |
| E2E / MSIX スモーク | インストール、未知 CLI、GUI で新規→保存→CLI→再起動、**インストール済みマニフェストが runFullTrust のみ** | Windows CI `MSIX Sideload Smoke` |

Linux 上では WinUI の XAML コンパイラが動かないため、`dotnet test` / `dotnet build` は失敗する（想定どおり）。検証は Windows か CI で行う。

### 自動テストで守る範囲 / 守らない範囲

件数は代表例＋ループ 1 本に畳んである（数千件の組み合わせ表は持たない）。

Store 提出で他社より安心できる、と説明するための自動チェック:

- Capability は `runFullTrust` のみ（`internetClient` などを足したらテストが落ちる）
- 本番コードに `HttpClient` / テレメトリ型が無い
- `PRIVACY.md` / `PRIVACY.en.md` と `runFullTrust` 正当化文が残っている
- Partner Center Identity とマニフェストが一致する
- サンプルのパスワードらしき文字列は `dummy` 等だと分かるものだけ
- テストは開発者の `%LocalAppData%\KeyAutomator` を書き換えない
- 設定の原子書き込み（途中クラッシュで JSON が空になりにくい）

E2E は **キー送信（テスト実行ボタン）をしない**。CI ランナー上で `SendInput` するとフォーカス先が不定で、入力漏れやハングの原因になる。代わりに次だけを Windows CI で回す。

1. MSIX インストールとエイリアス起動
2. GUI: 新規 → 名前変更 → 保存
3. パッケージの `LocalCache` 上の `config.json` に名前が残ること
4. 空手順マクロを CLI `-name` で実行し、キーを飛ばさず終了すること
5. GUI 再起動後も一覧に残ること

| 自動 | 実機のまま |
|---|---|
| 単体: CLI、破損 JSON、エイリアス代表例、SendInput キャプチャ | IME、実アプリへの入力、フォーカス競合 |
| E2E: 起動 → 新規 → 保存 → CLI → 再起動 | 高 DPI・複数ディスプレイ・スクリーンリーダーの聞き取り |
| 未知マクロ CLI がキー送信せず終わる | SmartScreen / 証明書 UI / Store 審査 |

増やさないもの: 削除・複製・ダイアログ確認の UI 操作。ロジックは単体テスト側。手順付きマクロの実キー送信も自動にしない。

ローカルで E2E だけ回す（インストール済みエイリアスが必要）:

```powershell
.\scripts\ci\Invoke-UiE2E.ps1 `
  -AliasPath "$env:LOCALAPPDATA\Microsoft\WindowsApps\KeyAutomator.exe" `
  -PackageFamilyName "pryzo.KeyAutomator_29frz59n2q2dp"
```

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
$ver = "2.8.7"
$distName = "KeyAutomator-v$ver-win-x64-single"
$distDir = ".\dist\$distName"
Remove-Item -Recurse -Force .\dist -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
Copy-Item -Force .\publish-sf\KeyAutomator.exe $distDir\
Copy-Item -Force .\config.sample.json $distDir\
Copy-Item -Force .\config.sample.en.json $distDir\
Copy-Item -Force .\使い方.txt $distDir\
Copy-Item -Force .\GettingStarted.txt $distDir\
Copy-Item -Force .\README.md $distDir\
Copy-Item -Force .\PRIVACY.md $distDir\
Copy-Item -Force .\PRIVACY.en.md $distDir\
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
6. **GitHub Release**（`.github/workflows/release.yml`）では単一 exe zip に加え、MSIX zip（`.msix` + 署名用 `.cer` + 入れ方）も添付する。`release: published` 時は続けて Store 用 `.msixupload` を `msstore publish` する（Secrets 必須。手動実行では `publish_store` をオンにしたときだけ）
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
- CLI 用に `AppExecutionAlias`（`KeyAutomator.exe`）を定義。インストール後は `%LocalAppData%\Microsoft\WindowsApps` 経由で `-h` / `-alias` を呼べる（`WindowsApps` 実体パスの直実行は ACL で失敗し得る）。GUI の「パス＋引数をコピー」（テスト実行の右）と「起動パスをコピー」（画面下）はこのエイリアスパスを返す。マクロ選択中は `-alias` / `-id` も付ける。
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
  - zip には `PRIVACY.md` / `PRIVACY.en.md` / `GettingStarted.txt` も含む
- 同じ Release 公開で Microsoft Store へも `.msixupload` を自動提出する（認定待ち。Secrets はリポジトリに書かない）
- 手順の詳細: [docs/releases/README.md](docs/releases/README.md)
- コミットメッセージは日本語（ファイル経由推奨）
- `bin/`, `obj/`, `publish/`, `publish-sf/`, `dist/`, `config.json`, `settings.json`, `error.log` は `.gitignore` 対象

## バージョン更新

次を揃えて更新してください。

- `KeyAutomator.csproj` の `Version` / `AssemblyVersion` / `FileVersion` / `InformationalVersion`
- `Package.appxmanifest` の `Identity/@Version`（4 部。末尾 `0`）
- `README.md` / `README_DEVELOPER.md` の表記
- `docs/releases/vX.Y.Z.md`（ユニットテストが csproj の Version と一致することを確認する）

GitHub Release の出し方は [docs/releases/README.md](docs/releases/README.md)。`scripts/Publish-GitHubRelease.ps1` は **main マージ後**に Windows 等で実行します（この Linux 環境の `gh` は読み取り専用のため Release は作れません）。

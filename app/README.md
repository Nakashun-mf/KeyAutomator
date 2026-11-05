# KeyAutomator — Electron 版

WinUI 3 版の後継です。キー送信ロジックと設定ファイル形式はそのままに、UI を Electron + React + TypeScript で作り直しています。
VSCode / Discord のような暗めのワークスペース UI、コマンドパレット（`Ctrl+K`）、起動スプラッシュを備えます。

| 項目 | 内容 |
|---|---|
| 実行環境 | Electron / Node 22+（開発）、Windows 10 1809+ / 11（実行） |
| UI | React 19 + TypeScript、依存の少ない素の CSS（色などは `src/renderer/src/styles/tokens.css` に集約） |
| キー送信 | Win32 `SendInput`（[koffi](https://koffi.dev/) 経由。ネイティブビルド不要） |
| 配布 | MSIX（Microsoft Store）と zip（ポータブル）。`electron-builder.yml` |
| 互換 | `config.json` / `settings.json` / CLI 引数は WinUI 版と同じ（SPEC.md §3〜§4） |

## 開発

```bash
cd app
npm ci
npm run dev          # 開発起動（Windows。データは app/.devdata に保存）
npm run typecheck
npm test             # 単体テスト（Linux / Windows どちらでも可）
npm run build        # out/ に main / preload / renderer を生成
npm run verify:build # ビルド成果物の健全性チェック
```

UI の自動操作テスト（Playwright）:

```bash
npx playwright install chromium
npm run e2e:renderer                 # renderer をモック API で操作（Chromium のみ）
npm run e2e:electron                 # 実 Electron を起動して通しで確認（Linux は xvfb-run -a を付ける）
```

`e2e:electron` は「テスト実行」ボタンを **Windows では押しません**（実際のキー入力が飛ぶため）。

配布物の作成（Windows）:

```bash
npm run dist:win     # dist/ に zip と MSIX(.appx) を生成（未署名・Store の Identity）
```

## 構成

```
app/
├── src/core/        # 純粋なロジック（Electron にも DOM にも依存しない。単体テストの主対象）
│   ├── runner.ts      repeat / end_repeat の解釈、SendInput バックエンドの抽象化、中断
│   ├── keys.ts        キー名 → Virtual-Key
│   ├── actions.ts     手順の種別・入力検証・ショートカット検証
│   ├── editor.ts      エディタ操作（保存検証、繰り返しで囲む、並べ替え…）
│   ├── store.ts       config.json / settings.json / error.log（一時ファイル経由で原子的に保存）
│   ├── paths.ts       データ保存先（MSIX / 保護フォルダ / ポータブル）
│   ├── cli.ts         引数の解釈（-id / -alias / -name / -NAME …）
│   └── i18n/          ja-JP.json / en-US.json（旧 .resw と同じキー）
├── src/native/win32.ts   SendInput / AttachConsole（koffi）
├── src/main/            # Electron main: 起動分岐（GUI / CLI）、IPC、スプラッシュ、最前面の確認ダイアログ
├── src/preload/         # contextBridge で renderer へ公開する API（sandbox のため単一ファイル）
├── src/renderer/        # React UI（index.html）と、起動スプラッシュ（splash.html）
├── tests/               # vitest
└── e2e/                 # Playwright
```

### 起動の流れ

1. `app.whenReady()` 直後にスプラッシュ（枠なし・React なしの軽量ページ）を出す。
2. 裏でメインウィンドウを `show: false` で作り、React が初回描画を終えたら `notifyReady` を受けて切り替える（最短 500ms 表示してちらつきを防ぐ）。
3. 通知が来なくても 10 秒でメインを出す。
4. 引数ありの起動（CLI）はウィンドウを作らず、マクロを実行して終了コード 0 / 1 で終わる。`dialog` 手順だけは確認ウィンドウを出す。

スプラッシュのアニメーションは CSS のみ。OS の「視覚効果を減らす」が有効なときは動きを止めます。

## 互換性メモ

- `config.json` のキーは `id / name / alias / delay_sec / actions[{type,value}]`（WinUI 版と同一）。BOM 付き・欠けたキーも読めます。
- 壊れた `config.json` は上書きせず、`.bak.<日時>` にバックアップして空で起動します。
- データ保存先: MSIX・`Program Files` 配下・書き込み不可の場所は `%LocalAppData%\KeyAutomator`、書き込み可能な場所に置いた zip 版は exe 横（ポータブル）。
- MSIX の `Identity`（`pryzo.KeyAutomator`）と `Publisher` は旧版と同じなので、Store 上は同じアプリの更新として出せます（バージョンは 3.0.0.0）。
- `KeyAutomator.exe` の AppExecutionAlias は `build/appx-extensions.xml` で引き継いでいます。

## 検証状況

| 項目 | 状況 |
|---|---|
| 型検査・単体テスト（ロジック／保存／CLI 解釈／エディタ操作／Win32 構造体レイアウト） | Linux で実行済み |
| renderer の操作（保存・未保存確認・繰り返し・テスト実行の UI・言語切替…） | Chromium で実行済み |
| 実 Electron の起動（スプラッシュ→メイン、IPC、ファイル保存、CLI の終了コード、配布レイアウト） | Linux（xvfb）で実行済み |
| **`SendInput` による実際のキー送信** | **Windows で未確認** |
| **MSIX のビルド・インストール・AppExecutionAlias・Store 提出** | **Windows で未確認**（CI の `Electron CI / windows` で生成まで確認） |
| `-h` を親コンソールへ出す（AttachConsole） | Windows で未確認 |

## WinUI 版からの切り替え手順（残り）

1. Windows で `npm run dev` → マクロの「テスト実行」でメモ帳へ入力できること、`dialog` が最前面に出ること。
2. CI の `Electron CI` が通り、artifact の MSIX を入れて `KeyAutomator.exe -h` / `-alias …` が動くこと。
3. `release.yml` / `msix-sideload.yml` / Store 提出スクリプトを Electron 版に差し替える（`.appx` を `msstore` に渡す）。
4. WinUI 版（ルートの `*.cs` / `*.xaml` / `KeyAutomator.csproj` / `KeyAutomator.Tests` / `Strings/` / `Package.appxmanifest`）を削除し、`app/` をリポジトリ直下へ移す。

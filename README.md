# KeyAutomator

キー入力シーケンスを登録し、指定秒数待機後にアクティブウィンドウへ自動入力する Windows デスクトップアプリです。

**バージョン:** 2.4.1  
**UI:** WinUI 3（Fluent / Mica）  
**言語:** C# / .NET 8

## できること

- マクロの登録・編集・削除・複製（GUI）
- テキスト / 特殊キー / ショートカット / マウスクリック / ウェイトの組み合わせ実行
- コマンドラインからサイレント実行（画面非表示）

## 動作環境

- Windows 10 バージョン 1809 以降 / Windows 11（64bit 推奨）
- **単一 exe** に .NET ランタイムと Windows App SDK を同梱（追加インストール不要）

## 使い方（利用者向け）

### GUI

1. `KeyAutomator.exe` を起動
2. 左の一覧でマクロを選ぶか、[新規]
3. 右側で名前・引数名・起動前ウェイト・アクションを編集
4. [保存]
5. [テスト実行] でウェイト中に入力先へフォーカスを移して確認

### CLI

| コマンド | 意味 |
|---|---|
| `KeyAutomator.exe -1` | ID=1 を実行 |
| `KeyAutomator.exe - 1` | 同上（`-` のあとに空白可） |
| `KeyAutomator.exe -id 1` | 同上 |
| `KeyAutomator.exe -alias login_ok` | 引数名（alias）で実行 |
| `KeyAutomator.exe -login_ok` | 同上（短縮） |
| `KeyAutomator.exe - login_ok` | 同上（`-` のあとに空白可） |
| `KeyAutomator.exe login_ok` | 同上（素の引数） |
| `KeyAutomator.exe -name "全選択＆コピー"` | 表示名で実行 |

引数名（alias）は英数字と `_` のみ。表示名とは別です。

成功時 Exit Code `0` / 失敗時 `1`（同階層 `error.log`）

### 設定

実行ファイルと同じフォルダの `config.json` を使用します。サンプルは `config.sample.json`。

アプリ設定は同フォルダの `settings.json` です。

| 項目 | 意味 | 既定 |
|---|---|---|
| `confirm_before_delete` | マクロ／手順削除前の確認ダイアログ | `true` |

画面右下の「削除前に確認」トグルでも変更できます。手順は **Delete キー**でも削除できます（テキスト入力中は除く）。

ショートカットは同時押しキーをプルダウンで何個でも追加できます（例: Ctrl + Shift + S）。  
マウスクリック（左／右／中／左ダブル）は **現在のカーソル位置** に対して実行されます。  
マクロ一覧・実行手順は **ドラッグ＆ドロップで並べ替え** できます。

## 配布物の入手

**単一の `KeyAutomator.exe` だけで動作します。**  
（初回起動時、内部リソースを一時フォルダへ展開します。数秒かかることがあります。）

### 会社などへ持っていく（推奨）

ビルド済み zip:

`dist\KeyAutomator-v2.4.1-win-x64-single.zip`

1. zip を USB / クラウド等でコピー
2. 解凍し、`KeyAutomator.exe` を任意のフォルダへ置く
3. 起動する（`config.json` / `settings.json` は exe と同じ場所に自動作成）

中に `使い方.txt` と `config.sample.json` があります。

### 開発者向けに自分で作る

1. [README_DEVELOPER.md](README_DEVELOPER.md) の単一 exe 発行手順を実行
2. 出力の `KeyAutomator.exe` だけを配布

## 注意

- 入力は「その時点のアクティブウィンドウ」へ送られます
- 管理者権限が必要なアプリへ送る場合は、本アプリも管理者起動してください
- パスワード等を `config.json` に平文保存する場合は取り扱いに注意してください

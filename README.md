# KeyAutomator

キー入力シーケンスを登録し、指定秒数待機後にアクティブウィンドウへ自動入力する Windows デスクトップアプリです。

**バージョン:** 2.7.2  
**UI:** WinUI 3（Fluent / Mica）  
**言語:** C# / .NET 8  
**ライセンス:** [MIT](LICENSE)

## ダウンロード（推奨）

ビルド済みの単一 exe は **GitHub Releases** から入手できます。

→ [最新リリース](https://github.com/Nakashun-mf/KeyAutomator/releases/latest)

1. `KeyAutomator-v*-win-x64-single.zip` をダウンロード
2. 解凍し、`KeyAutomator.exe` を任意のフォルダへ置く
3. 起動する（`config.json` / `settings.json` は exe と同じ場所に自動作成）

追加のランタイムインストールは不要です。初回起動時のみ、内部リソース展開で数秒かかることがあります。

## できること

- マクロの登録・編集・削除・複製（GUI）
- テキスト / 特殊キー / ショートカット / マウスクリック / ウェイト / 確認ダイアログの組み合わせ実行
- コマンドラインからサイレント実行（画面非表示）

## 動作環境

- Windows 10 バージョン 1809 以降 / Windows 11（64bit 推奨）
- **単一 exe** に .NET ランタイムと Windows App SDK を同梱

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
| `action_delay_sec` | 各手順のあいだに自動で挟む待機（秒） | `0.2` |

画面右下の「手順間隔」「削除前に確認」でも変更できます。手順間隔を `0` にすると従来どおり連続実行します。手順は **Delete キー**でも削除できます（テキスト入力中は除く）。

ショートカットは同時押しキーをプルダウンで何個でも追加できます（例: Ctrl + Shift + S）。  
マウスクリック（左／右／中／左ダブル）は **現在のカーソル位置** に対して実行されます。  
確認ダイアログはメッセージを表示し、ユーザーが **OK** を押すまで次の手順へ進みません（CLI 実行時も同様に表示されます）。  
マクロ一覧・実行手順は **Ctrl / Shift で複数選択**、**ドラッグ＆ドロップで並べ替え** できます。  
削除は複数選択に対応しています。未保存のまま別マクロへ切り替えると確認が出ます。  
テスト実行中は「中断」で停止できます。CLI は `-h` / `--help` で使い方を表示します。

## 開発者向け

ソースから自分で発行する場合は [README_DEVELOPER.md](README_DEVELOPER.md) を参照してください。

```powershell
git clone https://github.com/Nakashun-mf/KeyAutomator.git
cd KeyAutomator
dotnet publish -c Release -p:Platform=x64 -r win-x64 --self-contained true -o .\publish-sf
```

## 注意

- 入力は「その時点のアクティブウィンドウ」へ送られます
- 管理者権限が必要なアプリへ送る場合は、本アプリも管理者起動してください
- パスワード等を `config.json` に平文保存する場合は取り扱いに注意してください
- 本ソフトウェアは現状有姿（AS IS）で提供され、利用は自己責任です

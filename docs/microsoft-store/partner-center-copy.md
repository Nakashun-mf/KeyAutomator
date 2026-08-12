# Partner Center 記入用テキスト（KeyAutomator）

Partner Center の各欄に **そのまま貼れる下書き**です。公開者名・サポート URL は自分の環境に合わせて置き換えてください。

プレースホルダ:

- `{SUPPORT_URL}` … 例: `https://github.com/Nakashun-mf/KeyAutomator/issues`
- `{PRIVACY_URL}` … 例: `https://github.com/Nakashun-mf/KeyAutomator/blob/main/PRIVACY.md`
- `{PUBLISHER_DISPLAY_NAME}` … Partner Center の公開者表示名

---

## 製品情報

| 項目 | 推奨値 |
|---|---|
| 製品名 | KeyAutomator |
| カテゴリ | 生産性（Productivity）※取れない場合は「開発者ツール」 |
| 価格 | 無料 |
| 無料試用 | なし（無料のため） |
| 対象デバイス | PC（デスクトップ） |
| 対応 OS | Windows 10 バージョン 1809 以降 / Windows 11 |

---

## 短い説明（Short description）

**日本語（1000 文字以内・短め推奨）:**

```text
キー入力マクロを登録し、指定秒数後にアクティブウィンドウへ自動入力する Windows デスクトップアプリです。
```

**English:**

```text
Register keyboard macros and automatically type them into the active window after a delay. A Windows desktop productivity tool.
```

---

## 説明（Description）

**日本語:**

```text
KeyAutomator は、定型のキー入力やショートカット、マウスクリックをマクロとして登録し、起動前ウェイトのあと「その時点で前面にあるウィンドウ」へ自動入力する Windows 用デスクトップアプリです。

【主な機能】
・マクロの登録・編集・削除・複製（GUI）
・テキスト／特殊キー／ショートカット／マウスクリック／ウェイト／確認ダイアログ
・手順の繰り返し（ネスト可）
・コマンドラインからの実行（管理画面を出さずにマクロ実行）

【向いている使い方】
・同じフォーム入力や検証手順の繰り返し
・デモやサポート作業の定型操作
・自分用の作業自動化（業務アプリへの定型入力など）

【データの扱い】
・インターネットへ個人情報やマクロ内容を送信しません
・設定は PC 上のローカルファイルのみ（詳細はプライバシーポリシー）

【注意】
・入力はアクティブウィンドウへ送られます。ウェイト中に入力先をクリックして前面にしてください
・パスワード等をマクロに含めると、設定ファイルに平文で保存されます
・管理者権限が必要なアプリへ送る場合は、本アプリも管理者として起動してください

サポート: {SUPPORT_URL}
プライバシー: {PRIVACY_URL}
```

**English:**

```text
KeyAutomator lets you register keyboard and mouse macros and send them to the active window after a configurable delay.

Features:
• Create, edit, duplicate, and delete macros in a WinUI 3 GUI
• Text, special keys, shortcuts, mouse clicks, waits, and confirmation dialogs
• Nested repeats
• Command-line execution without opening the management UI

Privacy:
• No network upload of macros or personal data
• Settings stay on this PC (see the privacy policy)

Notes:
• Input goes to the foreground window—bring the target window to the front during the delay
• Secrets stored in macros are saved in plain text in the local config file
• To send keys to an elevated app, run KeyAutomator elevated as well

Support: {SUPPORT_URL}
Privacy: {PRIVACY_URL}
```

---

## プライバシーポリシー URL

```text
{PRIVACY_URL}
```

必須。公開ページが 404 だと認定で落ちることがあります。提出前にブラウザで開けて確認してください。

---

## サポート／連絡先

| 項目 | 例 |
|---|---|
| サポート Web | `{SUPPORT_URL}` |
| サポートメール | （任意。持っていれば Partner Center に登録） |

---

## システム要件（記載例）

```text
・Windows 10 バージョン 1809 以降、または Windows 11
・64bit（x64）推奨
・キーボード／マウス
・追加ランタイムの手動インストールは不要（MSIX パッケージに Windows App SDK 同梱）
```

---

## 認定用メモ（Notes for certification）

テスター向け。英語でも問題ありません（併記推奨）。

```text
KeyAutomator is a local productivity macro tool for Windows (WinUI 3 / Windows App SDK).

What it does:
- Lets the user create macros (text, keys, shortcuts, mouse clicks, waits, dialogs).
- After a delay, it sends input to the CURRENT FOREGROUND window using Win32 SendInput.
- It does NOT capture or log keystrokes (not a keylogger).
- It does NOT send data over the network.

How to test:
1. Launch the app from Start.
2. On first run, sample macros may be created.
3. Select a sample macro (or create a simple one with type=text).
4. Click "テスト実行" / test run, then within the delay click Notepad (or any editor) to focus it.
5. Confirm characters appear in the focused window.
6. Optional CLI (after install): KeyAutomator.exe -h

Restricted capability:
- runFullTrust is required for WinUI 3 desktop + SendInput automation. See the justification in the submission options.

Privacy policy: {PRIVACY_URL}
Source / issues: {SUPPORT_URL}
Publisher: {PUBLISHER_DISPLAY_NAME}
```

---

## 年齢区分（IARC）回答の目安

アンケートは Partner Center の設問に従ってください。一般的な目安:

| 観点 | 目安の答え |
|---|---|
| ユーザー生成コンテンツの共有・SNS | なし（ローカルのみ） |
| オンライン交流 | なし |
| 暴力・性的描写 | なし |
| 課金・ギャンブル | なし（無料・アプリ内課金なし） |
| 個人情報の収集 | 収集して外部送信しない（ローカル設定のみ） |

結果として低い年齢区分（例: 3+）になる想定ですが、**設問文言に忠実に回答**してください。

---

## 提出オプション: 制限付き機能

`runFullTrust` の説明文は [runFullTrust.md](runFullTrust.md) を使用。

---

## スクリーンショットのキャプション例

1. `マクロ一覧と編集画面`
2. `手順の追加（テキスト／キー／ショートカット）`
3. `テスト実行前のウェイト設定`
4. `CLI ヘルプ（KeyAutomator.exe -h）` ※任意

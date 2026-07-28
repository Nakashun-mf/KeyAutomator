# キー入力自動化アプリケーション (VB.NET) 基本設計仕様書

## 1. アプリケーション概要

本アプリケーションは、事前登録された一連のキー入力シーケンス（テキスト送信、特殊キー押下、ショートカットキー操作、ステップ間ウェイト）を、指定秒数待機後に「アクティブウィンドウ」へ自動入力するWindowsデスクトップツールである。

### 1.1 主な特徴
- **2種類の動作モード**: GUIによる登録・管理モードと、コマンドライン引数による自動実行モード。
- **高信頼なショートカット処理**: Win32 API (`SendInput`) による確実な修飾キー（Ctrl, Alt, Shift, Win）と組み合わせキー送信。
- **ポータビリティ**: 外部依存を極力排除し、設定ファイル (`config.json`) と同一ディレクトリで可搬動作。

---

## 2. 開発環境・動作環境

| 項目 | 仕様 |
| :--- | :--- |
| **開発言語** | C#（WinUI 3）※旧案: Visual Basic .NET |
| **ターゲットフレームワーク** | .NET 8.0 Windows（Windows App SDK） |
| **UIフレームワーク** | WinUI 3（Fluent Design / Mica） |
| **対応OS** | Windows 10 (1809+) / 11 (64bit 推奨) |
| **外部ライブラリ** | `System.Text.Json`, CommunityToolkit.Mvvm |
| **キー送信方式** | Win32 API `SendInput` |

---

## 3. 動作モードと起動仕様

アプリケーション起動時のコマンドライン引数によって、処理フローを分岐する。

```
[起動コマンド]
  ├── 引数なし                 => GUIモード起動 (管理画面表示)
  └── 引数あり (例: -1)         => CLIサイレント実行モード起動 (バックグラウンド処理)
```

### 3.1 GUIモード（引数なし）
- コマンド: `KeyAutomator.exe`
- 登録データの新規作成、編集、削除、一覧確認を行うフォーム（GUI）を表示。

### 3.2 CLIサイレント実行モード（引数あり）
- コマンドパターン:
  - `KeyAutomator.exe -<ID>` （例: `KeyAutomator.exe -1`）
  - `KeyAutomator.exe -id <ID>` （例: `KeyAutomator.exe -id 1`）
  - `KeyAutomator.exe -name "<登録名>"` （例: `KeyAutomator.exe -name "ログイン処理"`）
- 動作手順:
  1. 画面を表示せずにバックグラウンドで起動。
  2. 指定されたIDまたは名称に対応するマクロ設定を `config.json` から読み込み。
  3. 設定された「起動前ウェイト時間（秒）」だけ待機 (`Thread.Sleep`)。
  4. 待機終了時のアクティブウィンドウに対して、登録されたアクションリストを順番に実行。
  5. 全アクション完了後、即座にアプリケーションを正常終了（Exit Code: 0）。
- エラーハンドリング:
  - 該当するID/名称が存在しない場合、ログ（オプション）を残して即座に終了（Exit Code: 1）。UIダイアログなどのポップアップは出力しない。

---

## 4. データ構造仕様 (`config.json`)

設定データは実行ファイルと同階層の `config.json` に保存する。

### 4.1 JSONスキーマ・サンプル

```json
[
  {
    "id": 1,
    "name": "ログイン&定型データ入力",
    "delay_sec": 3.0,
    "actions": [
      { "type": "text", "value": "user_admin" },
      { "type": "key", "value": "TAB" },
      { "type": "text", "value": "p@ssword123" },
      { "type": "key", "value": "ENTER" },
      { "type": "wait", "value": "1.0" },
      { "type": "hotkey", "value": "CTRL+S" }
    ]
  },
  {
    "id": 2,
    "name": "全選択＆コピー",
    "delay_sec": 2.0,
    "actions": [
      { "type": "hotkey", "value": "CTRL+A" },
      { "type": "hotkey", "value": "CTRL+C" }
    ]
  }
]
```

### 4.2 アクション種別 (`type`) 定義

| `type` | 概要 | `value` のフォーマット例 | 説明 |
| :--- | :--- | :--- | :--- |
| `text` | テキスト文字列送信 | `"Hello World"` | 指定した文字列をそのままキー打鍵として送信 |
| `key` | 特殊キー単体打鍵 | `"ENTER"`, `"TAB"`, `"ESC"`, `"BACKSPACE"`, `"UP"`, `"DOWN"` | 定義された単一の機能キーを押下 |
| `hotkey` | ショートカットキー | `"CTRL+V"`, `"ALT+TAB"`, `"CTRL+SHIFT+S"` | 修飾キーを押しながら文字/機能キーを押下 |
| `wait` | ステップ間待機 | `"0.5"` | アプリのレスポンス待ち用（秒指定、小数可） |

---

## 5. 画面（GUI）設計仕様

WinFormsによるメインウィンドウのレイアウトおよび操作仕様。

### 5.1 画面レイアウト構成
- **左側パネル**: マクロ一覧リスト (`ListBox` または `DataGridView`)
  - 項目: ID, マクロ名, 起動前ウェイト(秒)
  - 下部に [新規作成] [複製] [削除] ボタン
- **右側パネル**: マクロ詳細編集領域
  - **基本情報**:
    - ID (自動付与 / 数値入力)
    - マクロ名 (テキストボックス)
    - 起動前ウェイト時間(秒) (数値入力ボックス: `NumericUpDown`)
  - **アクションリスト (`DataGridView`)**:
    - 列: 順序 (#), 種別 (Text/Key/Hotkey/Wait), 設定値 (Value)
    - 操作ボタン群:
      - [＋テキスト追加]
      - [＋特殊キー追加]
      - [＋ショートカット追加]
      - [＋ウェイト追加]
      - [▲ 上へ] [▼ 下へ] [削除]
  - **最下部**: [保存] [キャンセル] [テスト実行] ボタン

### 5.2 フォーム入力補助機能
- **ショートカット入力モーダル**:
  - キーボードのキーを押すと自動的に `CTRL + SHIFT + A` 等の組み合わせを検知してテキストボックスへ反映する記録キャプチャ機能。

---

## 6. キー入力送信ロジック（VB.NET実装設計）

標準の `My.Computer.Keyboard.SendKeys` は修飾キーの同時押し動作で不具合が生じやすいため、Win32 API `SendInput` 構造体をP/Invoke呼出しして実装する。

### 6.1Win32 API 定義概要 (VB.NET)

```vb
Imports System.Runtime.InteropServices

Public Class NativeMethods
    <StructLayout(LayoutKind.Sequential)>
    Public Structure INPUT
        Public type As UInteger
        Public U As InputUnion
    End Structure

    <StructLayout(LayoutKind.Explicit)>
    Public Structure InputUnion
        <FieldOffset(0)> Public mi As MOUSEINPUT
        <FieldOffset(0)> Public ki As KEYBDINPUT
        <FieldOffset(0)> Public hi As HARDWAREINPUT
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Public Structure KEYBDINPUT
        Public wVk As UShort
        Public wScan As UShort
        Public dwFlags As UInteger
        Public time As UInteger
        Public dwExtraInfo As IntPtr
    End Structure

    Public CONST INPUT_KEYBOARD As UInteger = 1
    Public CONST KEYEVENTF_KEYDOWN As UInteger = &H0
    Public CONST KEYEVENTF_KEYUP As UInteger = &H2
    Public CONST KEYEVENTF_UNICODE As UInteger = &H4

    <DllImport("user32.dll", SetLastError:=True)>
    Public Shared Function SendInput(nInputs As UInteger, pInputs() As INPUT, cbSize As Integer) As UInteger
    End Function
End Class
```

### 6.2 アクション処理アルゴリズム

1. **`text` 処理**:
   - 文字列を一文字ずつループ処理。
   - `KEYEVENTF_UNICODE` フラグを使用して Unicode 文字コードを直接 `SendInput` へ送信（クリップボードを経由しないため安全）。
2. **`key` 処理**:
   - キー名（`ENTER`, `TAB` 等）を Virtual Key Code (VK) へ変換。
   - `KEYDOWN` -> 短時間ウェイト -> `KEYUP` を送信。
3. **`hotkey` 処理**:
   - 例: `CTRL+SHIFT+S`
   - 修飾キー (`VK_CONTROL`, `VK_SHIFT`) の `KEYDOWN` を順番に送信。
   - メインキー (`VK_S`) の `KEYDOWN` / `KEYUP` を送信。
   - 修飾キーの `KEYUP` を逆順で送信。
4. **`wait` 処理**:
   - `Thread.Sleep(Convert.ToInt32(val * 1000))`

---

## 7. 例外処理・ログ仕様

1. **JSONパースエラー / ファイル非存在**:
   - GUIモード: エラーメッセージを表示し、空の設定ファイルを作成。
   - CLIモード: 何も表示せず終了コード `1` で終了。
2. **実行中の例外**:
   - キー送信中の予期せぬエラーはキャッチし、同階層の `error.log` にタイムスタンプ付きで記録。

---

## 8. テスト計画・確認項目

| テスト項目 | 確認内容 | パス条件 |
| :--- | :--- | :--- |
| **CLI起動パターン1** | `APP.exe -1` でID:1が起動するか | 指定秒数待機後、アクティブ画面に入力されること |
| **CLI起動パターン2** | `APP.exe -name "ログイン"` で名前検索起動できるか | 対象設定が正しく実行されること |
| **修飾キー同時押し** | `Ctrl+A` -> `Ctrl+C` などの連打動作 | 直前キーの押しっぱなし事故が発生しないこと |
| **日本語/特殊文字** | テキストで記号や全角文字が送信できるか | 途切れや文字化けなく正常に入力されること |
| **フォーカス未選択** | 待機秒数内にアクティブウィンドウが存在しない場合 | エラーで落ちずに実行終了すること |

---
*初版作成日: 2026年7月28日*

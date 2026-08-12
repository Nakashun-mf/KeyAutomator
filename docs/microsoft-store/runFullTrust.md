# runFullTrust 正当化文（Partner Center）

WinUI 3 デスクトップアプリはパッケージ化するとマニフェストに制限付き機能 `runFullTrust` が必要です。  
Partner Center の **Submission options / 制限付き機能の追加情報** に、次を貼ってください。

---

## 日本語（短め）

```text
本アプリは WinUI 3（Windows App SDK）のデスクトップアプリです。UWP サンドボックスではなくフルトラストのデスクトップ実行モデルで動作するため、パッケージ化時に runFullTrust が必要です。

用途はローカル PC 上の生産性マクロです。Win32 SendInput により、ユーザーが開始したマクロのキー／マウス入力を「その時点のアクティブウィンドウ」へ送信します。キー入力の傍受・記録（キーログ）は行いません。インターネットへのデータ送信やバックグラウンドでの監視も行いません。

宣言している Capability は runFullTrust のみで、未使用の権限は追加していません。
```

---

## English（推奨・認定向け）

```text
This is a WinUI 3 (Windows App SDK) desktop application. Packaged WinUI 3 apps use the full-trust desktop model (not the UWP sandbox), so the runFullTrust restricted capability is required.

The app is a local productivity macro tool. It uses Win32 SendInput to send user-defined key and mouse input to the current foreground window after an explicit user-started run (GUI test run or CLI). It does not capture or log keystrokes, does not monitor input in the background, and does not transmit macros or personal data over the network.

The package declares only runFullTrust; no unused capabilities are requested.
```

---

## 補足（聞かれたとき用）

| 質問 | 答え |
|---|---|
| なぜフルトラストか | WinUI 3 デスクトップ + `SendInput` による入力自動化のため |
| 他に Capability は？ | なし（`runFullTrust` のみ） |
| 管理者必須か | 通常は不要。管理者アプリへ送るときだけ昇格起動が必要 |
| 企業向け MDM か | 一般公開の生産性ツール（個人利用想定） |

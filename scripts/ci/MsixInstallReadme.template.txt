KeyAutomator MSIX（サイドロード）v__VERSION__
=====================================

【推奨】まずは単一 exe（*-win-x64-single.zip）で問題ない場合はそちらを使ってください。
これは PC にインストールする形（MSIX）の配布物です。

【インストール手順】
1. Windows の「開発者向け」で Developer Mode をオン（またはサイドローディングを許可）
2. 同梱の KeyAutomator-v__VERSION__-signing.cer をダブルクリック → 「証明書のインストール」
   - 現在のユーザー or ローカルコンピューター
   - 「信頼された人」ストアへ配置（見つからなければ「信頼されたルート」でも可）
3. PowerShell で:
   Add-AppxPackage -Path .\KeyAutomator-v__VERSION__-win-x64.msix
4. スタートメニューから KeyAutomator を起動

【アンインストール】
Get-AppxPackage *KeyAutomator* | Remove-AppxPackage

【注意】
- 署名はリリースごとに作る自己署名証明書です（同梱の .cer を信頼してください）
- 設定は %LocalAppData%\KeyAutomator に保存されます

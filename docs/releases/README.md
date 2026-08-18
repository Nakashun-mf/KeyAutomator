# GitHub Release の出し方

タグ `v*` の Release を **公開**すると、`.github/workflows/release.yml` が Windows 上で単一 exe zip と MSIX zip をビルドして Assets に添付します。

## v2.8.6 を出す手順（main マージ後）

英語 UI の変更は feature ブランチ上にあります。**タグは main に付けてください。**

1. PR を `main` にマージする
2. Windows または権限のある環境で:

```powershell
git checkout main
git pull origin main
git tag v2.8.6
git push origin v2.8.6
gh release create v2.8.6 `
  --title "KeyAutomator v2.8.6" `
  --notes-file .\docs\releases\v2.8.6.md
```

3. Actions の **Release** ワークフローが zip をアップロードし、Store 用 `.msixupload` も提出するまで待つ
4. https://github.com/Nakashun-mf/KeyAutomator/releases/latest で Assets を確認する
5. Partner Center で提出が認定キューに入ったかを確認する（下書き提出が残っていると失敗する）

## バージョンを上げるとき

1. `KeyAutomator.csproj` の Version 系と `Package.appxmanifest` の Identity Version
2. `README.md` / `README_DEVELOPER.md` の表記
3. 本フォルダに `vX.Y.Z.md` を追加（ユニットテストが csproj の Version と一致することを確認する）

# Microsoft Store 公開ガイド（KeyAutomator）

このフォルダは **Microsoft Store へ公開するための準備・申請サポート** です。  
コード変更だけでは Store に並びません。**Partner Center 上の操作（あなた自身のアカウント）** が必須です。

| 文書 | 内容 |
|---|---|
| [README.md](README.md)（本ファイル） | 方針・ギャップ・手順チェックリスト |
| [partner-center-copy.md](partner-center-copy.md) | Partner Center に貼る説明文・年齢区分・認定ノート |
| [runFullTrust.md](runFullTrust.md) | 制限付き機能 `runFullTrust` の正当化文 |
| [product-identity.md](product-identity.md) | Partner Center の Identity / Store ID（転記済み） |
| [listing-assets/README.md](listing-assets/README.md) | スクリーンショット等の素材仕様 |

公式ドキュメント:

- [初めてのアプリを公開する](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/publish-first-app)
- [提出の作成（MSIX）](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/create-app-submission)
- [パッケージ要件（MSIX）](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)
- [Microsoft Store ポリシー](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies)

---

## 推奨ルート: MSIX（現行プロジェクトの延長）

KeyAutomator は既に MSIX サイドロード経路があります。Store 公開は **同じ MSIX を StoreUpload モードで出す**のが最短です。

| 比較 | MSIX（推奨） | EXE/MSI（非推奨・現状） |
|---|---|---|
| 既存資産 | `Package.appxmanifest` / CI あり | 単一 exe zip のみ（インストーラ無し） |
| 署名 | 認定後に Microsoft が再署名 | **商用コード署名証明書 + 信頼された CA** が必須 |
| 配布形態 | Partner Center へ `.msixupload` をアップロード | CDN 上の **固定 HTTPS URL** + **サイレントインストール** |
| `runFullTrust` | 正当化の記入が必要（WinUI 3 では一般的） | 不要だが、インストーラ要件が重い |

現状のポータブル zip（単一 exe）だけでは EXE/MSI ルートの要件（サイレントインストーラ・Authenticode・CDN）を満たせません。

---

## 現状ギャップ（対応状況）

| 項目 | 現状 | 必要な対応 | 誰がやるか |
|---|---|---|---|
| 開発者アカウント | 未確認 | [Partner Center](https://partner.microsoft.com/dashboard) 登録（個人/企業） | **あなた** |
| アプリ名予約 | 未実施 | 「KeyAutomator」等を予約 | **あなた** |
| Identity / Publisher | Partner Center 値を転記済み（`pryzo.KeyAutomator`） | 変更時は [product-identity.md](product-identity.md) とマニフェストを同期 | 共同 |
| Store 用ビルド | `Build-MsixStore.ps1` / サイドロード Release MSIX | Windows で **Release** の `.msix` または `.msixupload` | **あなた（Windows）** |
| プライバシーポリシー URL | `PRIVACY.md` / `PRIVACY.en.md` | 言語ごとの **HTTPS 公開 URL** を Partner Center に登録 | **あなた**（下記） |
| スクリーンショット | `listing-assets/screenshot_{ja,en}_*.png`（1920×1080） | 実機キャプチャへ差し替え推奨。Partner Center へアップロード | 共同（素材は本フォルダ） |
| ストア説明文 | 下書きあり | Partner Center に転記 | 共同（文面は本フォルダ） |
| `runFullTrust` 説明 | 下書きあり | Submission options に転記 | 共同 |
| WACK（任意だが推奨） | 未実施 | Windows App Certification Kit | **あなた（Windows）** |
| 年齢区分 | 未実施 | IARC アンケート | 共同（回答例あり） |

### 自動で固定している提出契約（CI）

GitHub Actions の **Unit Tests** と **MSIX Sideload Smoke** がどちらも緑であること。特に:

- マニフェストの Capability は `runFullTrust` のみ（ソースとインストール済みパッケージの両方）
- 本番コードに HTTP / テレメトリ型が無い
- Partner Center Identity と `Package.appxmanifest` が一致する
- サンプルのパスワードらしき文字列はダミーだと分かる

提出前にこの 2 ワークフローが落ちているパッケージは上げない。

---

## GitHub Release からの自動提出

初回 listing が Store で live になったあと、GitHub の **Release 公開**（`release: published`）で `.github/workflows/release.yml` の `publish-store` ジョブが動きます。

1. `scripts/ci/Build-MsixStore.ps1` で Store 用 `.msixupload` を作る（GitHub 添付のサイドロード MSIX は使わない）
2. `microsoft/microsoft-store-apppublisher` と `msstore publish` で Partner Center に提出する（`--noCommit` なし＝認定キューへ送る）

公式手順: [Publish app updates with GitHub Actions](https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/github-actions)

### 必要な GitHub Secrets

リポジトリ **Settings → Secrets and variables → Actions**（値は git に置かない）:

| Secret | 中身 | この画面ではないもの |
|---|---|---|
| `AZURE_AD_TENANT_ID` | Entra の Tenant ID | — |
| `AZURE_AD_APPLICATION_CLIENT_ID` | Entra アプリの Application (client) ID | — |
| `AZURE_AD_APPLICATION_SECRET` | Entra アプリの Client secret | — |
| `SELLER_ID` | Partner Center の **販売者 ID**（数字） | Windows パブリッシャー ID `CN=...` ではない |

Store ID `9P814VCBNVGF` は秘密ではないのでワークフローに直書きしています。

Entra アプリは Partner Center の **User management → Microsoft Entra applications** で **Manager** にしてください。

### 制約

- **無料アプリのみ**（有料は公式 CLI が未対応）
- Partner Center に **下書き提出が残っていると失敗**する。認定待ち・下書きは完了または削除してから Release する
- 提出ごとに `Version` を前回より大きくする
- ジョブ成功＝提出完了であり、Store 反映は **認定後**
- Actions の手動実行（`workflow_dispatch`）では、入力 `publish_store` をオンにしたときだけ Store へ出す（誤提出防止）

> Linux 上の Cloud Agent では WinUI / MSIX の実ビルド・スクリーンショット撮影はできません。Store パッケージ生成と画面キャプチャは **Windows PC** で行ってください。  
> **Visual Studio は必須ではありません。** Partner Center + マニフェスト編集 + `dotnet` / 既存の PowerShell スクリプトで提出できます。

---

## あなたが行う手順（チェックリスト）

### 1. Partner Center アカウント

1. https://partner.microsoft.com/dashboard で開発者登録
2. 個人 or 会社アカウントを選択（料金・本人確認は Microsoft の案内に従う）
3. 公開者の表示名を決める（後で `PublisherDisplayName` と揃える）

### 2. アプリ名の予約

1. **新しい製品** → **MSIX または PWA アプリ**（表記は UI により異なる）
2. 名前候補: `KeyAutomator`（取れない場合は `KeyAutomator Desktop` など）
3. 予約後、左メニュー **製品の管理** → **製品の ID（Product identity）** を開く

### 3. Identity をマニフェストへ反映（完了）

Partner Center の値は `Package.appxmanifest` と [product-identity.md](product-identity.md) に反映済みです。

| 項目 | 値 |
|---|---|
| Name | `pryzo.KeyAutomator` |
| Publisher | `CN=4B1F058B-F39E-44DE-8373-4258152DED0F` |
| PublisherDisplayName | `pryzo` |
| Store ID | `9P814VCBNVGF` |

署名証明書の Subject も **Publisher と同一**にしてください（`New-CiSigningCertificate.ps1` の既定で対応済み）。

### 4. Release パッケージのビルド（VS なし）

**どちらでも Partner Center にアップロードできます。**

#### A. Release の `.msix` をそのまま上げる（シンプル）

Identity 更新後:

```powershell
.\scripts\ci\New-CiSigningCertificate.ps1
$msix = .\scripts\ci\Build-MsixSideload.ps1 -Configuration Release -Platform x64
# できた .msix を Partner Center の「パッケージ」へアップロード
```

自己署名のままで可（認定後に Microsoft が再署名）。  
GitHub Releases に付いている **古い Identity の** サイドロード zip を流用しないこと。

#### B. `.msixupload` を作る（推奨・クラッシュ解析用シンボル付き）

```powershell
.\scripts\ci\New-CiSigningCertificate.ps1
.\scripts\ci\Build-MsixStore.ps1 -Platform x64 -AppxBundlePlatforms "x64"
# AppPackages\Store\*.msixupload をアップロード
```

Visual Studio がある場合のみ、ウィザードでも可（必須ではない）。

### 5. Partner Center 提出の記入

[partner-center-copy.md](partner-center-copy.md) と [runFullTrust.md](runFullTrust.md) を転記。

必須になりやすいもの:

- [ ] 価格（無料推奨）
- [ ] 市場（日本のみ / 全世界など）
- [ ] カテゴリ（例: 生産性 / 開発者ツール）
- [ ] 年齢区分（IARC）
- [ ] プライバシーポリシー URL
- [ ] 説明・短い説明・スクリーンショット
- [ ] パッケージアップロード（Release の `.msix` または `.msixupload`）
- [ ] `runFullTrust` の追加説明
- [ ] 認定用メモ（テスター向け手順）

### 6. プライバシーポリシー URL

Partner Center は **ブラウザで開ける HTTPS URL** を要求します。例:

```text
日本語: https://github.com/Nakashun-mf/KeyAutomator/blob/main/PRIVACY.md
English: https://github.com/Nakashun-mf/KeyAutomator/blob/main/PRIVACY.en.md
```

ストアの言語ごとに URL を分けて登録してください。より体裁を整えたい場合は GitHub Pages や自前サイトへ相当ページを置く。

### 7. 認定・公開

1. **認定用に送信**
2. 通常は数営業日（初回は長めのことも）
3. 不合格時は理由メール／Partner Center のレポートを確認
4. 不合格内容をこのリポジトリの Issue / 本 PR スレッドに貼れば、追記・修正を続けてサポートできます

---

## ポリシー上の注意（このアプリ特有）

KeyAutomator は **キー入力の送信（SendInput）** を行います。

- **キーロガーではありません**（キー取得・記録はしない）
- 送信先は「その時点のアクティブウィンドウ」
- ネットワーク通信・利用状況送信なし（`PRIVACY.md` / `PRIVACY.en.md`）

認定では「自動化＝不正ツール」と誤解されないよう、ストア説明と認定メモで用途を明確にしてください（文面は `partner-center-copy.md` に用意済み）。

---

## バージョン運用

- `KeyAutomator.csproj` の `Version` / `AssemblyVersion` / `FileVersion`
- `Package.appxmanifest` の `Identity/@Version`（4 部。末尾は Store ルール上 `0` 推奨）

Store 更新のたびに **前より大きいバージョン**が必要です。

---

## 申請サポートの進め方

このリポジトリ側で継続サポートできること:

- Partner Center の文言推敲・日本語／英語併記
- 不合格理由への対応案（マニフェスト・説明・権限）
- Store 用ビルド手順の修正
- `PRIVACY.md` / `PRIVACY.en.md` / README の追記

あなた側で必要なこと:

- Partner Center ログインと提出操作
- Windows 上での関連付け・パッケージ生成・（任意）実機スクリーンショットの差し替え
- （任意）サポート用メールアドレスの用意

不合格レポートやスクショを共有してもらえれば、次の修正案を出します。

## 認定で落ちた場合（10.1.1.11 On Device Tiles）

パッケージ内 `Assets/` のタイル／StoreLogo が Visual Studio 既定のプレースホルダ（灰色の X）のままだと不合格になります。  
正式ロゴを `Assets/` に入れたうえで **バージョンを上げて** MSIX を作り直し、Partner Center へ再提出してください。

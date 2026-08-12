# Partner Center 製品 ID（KeyAutomator）

Store 提出時にマニフェストへ反映済みの値です。  
出典: Partner Center → 製品の管理 → 製品の ID

| 項目 | 値 | マニフェスト |
|---|---|---|
| Package/Identity/Name | `pryzo.KeyAutomator` | `<Identity Name>` |
| Package/Identity/Publisher | `CN=4B1F058B-F39E-44DE-8373-4258152DED0F` | `<Identity Publisher>` |
| Package/Properties/PublisherDisplayName | `pryzo` | `<PublisherDisplayName>` |
| Package Family Name (PFN) | `pryzo.KeyAutomator_29frz59n2q2dp` | （マニフェスト外・参照用） |
| Package SID | `S-1-15-2-1333629242-4040570673-554847077-682136689-1049133207-2168619062-3398237399` | （マニフェスト外・WNS 等） |
| Microsoft Store ID | `9P814VCBNVGF` | （マニフェスト外） |

## 署名証明書について

MSIX をローカルでビルドするとき、署名証明書の Subject は **Publisher と完全一致**が必要です。

```powershell
.\scripts\ci\New-CiSigningCertificate.ps1 `
  -Subject "CN=4B1F058B-F39E-44DE-8373-4258152DED0F"
```

（スクリプトの既定 Subject もこの値に合わせ済み。）  
Store 認定後は Microsoft が再署名します。

## 次のステップ

```powershell
.\scripts\ci\New-CiSigningCertificate.ps1
.\scripts\ci\Build-MsixSideload.ps1 -Configuration Release -Platform x64
# できた .msix を Partner Center のパッケージへアップロード
```

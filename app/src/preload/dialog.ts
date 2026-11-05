import { contextBridge, ipcRenderer } from 'electron'

// 確認ダイアログ（data: URL の小さなウィンドウ）専用。OK を押したことだけを伝える。
// sandbox の preload は electron 以外を require できないので、共有チャンクを作らないよう
// チャンネル名はここに直書きする（shared/api.ts の IPC.dialogClose と同じ値。tests/preload.test.ts で検証）。
contextBridge.exposeInMainWorld('dialogApi', {
  close: () => ipcRenderer.send('ka:dialog-close')
})

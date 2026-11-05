import { BrowserWindow, shell } from 'electron'
import { join } from 'node:path'
import { is } from './is'
import { IPC } from '../shared/api'
import { ipcMain } from 'electron'

const COLORS = {
  bg: '#1e1e1e',
  titlebar: '#181818',
  text: '#cccccc'
}

const loadRenderer = (win: BrowserWindow, page: string, query?: Record<string, string>): Promise<void> => {
  const devUrl = process.env['ELECTRON_RENDERER_URL']
  if (is.dev && devUrl) {
    const url = new URL(page, devUrl.endsWith('/') ? devUrl : devUrl + '/')
    for (const [k, v] of Object.entries(query ?? {})) url.searchParams.set(k, v)
    return win.loadURL(url.toString())
  }
  return win.loadFile(join(__dirname, '../renderer', page), { query })
}

/** 外部へ遷移させない（renderer はローカルページ以外を開かない）。 */
const lockNavigation = (win: BrowserWindow): void => {
  win.webContents.on('will-navigate', (e) => e.preventDefault())
  win.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https:\/\//i.test(url)) void shell.openExternal(url)
    return { action: 'deny' }
  })
}

/** 起動直後に最初に出す、小さな枠なしウィンドウ。 */
export const createSplash = async (lang: 'ja' | 'en'): Promise<BrowserWindow> => {
  const win = new BrowserWindow({
    width: 460,
    height: 300,
    frame: false,
    resizable: false,
    maximizable: false,
    minimizable: false,
    fullscreenable: false,
    show: false,
    center: true,
    skipTaskbar: true,
    backgroundColor: COLORS.bg,
    webPreferences: { sandbox: true, contextIsolation: true, nodeIntegration: false, spellcheck: false }
  })
  lockNavigation(win)
  win.once('ready-to-show', () => win.show())
  await loadRenderer(win, 'splash.html', { lang })
  return win
}

/** スプラッシュを滑らかに閉じる（フェードアウト後に破棄）。 */
export const closeSplash = async (win: BrowserWindow | null): Promise<void> => {
  if (!win || win.isDestroyed()) return
  try {
    await win.webContents.executeJavaScript("document.body.classList.add('closing')")
    await new Promise((r) => setTimeout(r, 240))
  } catch {
    // 既に閉じていてもよい
  }
  if (!win.isDestroyed()) win.destroy()
}

export const createMainWindow = async (): Promise<BrowserWindow> => {
  const win = new BrowserWindow({
    width: 1180,
    height: 760,
    minWidth: 920,
    minHeight: 600,
    show: false,
    title: 'KeyAutomator',
    backgroundColor: COLORS.bg,
    titleBarStyle: 'hidden',
    titleBarOverlay: { color: COLORS.titlebar, symbolColor: COLORS.text, height: 36 },
    webPreferences: {
      preload: join(__dirname, '../preload/index.js'),
      sandbox: true,
      contextIsolation: true,
      nodeIntegration: false,
      spellcheck: false
    }
  })
  lockNavigation(win)
  await loadRenderer(win, 'index.html')
  return win
}

const escapeHtml = (s: string): string =>
  s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;')

const dialogHtml = (message: string, okLabel: string): string => `<!doctype html>
<html><head><meta charset="utf-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline'">
<style>
  html,body{margin:0;height:100%;background:#252526;color:#cccccc;font:14px/1.6 "Segoe UI","Yu Gothic UI",system-ui,sans-serif;user-select:none}
  .box{box-sizing:border-box;height:100%;display:flex;flex-direction:column;border:1px solid #5865f2;border-radius:8px;overflow:hidden}
  .head{padding:10px 16px;background:#181818;font-size:12px;letter-spacing:.06em;color:#9d9d9d;-webkit-app-region:drag}
  .msg{flex:1;padding:18px 20px;white-space:pre-wrap;overflow:auto;user-select:text}
  .foot{display:flex;justify-content:flex-end;padding:12px 16px;background:#1f1f1f}
  button{min-width:96px;padding:7px 18px;border:0;border-radius:6px;background:#5865f2;color:#fff;font:inherit;cursor:pointer}
  button:hover{background:#4752c4} button:focus-visible{outline:2px solid #fff;outline-offset:2px}
</style></head>
<body><div class="box"><div class="head">KeyAutomator</div>
<div class="msg">${escapeHtml(message)}</div>
<div class="foot"><button id="ok" autofocus>${escapeHtml(okLabel)}</button></div></div>
<script>
  const ok = document.getElementById('ok');
  ok.addEventListener('click', () => window.dialogApi.close());
  window.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === 'Escape') window.dialogApi.close(); });
  ok.focus();
</script></body></html>`

/**
 * マクロ実行中の確認ダイアログ。OK（または閉じる）まで解決しない。
 * 他アプリの裏に回らないよう最前面に出す。signal が中断されたら閉じて解決する。
 */
export const showTopmostDialog = (message: string, okLabel: string, signal?: AbortSignal): Promise<void> =>
  new Promise<void>((resolve) => {
    const win = new BrowserWindow({
      width: 440,
      height: 220,
      frame: false,
      resizable: false,
      maximizable: false,
      minimizable: false,
      fullscreenable: false,
      show: false,
      center: true,
      alwaysOnTop: true,
      skipTaskbar: false,
      backgroundColor: '#252526',
      title: 'KeyAutomator',
      webPreferences: {
        preload: join(__dirname, '../preload/dialog.js'),
        sandbox: true,
        contextIsolation: true,
        nodeIntegration: false
      }
    })
    let done = false
    const finish = (): void => {
      if (done) return
      done = true
      ipcMain.removeListener(IPC.dialogClose, onClose)
      signal?.removeEventListener('abort', finish)
      if (!win.isDestroyed()) win.destroy()
      resolve()
    }
    const onClose = (e: Electron.IpcMainEvent): void => {
      if (e.sender === win.webContents) finish()
    }
    ipcMain.on(IPC.dialogClose, onClose)
    signal?.addEventListener('abort', finish)
    win.on('closed', finish)
    lockNavigation(win)
    win.once('ready-to-show', () => {
      win.setAlwaysOnTop(true, 'screen-saver')
      win.show()
      win.focus()
    })
    void win.loadURL('data:text/html;charset=utf-8,' + encodeURIComponent(dialogHtml(message, okLabel)))
  })

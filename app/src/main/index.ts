import { app, BrowserWindow, Menu, session } from 'electron'
import { join } from 'node:path'
import { extractUserArgs, isCliMode } from '../core/cli'
import { runCli } from './cli'
import { registerIpc } from './ipc'
import { createServices } from './services'
import { closeSplash, createMainWindow, createSplash } from './windows'

const args = extractUserArgs(process.argv, process.defaultApp === true)
const cli = isCliMode(args)

if (cli) {
  // 画面を出さない実行では GPU もいらない
  app.disableHardwareAcceleration()
  // dialog 手順のウィンドウを閉じても、CLI の実行が終わるまでは終了しない
  app.on('window-all-closed', () => undefined)
} else {
  app.on('window-all-closed', () => app.quit())
}

// Chromium のキャッシュ等を Roaming に作らない（配布版のみ。開発実行は Electron 既定のまま）
if (app.isPackaged && process.env.LOCALAPPDATA) {
  app.setPath('userData', join(process.env.LOCALAPPDATA, 'KeyAutomator', 'electron'))
}
// MSIX は Identity から AUMID が決まる。それ以外（zip 配布）はここで固定する
if (!(process as NodeJS.Process & { windowsStore?: boolean }).windowsStore) {
  app.setAppUserModelId('pryzo.KeyAutomator')
}

const MIN_SPLASH_MS = 500
const READY_TIMEOUT_MS = 10_000

const startGui = async (): Promise<void> => {
  const services = createServices()
  Menu.setApplicationMenu(null)
  // この画面はカメラ・位置情報などを一切使わない
  session.defaultSession.setPermissionRequestHandler((_wc, _permission, callback) => callback(false))

  const lang = services.t.language === 'ja-JP' ? 'ja' : 'en'
  const splashShownAt = Date.now()
  const splash = await createSplash(lang)

  let main: BrowserWindow | null = null
  let shown = false
  let readyEarly = false
  const reveal = async (): Promise<void> => {
    if (!main) {
      // renderer の通知が createMainWindow の完了より先に届いた場合
      readyEarly = true
      return
    }
    if (shown || main.isDestroyed()) return
    shown = true
    const wait = Math.max(0, MIN_SPLASH_MS - (Date.now() - splashShownAt))
    await new Promise((r) => setTimeout(r, wait))
    if (main.isDestroyed()) return
    main.show()
    main.focus()
    await closeSplash(splash)
  }

  registerIpc(services, { onReady: () => void reveal(), mainWindow: () => main })

  main = await createMainWindow()
  main.on('closed', () => (main = null))
  if (readyEarly) void reveal()
  // 描画完了の通知が来なくても、いつまでもスプラッシュのままにしない
  setTimeout(() => void reveal(), READY_TIMEOUT_MS)
}

void app.whenReady().then(async () => {
  if (cli) {
    const services = createServices()
    const code = await runCli(args, services)
    app.exit(code)
    return
  }
  await startGui()
})

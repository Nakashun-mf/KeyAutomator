// 実 Electron（main + preload + renderer）を起動して、スプラッシュ→メイン表示・IPC・ファイル保存を通しで確認する。
// 事前に `npm run build` が必要。設定は開発実行用の .devdata に書く（実データは触らない）。
// ここでは「テスト実行」を Windows で押さない（実際のキー入力が飛ぶため）。
const { _electron } = require('playwright')
const fs = require('fs')
const path = require('path')
const APP = path.join(__dirname, '..')
const OUT = path.join(APP, 'out/e2e-shots')
fs.mkdirSync(OUT, { recursive: true })
const electronBin = require('electron')  // electron パッケージは実行ファイルのパスを返す
let failures = 0
const ok = (c, m) => { if (!c) { failures++; console.log('FAIL:', m) } else console.log('ok  :', m) }

;(async () => {
  fs.rmSync(path.join(APP, '.devdata'), { recursive: true, force: true })
  const t0 = Date.now()
  const app = await _electron.launch({
    executablePath: electronBin,
    args: [APP, ...(process.platform === 'linux' ? ['--no-sandbox', '--disable-gpu'] : [])],
    env: { ...process.env, ELECTRON_DISABLE_SECURITY_WARNINGS: '1' }
  })
  const seen = []
  app.on('window', (w) => seen.push(w))
  // splash first
  let splash
  for (let i = 0; i < 100 && !splash; i++) {
    splash = app.windows().find((w) => w.url().includes('splash.html'))
    if (!splash) await new Promise((r) => setTimeout(r, 50))
  }
  ok(!!splash, `splash window appears (${Date.now() - t0} ms after launch)`)
  if (splash) {
    await new Promise((r) => setTimeout(r, 200))
    await splash.screenshot({ path: path.join(OUT, '10-electron-splash.png') })
    ok((await splash.textContent('#status')).length > 0, 'splash text localized')
  }

  // main window
  let main
  for (let i = 0; i < 200 && !main; i++) {
    main = app.windows().find((w) => w.url().includes('index.html'))
    if (!main) await new Promise((r) => setTimeout(r, 50))
  }
  ok(!!main, 'main window created')
  await main.waitForSelector('.macro-item', { timeout: 30000 })
  ok((await main.locator('.macro-item').count()) === 3, 'first run seeds 3 sample macros from real config.json')

  const visible = await app.evaluate(({ BrowserWindow }) =>
    BrowserWindow.getAllWindows().filter((w) => w.isVisible()).map((w) => w.getTitle() + '|' + w.getURL().split('/').pop().split('?')[0]))
  console.log('visible windows right after render:', JSON.stringify(visible))

  // wait for splash to close + main visible
  for (let i = 0; i < 60; i++) {
    const v = await app.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows().map((w) => [w.getURL().split('/').pop().split('?')[0], w.isVisible()]))
    if (v.length === 1 && v[0][0] === 'index.html' && v[0][1]) break
    await new Promise((r) => setTimeout(r, 100))
  }
  const after = await app.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows().map((w) => [w.getURL().split('/').pop().split('?')[0], w.isVisible()]))
  ok(JSON.stringify(after) === JSON.stringify([['index.html', true]]), 'splash closed, main visible: ' + JSON.stringify(after))
  console.log(`time to main visible: ${Date.now() - t0} ms (xvfb, software rendering)`)
  await new Promise((r) => setTimeout(r, 400))
  await main.screenshot({ path: path.join(OUT, '11-electron-main.png') })

  // real IPC: create macro -> file on disk
  await main.click('[data-testid=new-macro]')
  await main.waitForFunction(() => document.querySelectorAll('.macro-item').length === 4)
  const cfg = JSON.parse(fs.readFileSync(path.join(APP, '.devdata/config.json'), 'utf8'))
  ok(cfg.length === 4 && cfg[3].id === 4, 'New macro persisted to config.json via IPC')

  // edit + save with Ctrl+S
  await main.fill('[data-testid=name]', 'E2E macro')
  await main.click('button[aria-label="テキスト追加"], button[aria-label="Add text"]')
  await main.keyboard.press('Control+s')
  await main.waitForFunction(() => !document.querySelector('.tab-dirty'))
  const cfg2 = JSON.parse(fs.readFileSync(path.join(APP, '.devdata/config.json'), 'utf8'))
  ok(cfg2[3].name === 'E2E macro' && cfg2[3].actions.length === 1, 'edited macro saved to disk')
  ok(Object.keys(cfg2[3]).sort().join() === 'actions,alias,delay_sec,id,name', 'config.json schema unchanged (snake_case keys)')

  // settings change persists
  await main.click('[data-testid=open-settings]')
  await main.fill('.settings input[type=number]', '0.5')
  await new Promise((r) => setTimeout(r, 300))
  const st = JSON.parse(fs.readFileSync(path.join(APP, '.devdata/settings.json'), 'utf8'))
  ok(st.action_delay_sec === 0.5, 'settings.json updated: ' + JSON.stringify(st))

  // テスト実行は実際に SendInput を打つので、Windows では絶対に押さない。
  // Linux では SendInput が無いので「実行エラーがダイアログで出る（落ちない）」ことだけを確認する。
  if (process.platform !== 'win32') {
    await main.keyboard.press('Escape')
    await main.click('[data-testid=test]')
    await main.click('.dialog-actions button.btn-primary')
    await main.waitForSelector('.dialog-title', { timeout: 15000 })
    ok((await main.textContent('.dialog-title')).length > 0, 'run failure is shown as an error dialog (Linux only)')
  }

  await app.close()
  console.log(failures === 0 ? 'ALL OK' : failures + ' FAILURES')
  process.exit(failures ? 1 : 0)
})().catch((e) => { console.error(e); process.exit(2) })

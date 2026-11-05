// renderer だけを静的サーバで配信し、preload の代わりにメモリ上のモック API を差し込んで操作を検証する。
// 事前に `npm run build` が必要。Chromium が要る（`npx playwright install chromium`）。
const { chromium } = require('playwright')
const http = require('http')
const fs = require('fs')
const path = require('path')

const ROOT = path.join(__dirname, '../out/renderer')
const OUT = path.join(__dirname, '../out/e2e-shots')
fs.mkdirSync(OUT, { recursive: true })
const MIME = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css' }

const server = http.createServer((req, res) => {
  const u = new URL(req.url, 'http://x')
  const f = path.join(ROOT, u.pathname === '/' ? 'index.html' : u.pathname)
  if (!f.startsWith(ROOT) || !fs.existsSync(f)) { res.writeHead(404); return res.end() }
  res.writeHead(200, { 'content-type': MIME[path.extname(f)] ?? 'application/octet-stream' })
  res.end(fs.readFileSync(f))
})

const mockApi = (lang) => `
  const macros = [
    { id: 1, name: 'ログイン&定型データ入力', alias: 'login_ok', delay_sec: 3, actions: [
      { type: 'text', value: 'user_admin' }, { type: 'key', value: 'TAB' },
      { type: 'text', value: 'dummy_secret_do_not_use' }, { type: 'key', value: 'ENTER' },
      { type: 'dialog', value: 'ログイン完了を確認したら OK を押してください' },
      { type: 'wait', value: '1.0' }, { type: 'hotkey', value: 'CTRL+S' },
      { type: 'repeat', value: '3' }, { type: 'key', value: 'DOWN' },
      { type: 'repeat', value: '2' }, { type: 'mouse', value: 'LEFT' }, { type: 'end_repeat', value: '' },
      { type: 'end_repeat', value: '' } ] },
    { id: 2, name: '全選択＆コピー', alias: 'select_copy', delay_sec: 2, actions: [
      { type: 'hotkey', value: 'CTRL+A' }, { type: 'hotkey', value: 'CTRL+C' } ] },
    { id: 3, name: 'Enterを3回', alias: 'enter_x3', delay_sec: 2, actions: [
      { type: 'repeat', value: '3' }, { type: 'key', value: 'ENTER' }, { type: 'end_repeat', value: '' } ] }
  ]
  window.__saved = []
  window.__runs = []
  window.api = {
    getState: async () => ({ macros: JSON.parse(JSON.stringify(macros)), settings: { confirm_before_delete: true, action_delay_sec: 0.2, ui_language: '${lang}' },
      systemLocale: 'ja-JP', dataDir: 'C:\\\\Users\\\\you\\\\AppData\\\\Local\\\\KeyAutomator', configPath: 'C:\\\\Users\\\\you\\\\AppData\\\\Local\\\\KeyAutomator\\\\config.json', usingFallbackDir: true, loadError: null, version: '3.0.0' }),
    saveMacros: async (m) => { window.__saved.push(JSON.parse(JSON.stringify(m))); return { ok: true } },
    loadSamples: async () => ({ ok: true, macros: macros.slice(0,3) }),
    saveSettings: async () => {}, setLanguage: async () => {},
    runMacro: async (m, d) => { window.__runs.push(m); await new Promise(r => setTimeout(r, 1500)); return { status: 'done' } },
    stopRun: async () => {},
    openDataFolder: async () => ({ ok: true, path: 'x' }),
    copyLaunchCommand: async (a, i) => ({ ok: true, text: '"C:\\\\KeyAutomator.exe" -alias ' + a }),
    notifyReady: () => { window.__ready = true }
  }
`


let failures = 0
const ok = (cond, msg) => { if (!cond) { failures++; console.log('FAIL:', msg) } else console.log('ok  :', msg) }

;(async () => {
  await new Promise((r) => server.listen(0, r))
  const base = 'http://localhost:' + server.address().port
  const browser = await chromium.launch()
  const ctx = await browser.newContext({ viewport: { width: 1180, height: 760 }, reducedMotion: 'reduce' })
  const page = await ctx.newPage()
  const errors = []
  page.on('pageerror', (e) => errors.push(e.message))
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()) })
  await page.addInitScript(mockApi('ja-JP'))
  await page.goto(base + '/index.html')
  await page.waitForFunction(() => window.__ready === true)

  const tab = () => page.textContent('.tab')
  const stepCount = () => page.locator('.steps .step').count()
  const status = () => page.textContent('.status-msg')

  // 1 select another macro
  await page.click('.macro-item:nth-child(2)')
  ok((await tab()).includes('全選択'), 'select macro 2 opens it')
  ok((await stepCount()) === 2, 'macro 2 has 2 steps')

  // 2 dirty -> switching asks
  await page.fill('[data-testid=name]', '全選択(改)')
  ok(await page.locator('.tab-dirty').count() === 1, 'dirty dot appears')
  await page.click('.macro-item:nth-child(3)')
  ok(await page.locator('.dialog').count() === 1, 'unsaved dialog opens')
  ok((await page.locator('.dialog-actions button').allTextContents()).length === 3, 'dialog has save/discard/cancel')
  await page.click('.dialog-actions button:has-text("キャンセル")')
  ok((await tab()).includes('全選択(改)'), 'cancel keeps editing macro 2')

  // 3 save with Ctrl+S
  await page.keyboard.press('Control+s')
  await page.waitForFunction(() => window.__saved.length > 0)
  const saved = await page.evaluate(() => window.__saved.at(-1))
  ok(saved[1].name === '全選択(改)', 'Ctrl+S persists renamed macro')
  ok(await page.locator('.tab-dirty').count() === 0, 'dirty dot clears after save')
  ok((await status()).includes('保存しました'), 'status says saved: ' + (await status()))

  // 4 add steps
  await page.click('button[aria-label="テキスト追加"]')
  ok((await stepCount()) === 3, 'add text step -> 3')
  ok(await page.locator('.steps .step.selected').count() === 1, 'new step is selected')

  // 5 wrap in repeat: select steps 1-2 with shift
  await page.click('.steps .step:nth-child(1) .step-no')
  await page.click('.steps .step:nth-child(2) .step-no', { modifiers: ['Shift'] })
  ok(await page.locator('.steps .step.selected').count() === 2, 'shift range selects 2 steps')
  await page.click('[data-testid=wrap-repeat]')
  const types = await page.locator('.steps .step').evaluateAll((els) => els.map((e) => e.querySelector('select').value))
  ok(JSON.stringify(types) === JSON.stringify(['repeat', 'hotkey', 'hotkey', 'end_repeat', 'text']), 'wrapped: ' + types)
  ok((await status()).includes("1〜2"), 'wrap status mentions range: ' + (await status()))

  // 6 invalid repeat count blocks save
  await page.fill('.steps .step:nth-child(1) input[type=number]', '0')
  await page.keyboard.press('Control+s')
  ok((await status()).includes("手順 1"), 'invalid repeat blocks save: ' + (await status()))
  await page.fill('.steps .step:nth-child(1) input[type=number]', '4')

  // 7 hotkey part editing
  await page.click('.steps .step:nth-child(2) .hotkey > .icon-btn')
  const hk = await page.locator('.steps .step:nth-child(2) .hotkey select').count()
  ok(hk === 3, 'hotkey gets an extra key part (' + hk + ')')

  // 8 move & delete steps with confirm
  await page.click('.steps .step:nth-child(5) .step-no')
  await page.click('button[aria-label="上へ"]')
  const order = await page.locator('.steps .step').evaluateAll((els) => els.map((e) => e.querySelector('select').value))
  ok(order[3] === 'text' && order[4] === 'end_repeat', 'move up swaps text above end_repeat: ' + order)
  await page.click('button[aria-label="アクション削除"]')
  ok(await page.locator('.dialog').count() === 1, 'delete step asks for confirmation')
  await page.click('.dialog-actions button:has-text("削除")')
  ok((await stepCount()) === 4, 'step deleted -> 4')

  // 9 discard
  await page.click('button:has-text("破棄")')
  ok((await stepCount()) === 2, 'discard restores saved steps')

  // 10 test run flow
  await page.keyboard.press('F5')
  ok(await page.locator('.dialog').count() === 1, 'F5 opens test confirmation')
  await page.click('.dialog-actions button:has-text("実行")')
  await page.waitForSelector('.runbanner')
  ok(true, 'run banner shows while running')
  ok(await page.locator('.statusbar.running').count() === 1, 'status bar turns into run color')
  ok((await page.locator('.runbanner-text').textContent()).includes('あと'), 'countdown text: ' + (await page.locator('.runbanner-text').textContent()))
  await page.waitForSelector('.runbanner', { state: 'detached' })
  const runs = await page.evaluate(() => window.__runs)
  ok(runs.length === 1 && runs[0].name === '全選択(改)', 'runMacro called with current draft')
  ok((await status()).includes("完了"), 'status says finished: ' + (await status()))

  // 11 delete macro with confirm
  await page.click('.macro-item:nth-child(3)')
  await page.click('button[aria-label="削除"]:visible >> nth=0')
  ok(await page.locator('.dialog').count() === 1, 'delete macro asks')
  await page.click('.dialog-actions button:has-text("削除")')
  await page.waitForFunction(() => window.__saved.at(-1).length === 2)
  ok(await page.locator('.macro-item').count() === 2, 'macro deleted -> 2 left')

  // 12 new macro
  await page.click('[data-testid=new-macro]')
  await page.waitForFunction(() => window.__saved.at(-1).length === 3)
  ok(await page.locator('.macro-item').count() === 3, 'new macro added')
  ok((await tab()).includes('新しいマクロ'), 'new macro opened in editor')

  // 13 palette: switch language to English
  await page.keyboard.press('Control+k')
  await page.fill('.palette-input', 'English')
  await page.keyboard.press('Enter')
  await page.waitForFunction(() => document.documentElement.lang === 'en')
  ok((await page.textContent('.sidebar-title')) === 'Macros', 'UI switches to English')
  ok((await page.textContent('[data-testid=test]')).includes('Test run'), 'test button in English')

  // 14 settings toggles
  await page.click('[data-testid=open-settings]')
  await page.waitForSelector('[data-testid=settings]')
  await page.screenshot({ path: path.join(OUT, '07-settings-en.png') })
  await page.keyboard.press('Escape')
  ok(await page.locator('[data-testid=settings]').count() === 0, 'Esc closes settings')

  // 15 palette final look
  await page.keyboard.press('Control+k')
  await page.screenshot({ path: path.join(OUT, '08-palette-en.png') })
  await page.keyboard.press('Escape')

  console.log('page errors:', JSON.stringify(errors))
  console.log(failures === 0 ? 'ALL OK' : failures + ' FAILURES')
  await browser.close(); server.close()
  process.exit(failures === 0 && errors.length === 0 ? 0 : 1)
})()

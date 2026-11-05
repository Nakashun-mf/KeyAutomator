// ビルド成果物の健全性チェック（CI で electron-vite build の直後に実行）。
// sandbox の preload は electron 以外を require できないので、相対 require / 共有チャンクが無いことを確認する。
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'

const root = new URL('../out/', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')
const problems = []

for (const file of ['main/index.js', 'preload/index.js', 'preload/dialog.js', 'renderer/index.html', 'renderer/splash.html']) {
  if (!existsSync(join(root, file))) problems.push(`missing: out/${file}`)
}

for (const name of readdirSync(join(root, 'preload'))) {
  if (!name.endsWith('.js')) {
    problems.push(`preload must contain only single-file scripts, found: ${name}`)
    continue
  }
  const src = readFileSync(join(root, 'preload', name), 'utf8')
  for (const m of src.matchAll(/require\((["'])([^"']+)\1\)/g)) {
    if (m[2] !== 'electron') problems.push(`preload/${name} requires "${m[2]}" (sandboxed preload may only require electron)`)
  }
}

// renderer に Node 用モジュールが混ざっていないこと（core の import 経路の取り違え検知）
const assets = join(root, 'renderer/assets')
for (const name of readdirSync(assets).filter((n) => n.endsWith('.js'))) {
  const src = readFileSync(join(assets, name), 'utf8')
  if (/__vite-browser-external/.test(src)) problems.push(`renderer/assets/${name} pulls in a Node built-in (browser-external)`)
}

if (problems.length) {
  console.error('verify-build FAILED:\n - ' + problems.join('\n - '))
  process.exit(1)
}
console.log('verify-build ok')

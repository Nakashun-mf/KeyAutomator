import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { IPC } from '../src/shared/api'

// sandbox の preload は相対 require が使えない。共有チャンクが生まれない構成を保つための検査。
describe('preload scripts', () => {
  it('dialog preload hardcodes the same channel as IPC.dialogClose', () => {
    const src = readFileSync(join(__dirname, '../src/preload/dialog.ts'), 'utf8')
    expect(src).toContain(`'${IPC.dialogClose}'`)
  })

  it('dialog preload imports nothing but electron', () => {
    const src = readFileSync(join(__dirname, '../src/preload/dialog.ts'), 'utf8')
    const imports = [...src.matchAll(/^import .* from '(.*)'/gm)].map((m) => m[1])
    expect(imports).toEqual(['electron'])
  })
})

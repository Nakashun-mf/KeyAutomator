import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { createTranslator } from '../src/core/loc'
import { createRunner, CancelledError, type InputBackend } from '../src/core/runner'
import { createPaths } from '../src/core/paths'

export const tJa = createTranslator('ja-JP', 'ja-JP')
export const tEn = createTranslator('en-US', 'en-US')

export const tempPaths = () => createPaths(mkdtempSync(join(tmpdir(), 'keyauto-')))

export interface Captured {
  events: string[]
  sleeps: number[]
  logs: string[]
  dialogs: string[]
}

/** 実 SendInput を呼ばず、操作をキャプチャするランナー。待機は実時間を使わない。 */
export const captureRunner = () => {
  const cap: Captured = { events: [], sleeps: [], logs: [], dialogs: [] }
  const input: InputBackend = {
    key: (vk, down, ext) => (cap.events.push(`key:${vk.toString(16)}:${down ? 'dn' : 'up'}${ext ? ':ext' : ''}`), true),
    unicode: (cu, down) => (cap.events.push(`uni:${String.fromCharCode(cu)}:${down ? 'dn' : 'up'}`), true),
    mouse: (b, down) => (cap.events.push(`mouse:${b}:${down ? 'dn' : 'up'}`), true)
  }
  const runner = createRunner({
    input,
    t: tEn,
    log: (m) => void cap.logs.push(m),
    showDialog: async (m) => void cap.dialogs.push(m),
    sleep: async (ms, signal) => {
      if (signal?.aborted) throw new CancelledError()
      cap.sleeps.push(ms)
    }
  })
  return { runner, cap }
}

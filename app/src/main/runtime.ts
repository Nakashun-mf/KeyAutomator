import { createRunner, CancelledError, sliceSleep, type InputBackend, type Runner } from '../core/runner'
import type { Services } from './services'
import { showTopmostDialog } from './windows'

let inputPromise: Promise<InputBackend> | null = null

/** 実 SendInput は最初に使うときだけ読み込む（起動を遅くしない）。 */
const getInput = (): Promise<InputBackend> => {
  inputPromise ??= import('../native/win32').then((m) => m.createWin32Input())
  return inputPromise
}

const tick = (ms: number): Promise<void> => new Promise((r) => setTimeout(r, ms))

export const createRealRunner = async (services: Services, signal?: AbortSignal): Promise<Runner> =>
  createRunner({
    input: await getInput(),
    t: services.t,
    log: (m) => services.logger.write(m),
    sleep: (ms, s) => sliceSleep(ms, s, tick),
    showDialog: (message) => showTopmostDialog(message, services.t.get('Dialog_Ok'), signal)
  })

export { CancelledError }

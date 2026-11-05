import { isExtendedKey, resolveKey, resolveModifier, VK, VK_NONE } from './keys'
import type { Translator } from './loc'
import {
  findMatchingEnd,
  isRepeatEnd,
  isRepeatMarker,
  isRepeatStart,
  parseRepeatCount
} from './repeatBlock'
import type { ActionItem, MacroItem } from './types'

/** 実際の入力注入（Win32 SendInput）を抽象化する。テストではキャプチャ実装を渡す。 */
export interface InputBackend {
  /** Virtual-Key の押下／解放。失敗時は例外ではなく false。 */
  key(vk: number, down: boolean, extended: boolean): boolean
  /** UTF-16 コードユニットをそのまま送る（KEYEVENTF_UNICODE）。 */
  unicode(codeUnit: number, down: boolean): boolean
  mouse(button: 'left' | 'right' | 'middle', down: boolean): boolean
}

export interface RunnerEnv {
  input: InputBackend
  /** 待機。signal が abort されたら CancelledError で reject する。 */
  sleep(ms: number, signal?: AbortSignal): Promise<void>
  /** 確認ダイアログ。OK まで解決しない。 */
  showDialog(message: string): Promise<void>
  log(message: string): void
  t: Translator
}

export class CancelledError extends Error {
  constructor() {
    super('cancelled')
    this.name = 'CancelledError'
  }
}

const throwIfAborted = (signal?: AbortSignal): void => {
  if (signal?.aborted) throw new CancelledError()
}

/** 待機を 50ms ずつに分け、中断を素早く拾う。 */
export const sliceSleep = async (
  ms: number,
  signal: AbortSignal | undefined,
  tick: (ms: number) => Promise<void>
): Promise<void> => {
  let remaining = ms
  while (remaining > 0) {
    throwIfAborted(signal)
    const step = Math.min(50, remaining)
    await tick(step)
    remaining -= step
  }
}

export const createRunner = (env: RunnerEnv) => {
  const { input, t } = env
  const sleep = (ms: number, signal?: AbortSignal): Promise<void> =>
    ms <= 0 ? Promise.resolve() : env.sleep(ms, signal)

  const send = (ok: boolean): void => {
    if (!ok) env.log(t.format('Log_SendInputFailed', 'n/a'))
  }
  const sendVk = (vk: number, down: boolean): void => send(input.key(vk, down, isExtendedKey(vk)))

  const sendText = async (text: string, signal?: AbortSignal): Promise<void> => {
    if (!text) return
    // C# の char と同じく UTF-16 コードユニット単位で送る
    for (let i = 0; i < text.length; i++) {
      throwIfAborted(signal)
      const ch = text.charCodeAt(i)
      if (ch === 0x0a) {
        sendVk(VK.RETURN, true)
        sendVk(VK.RETURN, false)
      } else if (ch === 0x0d) {
        // CR は無視
      } else {
        send(input.unicode(ch, true))
        send(input.unicode(ch, false))
      }
      await sleep(5, signal)
    }
  }

  const sendKey = async (keyName: string): Promise<void> => {
    const vk = resolveKey(keyName)
    if (vk === VK_NONE) {
      env.log(t.format('Log_UnknownKey', keyName))
      return
    }
    sendVk(vk, true)
    await sleep(20)
    sendVk(vk, false)
    await sleep(10)
  }

  const sendHotkey = async (hotkey: string): Promise<void> => {
    if (!hotkey || hotkey.trim() === '') return
    const parts = hotkey.split(/[+\s]+/).map((s) => s.trim()).filter((s) => s.length > 0)
    const modifiers: number[] = []
    let main = VK_NONE
    for (const part of parts) {
      const mod = resolveModifier(part)
      if (mod !== VK_NONE) modifiers.push(mod)
      else main = resolveKey(part)
    }
    if (main === VK_NONE) {
      env.log(t.format('Log_InvalidHotkeyMain', hotkey))
      return
    }
    for (const mod of modifiers) {
      sendVk(mod, true)
      await sleep(10)
    }
    sendVk(main, true)
    await sleep(20)
    sendVk(main, false)
    await sleep(10)
    for (let i = modifiers.length - 1; i >= 0; i--) {
      sendVk(modifiers[i], false)
      await sleep(10)
    }
  }

  const click = async (button: 'left' | 'right' | 'middle'): Promise<void> => {
    send(input.mouse(button, true))
    await sleep(30)
    send(input.mouse(button, false))
    await sleep(10)
  }

  const sendMouse = async (action: string): Promise<void> => {
    switch ((action ?? '').trim().toUpperCase()) {
      case 'LEFT':
        return click('left')
      case 'RIGHT':
        return click('right')
      case 'MIDDLE':
        return click('middle')
      case 'LEFT_DOUBLE':
      case 'DOUBLE':
      case 'DBLCLICK':
        await click('left')
        await sleep(40)
        return click('left')
      default:
        env.log(t.format('Log_UnknownMouse', action))
    }
  }

  const executeAction = async (action: ActionItem, signal?: AbortSignal): Promise<void> => {
    throwIfAborted(signal)
    const value = action.value ?? ''
    switch ((action.type ?? '').trim().toLowerCase()) {
      case 'text':
        return sendText(value, signal)
      case 'key':
        return sendKey(value)
      case 'hotkey':
        return sendHotkey(value)
      case 'mouse':
        return sendMouse(value)
      case 'wait': {
        const sec = Number(value.trim())
        if (value.trim() !== '' && Number.isFinite(sec)) return sleep(Math.floor(Math.max(0, sec) * 1000), signal)
        env.log(t.format('Log_InvalidWait', value))
        return
      }
      case 'dialog':
        return env.showDialog(value.trim() === '' ? t.get('Dialog_DefaultMessage') : value.trim())
      case 'repeat':
      case 'end_repeat':
        // executeActions 側で解釈する。単体呼び出し時は何もしない。
        return
      default:
        env.log(t.format('Log_UnknownActionType', action.type))
    }
  }

  interface Frame {
    bodyStart: number
    remaining: number
  }

  const hasMoreExecutableWork = (actions: readonly ActionItem[], nextIndex: number, stack: readonly Frame[]): boolean => {
    // 最終周回（remaining == 1）ではジャンプバックしないので、末尾の余分な間隔を入れない
    if (stack.some((f) => f.remaining > 1)) return true
    for (let i = nextIndex; i < actions.length; i++) {
      if (!isRepeatMarker(actions[i].type)) return true
      if (isRepeatStart(actions[i].type)) {
        const end = findMatchingEnd(actions, i)
        if (end < 0) return false
        const count = parseRepeatCount(actions[i].value)
        if (count !== null && count > 0 && end > i + 1) return true
        i = end
      }
    }
    return false
  }

  /** repeat / end_repeat を解釈しながらアクション列を実行する。 */
  const executeActions = async (
    actions: readonly ActionItem[],
    stepDelayMs: number,
    signal?: AbortSignal
  ): Promise<void> => {
    if (actions.length === 0) return
    const stack: Frame[] = []
    let i = 0
    while (i < actions.length) {
      throwIfAborted(signal)
      const action = actions[i]
      const type = (action.type ?? '').trim()

      if (isRepeatStart(type)) {
        const count = parseRepeatCount(action.value)
        if (count === null) {
          env.log(t.format('Log_InvalidRepeatCount', action.value))
          i++
          continue
        }
        const end = findMatchingEnd(actions, i)
        if (end < 0) {
          env.log(t.format('Log_MissingEndRepeat', i + 1))
          return
        }
        if (end === i + 1) {
          // 空ボディ → ブロックを飛ばす
          i = end + 1
          continue
        }
        stack.push({ bodyStart: i + 1, remaining: count })
        i++
        continue
      }

      if (isRepeatEnd(type)) {
        const frame = stack.pop()
        if (!frame) {
          env.log(t.format('Log_OrphanEndRepeat', i + 1))
          i++
          continue
        }
        const remaining = frame.remaining - 1
        if (remaining > 0) {
          stack.push({ bodyStart: frame.bodyStart, remaining })
          if (stepDelayMs > 0) await sleep(stepDelayMs, signal)
          i = frame.bodyStart
          continue
        }
        i++
        continue
      }

      await executeAction(action, signal)
      i++
      if (stepDelayMs > 0 && hasMoreExecutableWork(actions, i, stack)) await sleep(stepDelayMs, signal)
    }
  }

  const executeMacro = async (
    macro: MacroItem,
    actionDelaySec: number,
    signal?: AbortSignal
  ): Promise<void> => {
    await sleep(Math.floor(Math.max(0, macro.delay_sec) * 1000), signal)
    await executeActions(macro.actions, Math.floor(Math.max(0, actionDelaySec) * 1000), signal)
  }

  return { sendText, sendKey, sendHotkey, sendMouse, executeAction, executeActions, executeMacro }
}

export type Runner = ReturnType<typeof createRunner>

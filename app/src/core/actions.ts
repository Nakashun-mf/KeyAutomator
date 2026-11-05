import {
  REPEAT_DEFAULT_COUNT,
  REPEAT_END,
  REPEAT_MAX_COUNT,
  REPEAT_START,
  isRepeatEnd,
  isRepeatStart,
  parseRepeatCount
} from './repeatBlock'
import type { ActionItem } from './types'
import type { Translator } from './loc'

export interface ActionTypeOption {
  code: string
  labelKey: string
  hintKey: string
  placeholderKey?: string
}

export const ACTION_TYPES: readonly ActionTypeOption[] = [
  { code: 'text', labelKey: 'ActionType_text', hintKey: 'ActionHint_text', placeholderKey: 'ActionPlaceholder_text' },
  { code: 'key', labelKey: 'ActionType_key', hintKey: 'ActionHint_key' },
  { code: 'hotkey', labelKey: 'ActionType_hotkey', hintKey: 'ActionHint_hotkey' },
  { code: 'mouse', labelKey: 'ActionType_mouse', hintKey: 'ActionHint_mouse' },
  { code: 'wait', labelKey: 'ActionType_wait', hintKey: 'ActionHint_wait', placeholderKey: 'ActionPlaceholder_wait' },
  { code: 'dialog', labelKey: 'ActionType_dialog', hintKey: 'ActionHint_dialog', placeholderKey: 'ActionPlaceholder_dialog' },
  { code: REPEAT_START, labelKey: 'ActionType_repeat', hintKey: 'ActionHint_repeat', placeholderKey: 'ActionPlaceholder_repeat' },
  { code: REPEAT_END, labelKey: 'ActionType_end_repeat', hintKey: 'ActionHint_end_repeat' }
]

export const getActionType = (code: string | null | undefined): ActionTypeOption =>
  ACTION_TYPES.find((t) => t.code === (code ?? '').toLowerCase()) ?? ACTION_TYPES[0]

export interface MouseActionOption {
  code: string
  labelKey: string
}

export const MOUSE_ACTIONS: readonly MouseActionOption[] = [
  { code: 'LEFT', labelKey: 'Mouse_LEFT' },
  { code: 'RIGHT', labelKey: 'Mouse_RIGHT' },
  { code: 'MIDDLE', labelKey: 'Mouse_MIDDLE' },
  { code: 'LEFT_DOUBLE', labelKey: 'Mouse_LEFT_DOUBLE' }
]

export const isKnownMouseAction = (code: string | null | undefined): boolean =>
  MOUSE_ACTIONS.some((m) => m.code === (code ?? '').trim().toUpperCase())

export interface SpecialKeyOption {
  code: string
  /** 表示名。numpad は labelKey/labelArg で翻訳する。 */
  label: string
  labelKey?: string
  labelArg?: string
}

const buildSpecialKeys = (): SpecialKeyOption[] => {
  const list: SpecialKeyOption[] = []
  const plain = (code: string, label: string): void => void list.push({ code, label })
  const loc = (code: string, labelKey: string, labelArg: string): void =>
    void list.push({ code, label: labelArg, labelKey, labelArg })

  plain('CTRL', 'Ctrl')
  plain('SHIFT', 'Shift')
  plain('ALT', 'Alt')
  plain('LWIN', 'Windows')
  plain('ENTER', 'Enter')
  plain('TAB', 'Tab')
  plain('ESC', 'Esc')
  plain('SPACE', 'Space')
  plain('BACKSPACE', 'Backspace')
  plain('DELETE', 'Delete')
  plain('INSERT', 'Insert')
  plain('HOME', 'Home')
  plain('END', 'End')
  plain('PAGEUP', 'Page Up')
  plain('PAGEDOWN', 'Page Down')
  plain('UP', '↑')
  plain('DOWN', '↓')
  plain('LEFT', '←')
  plain('RIGHT', '→')
  for (let c = 0x41; c <= 0x5a; c++) plain(String.fromCharCode(c), String.fromCharCode(c))
  for (let d = 0; d <= 9; d++) plain(String(d), String(d))
  for (let f = 1; f <= 12; f++) plain(`F${f}`, `F${f}`)
  for (let n = 0; n <= 9; n++) loc(`NUMPAD${n}`, 'Key_NumpadDigit', String(n))
  loc('MULTIPLY', 'Key_NumpadSymbol', '*')
  loc('ADD', 'Key_NumpadSymbol', '+')
  loc('SUBTRACT', 'Key_NumpadSymbol', '-')
  loc('DECIMAL', 'Key_NumpadSymbol', '.')
  loc('DIVIDE', 'Key_NumpadSymbol', '/')
  plain('OEM_PLUS', '= +')
  plain('OEM_MINUS', '- _')
  plain('OEM_COMMA', ',')
  plain('OEM_PERIOD', '.')
  plain('OEM_1', '; :')
  plain('OEM_2', '/ ?')
  plain('OEM_3', '` ~')
  plain('OEM_4', '[ {')
  plain('OEM_5', '\\ |')
  plain('OEM_6', '] }')
  plain('OEM_7', "' \"")
  return list
}

export const SPECIAL_KEYS: readonly SpecialKeyOption[] = buildSpecialKeys()

const KEY_ALIASES: Record<string, string> = {
  RETURN: 'ENTER',
  ESCAPE: 'ESC',
  BS: 'BACKSPACE',
  DEL: 'DELETE',
  INS: 'INSERT',
  PGUP: 'PAGEUP',
  PGDN: 'PAGEDOWN',
  CONTROL: 'CTRL',
  CTL: 'CTRL',
  WINDOWS: 'LWIN',
  WIN: 'LWIN',
  RWIN: 'LWIN'
}

export const normalizeKeyCode = (code: string): string => {
  const t = code.trim().toUpperCase()
  return KEY_ALIASES[t] ?? t
}

export const isKnownSpecialKey = (code: string | null | undefined): boolean =>
  code != null && code.trim() !== '' && SPECIAL_KEYS.some((k) => k.code === normalizeKeyCode(code))

export const specialKeyLabel = (opt: SpecialKeyOption, t: Translator): string =>
  opt.labelKey ? t.format(opt.labelKey, opt.labelArg ?? '') : opt.label

const MODIFIERS = new Set(['CTRL', 'SHIFT', 'ALT', 'LWIN', 'RWIN', 'WIN', 'WINDOWS', 'CONTROL', 'CTL', 'MENU'])

export const splitHotkey = (hotkey: string): string[] =>
  hotkey.split(/[+\s]+/).map((s) => s.trim()).filter((s) => s.length > 0)

export type HotkeyError = 'empty' | 'modifier-duplicate' | 'main-once' | 'need-main'

export interface HotkeyCheck {
  ok: boolean
  error?: HotkeyError
  /** modifier-duplicate のときの重複キー名 */
  detail?: string
}

export const checkHotkey = (hotkey: string | null | undefined): HotkeyCheck => {
  if (hotkey == null || hotkey.trim() === '') return { ok: false, error: 'empty' }
  const parts = splitHotkey(hotkey)
  if (parts.length === 0) return { ok: false, error: 'empty' }
  const seen = new Set<string>()
  let hasMain = false
  for (const part of parts) {
    const n = normalizeKeyCode(part)
    if (MODIFIERS.has(n)) {
      if (seen.has(n)) return { ok: false, error: 'modifier-duplicate', detail: n }
      seen.add(n)
    } else {
      if (hasMain) return { ok: false, error: 'main-once' }
      hasMain = true
    }
  }
  return hasMain ? { ok: true } : { ok: false, error: 'need-main' }
}

const parseSeconds = (value: string): number | null => {
  const n = Number(value.trim())
  return value.trim() !== '' && Number.isFinite(n) ? n : null
}

export const isNumeric = (value: string): boolean => parseSeconds(value) !== null

/** 手順 1 件の入力検証。問題があればエラー文を返す。 */
export const validateAction = (action: ActionItem, t: Translator): string | null => {
  const type = action.type.toLowerCase()
  if (type === 'hotkey') {
    const c = checkHotkey(action.value)
    if (c.ok) return null
    switch (c.error) {
      case 'empty':
        return t.get('Error_HotkeyEmpty')
      case 'modifier-duplicate':
        return t.format('Error_ModifierDuplicate', c.detail ?? '')
      case 'main-once':
        return t.get('Error_MainKeyOnce')
      default:
        return t.get('Error_NeedMainKey')
    }
  }
  if (type === 'wait' && !isNumeric(action.value)) return t.get('Error_WaitNumeric')
  if (isRepeatStart(type) && parseRepeatCount(action.value) === null) {
    return t.format('Error_RepeatCount', REPEAT_MAX_COUNT)
  }
  return null
}

/** 種別を切り替えたときに value を既定値へ整える。 */
export const coerceValueForType = (type: string, value: string, t: Translator): string => {
  const ty = type.toLowerCase()
  if (ty === 'key') return isKnownSpecialKey(value) ? normalizeKeyCode(value) : 'ENTER'
  if (ty === 'hotkey') return value.trim() === '' || !checkHotkey(value).ok ? 'CTRL+S' : value
  if (ty === 'mouse') return isKnownMouseAction(value) ? value.trim().toUpperCase() : 'LEFT'
  if (ty === 'dialog') {
    return value.trim() === '' || isNumeric(value) ? t.get('Dialog_DefaultMessage') : value
  }
  if (isRepeatStart(ty)) {
    return parseRepeatCount(value) !== null ? String(parseRepeatCount(value)) : String(REPEAT_DEFAULT_COUNT)
  }
  if (isRepeatEnd(ty)) return ''
  return value
}

/** 既定値つきの新規手順。 */
export const newAction = (type: string, t: Translator): ActionItem => {
  switch (type) {
    case 'key':
      return { type, value: 'ENTER' }
    case 'hotkey':
      return { type, value: 'CTRL+S' }
    case 'mouse':
      return { type, value: 'LEFT' }
    case 'wait':
      return { type, value: '0.5' }
    case 'dialog':
      return { type, value: t.get('Dialog_DefaultMessage') }
    default:
      return { type, value: '' }
  }
}

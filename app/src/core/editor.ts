import {
  coerceValueForType,
  isKnownMouseAction,
  isKnownSpecialKey,
  newAction,
  normalizeKeyCode,
  splitHotkey,
  validateAction
} from './actions'
import type { Translator } from './loc'
import {
  REPEAT_DEFAULT_COUNT,
  REPEAT_END,
  REPEAT_MAX_COUNT,
  REPEAT_START,
  isRepeatEnd,
  isRepeatStart,
  parseRepeatCount,
  validateRepeats
} from './repeatBlock'
import { isValidAlias, type ActionItem, type MacroItem } from './types'

/** エディタ上の 1 手順。uid は React の key / 選択管理用（保存はしない）。 */
export interface DraftAction extends ActionItem {
  uid: string
}

/** エディタ上のマクロ。数値欄は入力途中を許すため文字列で持つ。 */
export interface Draft {
  id: string
  name: string
  alias: string
  delay: string
  actions: DraftAction[]
}

let uidCounter = 0
export const makeUid = (): string => `a${++uidCounter}`

/** 保存済みの手順を、エディタで扱える正規形にする（key/mouse/hotkey/dialog/repeat の値補正）。 */
const normalizeAction = (a: ActionItem, t: Translator): ActionItem => {
  const type = a.type.trim() === '' ? 'text' : a.type
  const value = a.value ?? ''
  switch (type.toLowerCase()) {
    case 'key':
      return { type: 'key', value: isKnownSpecialKey(value) ? normalizeKeyCode(value) : 'ENTER' }
    case 'hotkey': {
      const parts = splitHotkey(value.trim() === '' ? 'CTRL+S' : value)
      return { type: 'hotkey', value: (parts.length ? parts : ['CTRL', 'S']).map(normalizeKeyCode).join('+') }
    }
    case 'mouse':
      return { type: 'mouse', value: isKnownMouseAction(value) ? value.trim().toUpperCase() : 'LEFT' }
    case 'dialog':
      return { type: 'dialog', value: value.trim() === '' ? t.get('Dialog_DefaultMessage') : value }
    case REPEAT_START: {
      const n = parseRepeatCount(value)
      return { type: REPEAT_START, value: String(n ?? REPEAT_DEFAULT_COUNT) }
    }
    case REPEAT_END:
      return { type: REPEAT_END, value: '' }
    default:
      return { type, value }
  }
}

export const draftFromMacro = (m: MacroItem, t: Translator): Draft => ({
  id: String(m.id),
  name: m.name,
  alias: m.alias,
  delay: String(m.delay_sec),
  actions: m.actions.map((a) => ({ uid: makeUid(), ...normalizeAction(a, t) }))
})

export const emptyDraft = (): Draft => ({ id: '1', name: '', alias: '', delay: '0', actions: [] })

const toNumber = (s: string): number => {
  const n = Number(s.trim())
  return s.trim() !== '' && Number.isFinite(n) ? n : 0
}

/** テスト実行・比較用。検証はしない。 */
export const draftToMacro = (d: Draft): MacroItem => ({
  id: Math.round(toNumber(d.id)),
  name: d.name.trim(),
  alias: d.alias.trim(),
  delay_sec: toNumber(d.delay),
  actions: d.actions.map((a) => ({ type: a.type, value: a.value }))
})

/** 保存済みマクロとエディタ内容が違うか。 */
export const isDraftDirty = (d: Draft | null, saved: MacroItem | null, t: Translator): boolean => {
  if (!d || !saved) return false
  const base = draftFromMacro(saved, t)
  const strip = (x: Draft) =>
    JSON.stringify([x.id, x.name, x.alias, x.delay, x.actions.map((a) => [a.type, a.value])])
  return strip(d) !== strip(base)
}

export type SaveValidation = { ok: true; macro: MacroItem } | { ok: false; message: string }

/** 保存前の検証（TrySaveMacro と同じ順序・同じメッセージ）。 */
export const validateDraft = (
  d: Draft,
  macros: readonly MacroItem[],
  originalId: number,
  t: Translator
): SaveValidation => {
  const fail = (message: string): SaveValidation => ({ ok: false, message })

  if (d.name.trim() === '') return fail(t.get('Status_NameRequired'))

  const idNum = Number(d.id.trim())
  if (d.id.trim() === '' || !Number.isFinite(idNum) || Math.abs(idNum - Math.round(idNum)) > 0.001) {
    return fail(t.get('Status_IdMustBeInteger'))
  }
  const id = Math.round(idNum)
  if (id < 1) return fail(t.get('Status_IdMustBePositive'))

  const alias = d.alias.trim()
  if (!isValidAlias(alias)) return fail(t.get('Error_AliasCharset'))

  if (macros.some((m) => m.id === id && m.id !== originalId)) return fail(t.format('Status_IdInUse', id))
  if (alias !== '' && macros.some((m) => m.id !== originalId && m.alias.toLowerCase() === alias.toLowerCase())) {
    return fail(t.format('Status_AliasInUse', alias))
  }

  for (let i = 0; i < d.actions.length; i++) {
    const err = validateAction(d.actions[i], t)
    if (err) return fail(t.format('Status_StepError', i + 1, err))
  }

  const loops = validateRepeats(d.actions)
  if (!loops.ok) {
    if (loops.code === 'count') return fail(t.format('Error_RepeatCountAtStep', loops.step, REPEAT_MAX_COUNT))
    if (loops.code === 'orphan') return fail(t.format('Error_OrphanEndRepeat', loops.step))
    return fail(t.format('Error_UnclosedRepeat', loops.open))
  }

  return {
    ok: true,
    macro: {
      id,
      name: d.name.trim(),
      alias,
      delay_sec: toNumber(d.delay),
      actions: d.actions.map((a) => ({ type: a.type, value: a.value }))
    }
  }
}

// ----------------------------------------------------------------- 手順の編集

/** 選択中の最も下の手順の直後。未選択なら末尾。 */
export const resolveInsertIndex = (actions: readonly DraftAction[], selected: ReadonlySet<string>): number => {
  let anchor = -1
  actions.forEach((a, i) => {
    if (selected.has(a.uid)) anchor = i
  })
  return anchor < 0 ? actions.length : anchor + 1
}

export const insertAction = (
  actions: readonly DraftAction[],
  selected: ReadonlySet<string>,
  type: string,
  t: Translator
): { actions: DraftAction[]; added: DraftAction } => {
  const added: DraftAction = { uid: makeUid(), ...newAction(type, t) }
  const next = [...actions]
  next.splice(resolveInsertIndex(actions, selected), 0, added)
  return { actions: next, added }
}

export interface WrapResult {
  actions: DraftAction[]
  start: DraftAction
  /** 囲んだ範囲（1 始まり）。空ブロックなら null。 */
  range: { first: number; last: number } | null
}

/** 選択中の手順を repeat ～ end_repeat で囲む。未選択なら空ブロックを挿入。 */
export const wrapInRepeat = (actions: readonly DraftAction[], selected: ReadonlySet<string>): WrapResult => {
  const start: DraftAction = { uid: makeUid(), type: REPEAT_START, value: String(REPEAT_DEFAULT_COUNT) }
  const end: DraftAction = { uid: makeUid(), type: REPEAT_END, value: '' }
  const next = [...actions]
  const indices = actions.flatMap((a, i) => (selected.has(a.uid) ? [i] : []))

  if (indices.length === 0) {
    const at = actions.length
    next.splice(at, 0, start, end)
    return { actions: next, start, range: null }
  }
  const first = indices[0]
  const last = indices[indices.length - 1]
  next.splice(first, 0, start)
  next.splice(last + 2, 0, end)
  return { actions: next, start, range: { first: first + 1, last: last + 1 } }
}

export const removeActions = (actions: readonly DraftAction[], selected: ReadonlySet<string>): DraftAction[] =>
  actions.filter((a) => !selected.has(a.uid))

/** 選択した手順を 1 つ上／下へ。端にある選択はそれ以上動かさない。 */
export const moveSelected = (
  actions: readonly DraftAction[],
  selected: ReadonlySet<string>,
  delta: -1 | 1
): DraftAction[] => {
  const next = [...actions]
  if (delta < 0) {
    for (let i = 1; i < next.length; i++) {
      if (selected.has(next[i].uid) && !selected.has(next[i - 1].uid)) [next[i - 1], next[i]] = [next[i], next[i - 1]]
    }
  } else {
    for (let i = next.length - 2; i >= 0; i--) {
      if (selected.has(next[i].uid) && !selected.has(next[i + 1].uid)) [next[i + 1], next[i]] = [next[i], next[i + 1]]
    }
  }
  return next
}

export const canMoveSelected = (actions: readonly DraftAction[], selected: ReadonlySet<string>, delta: -1 | 1): boolean => {
  if (selected.size === 0) return false
  return moveSelected(actions, selected, delta).some((a, i) => a.uid !== actions[i].uid)
}

/** ドラッグ＆ドロップなどの並べ替え。 */
export const reorder = <T>(list: readonly T[], from: number, to: number): T[] => {
  if (from === to || from < 0 || to < 0 || from >= list.length || to >= list.length) return [...list]
  const next = [...list]
  const [item] = next.splice(from, 1)
  next.splice(to, 0, item)
  return next
}

/** 種別変更。value は新しい種別に合わせて整える。 */
export const changeActionType = (action: DraftAction, type: string, t: Translator): DraftAction => ({
  ...action,
  type,
  value: coerceValueForType(type, action.value, t)
})

export const hotkeyParts = (value: string): string[] => {
  const parts = splitHotkey(value)
  return parts.length ? parts.map(normalizeKeyCode) : ['CTRL', 'S']
}

export const joinHotkey = (parts: readonly string[]): string => {
  const codes = parts.map(normalizeKeyCode).filter((c) => c !== '')
  return codes.length === 0 ? 'CTRL+S' : codes.join('+')
}

export const countLoops = (actions: readonly ActionItem[]): number => actions.filter((a) => isRepeatStart(a.type)).length

export const isLoopMarker = (type: string): boolean => isRepeatStart(type) || isRepeatEnd(type)

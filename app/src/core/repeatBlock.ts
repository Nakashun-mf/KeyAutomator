import type { ActionItem } from './types'

/** フラットな actions 上の repeat / end_repeat ブロックを扱う。 */
export const REPEAT_START = 'repeat'
export const REPEAT_END = 'end_repeat'
export const REPEAT_DEFAULT_COUNT = 2
export const REPEAT_MAX_COUNT = 9999

const eq = (a: string | null | undefined, b: string): boolean =>
  (a ?? '').toLowerCase() === b

export const isRepeatStart = (type: string | null | undefined): boolean => eq(type, REPEAT_START)
export const isRepeatEnd = (type: string | null | undefined): boolean => eq(type, REPEAT_END)
export const isRepeatMarker = (type: string | null | undefined): boolean =>
  isRepeatStart(type) || isRepeatEnd(type)

/** 回数（1〜9999 の整数）を解釈する。不正なら null。 */
export const parseRepeatCount = (value: string | null | undefined): number | null => {
  if (value == null || value.trim() === '') return null
  const n = Number(value.trim())
  if (!Number.isFinite(n)) return null
  if (n < 1 || n > REPEAT_MAX_COUNT || Math.abs(n - Math.round(n)) > 0.001) return null
  return Math.round(n)
}

/** startIndex の repeat に対応する end_repeat のインデックス。無ければ -1。 */
export const findMatchingEnd = (actions: readonly ActionItem[], startIndex: number): number => {
  if (startIndex < 0 || startIndex >= actions.length || !isRepeatStart(actions[startIndex].type)) {
    return -1
  }
  let depth = 0
  for (let i = startIndex + 1; i < actions.length; i++) {
    if (isRepeatStart(actions[i].type)) {
      depth++
    } else if (isRepeatEnd(actions[i].type)) {
      if (depth === 0) return i
      depth--
    }
  }
  return -1
}

/** 各アクションの表示用ネスト深さ（repeat 行は開始深さ、end_repeat は対応開始と同じ深さ）。 */
export const computeDepths = (actions: readonly ActionItem[]): number[] => {
  const depths: number[] = []
  let depth = 0
  for (const action of actions) {
    if (isRepeatEnd(action.type)) depth = Math.max(0, depth - 1)
    depths.push(depth)
    if (isRepeatStart(action.type)) depth++
  }
  return depths
}

export type RepeatValidation =
  | { ok: true }
  | { ok: false; code: 'count'; step: number }
  | { ok: false; code: 'orphan'; step: number }
  | { ok: false; code: 'unclosed'; open: number }

/** 開始／終了の対応と回数を検証する。 */
export const validateRepeats = (actions: readonly ActionItem[]): RepeatValidation => {
  let open = 0
  for (let i = 0; i < actions.length; i++) {
    const { type, value } = actions[i]
    if (isRepeatStart(type)) {
      if (parseRepeatCount(value) === null) return { ok: false, code: 'count', step: i + 1 }
      open++
    } else if (isRepeatEnd(type)) {
      if (open === 0) return { ok: false, code: 'orphan', step: i + 1 }
      open--
    }
  }
  return open > 0 ? { ok: false, code: 'unclosed', open } : { ok: true }
}

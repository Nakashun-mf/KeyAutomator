import { describe, expect, it } from 'vitest'
import {
  canMoveSelected,
  changeActionType,
  draftFromMacro,
  draftToMacro,
  hotkeyParts,
  insertAction,
  isDraftDirty,
  joinHotkey,
  moveSelected,
  removeActions,
  reorder,
  validateDraft,
  wrapInRepeat,
  type Draft,
  type DraftAction
} from '../src/core/editor'
import type { MacroItem } from '../src/core/types'
import { tEn, tJa } from './helpers'

const macro = (over: Partial<MacroItem> = {}): MacroItem => ({
  id: 1,
  name: 'M',
  alias: 'm',
  delay_sec: 3,
  actions: [],
  ...over
})
const act = (uid: string, type = 'key', value = 'A'): DraftAction => ({ uid, type, value })
const sel = (...uids: string[]) => new Set(uids)
const uids = (l: DraftAction[]) => l.map((a) => a.uid)

describe('draft conversion', () => {
  it('normalizes loaded actions like the C# editor model', () => {
    const d = draftFromMacro(
      macro({
        actions: [
          { type: 'key', value: 'return' },
          { type: 'key', value: 'zzz' },
          { type: 'hotkey', value: 'control + s' },
          { type: 'mouse', value: 'bogus' },
          { type: 'dialog', value: '' },
          { type: 'repeat', value: 'x' },
          { type: 'end_repeat', value: 'junk' },
          { type: '', value: 'hi' },
          { type: 'custom', value: 'kept' }
        ]
      }),
      tEn
    )
    expect(d.actions.map((a) => [a.type, a.value])).toEqual([
      ['key', 'ENTER'],
      ['key', 'ENTER'],
      ['hotkey', 'CTRL+S'],
      ['mouse', 'LEFT'],
      ['dialog', 'Press OK to continue to the next step.'],
      ['repeat', '2'],
      ['end_repeat', ''],
      ['text', 'hi'],
      ['custom', 'kept']
    ])
  })

  it('round-trips through draftToMacro and gives each step a unique uid', () => {
    const m = macro({ actions: [{ type: 'text', value: 'a' }, { type: 'wait', value: '1' }] })
    const d = draftFromMacro(m, tEn)
    expect(draftToMacro(d)).toEqual(m)
    expect(new Set(d.actions.map((a) => a.uid)).size).toBe(2)
  })

  it('dirty detection ignores uid changes but sees edits', () => {
    const m = macro({ actions: [{ type: 'text', value: 'a' }] })
    const d = draftFromMacro(m, tEn)
    expect(isDraftDirty(d, m, tEn)).toBe(false)
    expect(isDraftDirty(draftFromMacro(m, tEn), m, tEn)).toBe(false)
    expect(isDraftDirty({ ...d, name: 'N' }, m, tEn)).toBe(true)
    expect(isDraftDirty({ ...d, actions: [{ ...d.actions[0], value: 'b' }] }, m, tEn)).toBe(true)
    expect(isDraftDirty(null, m, tEn)).toBe(false)
  })
})

describe('validateDraft', () => {
  const others = [macro({ id: 1, alias: 'one' }), macro({ id: 2, alias: 'two' })]
  const base = (over: Partial<Draft> = {}): Draft => ({ id: '1', name: 'N', alias: '', delay: '1.5', actions: [], ...over })
  const msg = (d: Draft, t = tEn) => {
    const r = validateDraft(d, others, 1, t)
    return r.ok ? 'OK' : r.message
  }

  it('accepts a valid macro and trims fields', () => {
    const r = validateDraft(base({ name: '  N  ', alias: ' al ' }), others, 1, tEn)
    expect(r).toEqual({ ok: true, macro: { id: 1, name: 'N', alias: 'al', delay_sec: 1.5, actions: [] } })
  })

  it('checks in the same order as the original app', () => {
    expect(msg(base({ name: ' ' }))).toBe(tEn.get('Status_NameRequired'))
    expect(msg(base({ id: '1.5' }))).toBe(tEn.get('Status_IdMustBeInteger'))
    expect(msg(base({ id: '' }))).toBe(tEn.get('Status_IdMustBeInteger'))
    expect(msg(base({ id: 'x' }))).toBe(tEn.get('Status_IdMustBeInteger'))
    expect(msg(base({ id: '0' }))).toBe(tEn.get('Status_IdMustBePositive'))
    expect(msg(base({ alias: 'a b' }))).toBe(tEn.get('Error_AliasCharset'))
    expect(msg(base({ id: '2' }))).toBe(tEn.format('Status_IdInUse', 2))
    expect(msg(base({ alias: 'TWO' }))).toBe(tEn.format('Status_AliasInUse', 'TWO'))
  })

  it('own id and own alias are not conflicts', () => {
    expect(msg(base({ id: '1', alias: 'one' }))).toBe('OK')
  })

  it('reports the failing step with a localized reason', () => {
    const d = base({ actions: [act('a'), act('b', 'wait', 'abc')] })
    expect(msg(d, tJa)).toBe(tJa.format('Status_StepError', 2, tJa.get('Error_WaitNumeric')))
  })

  it('reports repeat structure problems', () => {
    expect(msg(base({ actions: [act('a', 'repeat', '2')] }))).toBe(tEn.format('Error_UnclosedRepeat', 1))
    expect(msg(base({ actions: [act('a'), act('b', 'end_repeat', '')] }))).toBe(tEn.format('Error_OrphanEndRepeat', 2))
  })
})

describe('step editing', () => {
  const list = [act('a'), act('b'), act('c'), act('d')]

  it('insertAction goes after the lowest selected step, else at the end', () => {
    expect(uids(insertAction(list, sel('b', 'a'), 'wait', tEn).actions).slice(0, 4)).toEqual(['a', 'b', expect.any(String), 'c'])
    const end = insertAction(list, sel(), 'key', tEn)
    expect(end.actions[4]).toBe(end.added)
    expect(end.added).toMatchObject({ type: 'key', value: 'ENTER' })
  })

  it('wrapInRepeat wraps the selection range, or inserts an empty block', () => {
    const w = wrapInRepeat(list, sel('b', 'c'))
    expect(w.actions.map((a) => a.type)).toEqual(['key', 'repeat', 'key', 'key', 'end_repeat', 'key'])
    expect(w.range).toEqual({ first: 2, last: 3 })
    expect(w.start.value).toBe('2')
    const empty = wrapInRepeat(list, sel())
    expect(empty.actions.slice(-2).map((a) => a.type)).toEqual(['repeat', 'end_repeat'])
    expect(empty.range).toBeNull()
  })

  it('wrapInRepeat with non-contiguous selection wraps first..last', () => {
    const w = wrapInRepeat(list, sel('a', 'd'))
    expect(w.actions.map((x) => x.type)).toEqual(['repeat', 'key', 'key', 'key', 'key', 'end_repeat'])
  })

  it('removeActions drops only the selection', () => {
    expect(uids(removeActions(list, sel('a', 'c')))).toEqual(['b', 'd'])
  })

  it('moveSelected moves blocks and stops at the edges', () => {
    expect(uids(moveSelected(list, sel('b'), -1))).toEqual(['b', 'a', 'c', 'd'])
    expect(uids(moveSelected(list, sel('b', 'c'), 1))).toEqual(['a', 'd', 'b', 'c'])
    expect(uids(moveSelected(list, sel('a'), -1))).toEqual(['a', 'b', 'c', 'd'])
    expect(uids(moveSelected(list, sel('a', 'b'), -1))).toEqual(['a', 'b', 'c', 'd'])
    expect(uids(moveSelected(list, sel('d'), 1))).toEqual(['a', 'b', 'c', 'd'])
  })

  it('canMoveSelected', () => {
    expect(canMoveSelected(list, sel('a'), -1)).toBe(false)
    expect(canMoveSelected(list, sel('a'), 1)).toBe(true)
    expect(canMoveSelected(list, sel('d'), 1)).toBe(false)
    expect(canMoveSelected(list, sel(), 1)).toBe(false)
  })

  it('reorder', () => {
    expect(reorder([1, 2, 3, 4], 0, 2)).toEqual([2, 3, 1, 4])
    expect(reorder([1, 2, 3, 4], 3, 0)).toEqual([4, 1, 2, 3])
    expect(reorder([1, 2], 5, 0)).toEqual([1, 2])
  })

  it('changeActionType coerces the value', () => {
    expect(changeActionType(act('a', 'text', 'hello'), 'key', tEn)).toMatchObject({ type: 'key', value: 'ENTER' })
    expect(changeActionType(act('a', 'text', ''), 'hotkey', tEn).value).toBe('CTRL+S')
    expect(changeActionType(act('a', 'repeat', '4'), 'end_repeat', tEn).value).toBe('')
  })

  it('hotkey parts', () => {
    expect(hotkeyParts('control+shift + a')).toEqual(['CTRL', 'SHIFT', 'A'])
    expect(hotkeyParts('')).toEqual(['CTRL', 'S'])
    expect(joinHotkey(['ctrl', 'c'])).toBe('CTRL+C')
    expect(joinHotkey([])).toBe('CTRL+S')
  })
})

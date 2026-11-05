import { describe, expect, it } from 'vitest'
import { computeDepths, findMatchingEnd, parseRepeatCount, validateRepeats } from '../src/core/repeatBlock'
import { checkHotkey, coerceValueForType, validateAction } from '../src/core/actions'
import { resolveKey, mappedKeyNames, isExtendedKey, VK } from '../src/core/keys'
import { createTranslator, formatTemplate, tableKeys } from '../src/core/loc'
import { extractUserArgs, formatLaunchCommand, getHelpText, isCliMode, isHelpRequest, resolveMacro } from '../src/core/cli'
import { isValidAlias, type MacroItem } from '../src/core/types'
import { createBuiltInSamples } from '../src/core/samples'
import { resolveCliLaunchPath, isRestrictedInstallDirectory } from '../src/core/paths'
import { tEn, tJa } from './helpers'

const a = (type: string, value = '') => ({ type, value })

describe('repeatBlock', () => {
  it('parseRepeatCount accepts 1..9999 integers only', () => {
    expect(parseRepeatCount('3')).toBe(3)
    expect(parseRepeatCount(' 9999 ')).toBe(9999)
    for (const bad of ['0', '10000', '1.5', '', 'abc', null, undefined]) expect(parseRepeatCount(bad)).toBeNull()
  })

  it('findMatchingEnd handles nesting', () => {
    const list = [a('repeat', '2'), a('repeat', '3'), a('key', 'A'), a('end_repeat'), a('end_repeat')]
    expect(findMatchingEnd(list, 0)).toBe(4)
    expect(findMatchingEnd(list, 1)).toBe(3)
    expect(findMatchingEnd(list, 2)).toBe(-1)
    expect(findMatchingEnd([a('repeat', '2')], 0)).toBe(-1)
  })

  it('computeDepths indents bodies and aligns end with start', () => {
    const list = [a('text', 'x'), a('repeat', '2'), a('key', 'A'), a('repeat', '2'), a('key', 'B'), a('end_repeat'), a('end_repeat')]
    expect(computeDepths(list)).toEqual([0, 0, 1, 1, 2, 1, 0])
  })

  it('validateRepeats reports count / orphan / unclosed', () => {
    expect(validateRepeats([a('repeat', '2'), a('end_repeat')])).toEqual({ ok: true })
    expect(validateRepeats([a('repeat', 'x'), a('end_repeat')])).toEqual({ ok: false, code: 'count', step: 1 })
    expect(validateRepeats([a('key', 'A'), a('end_repeat')])).toEqual({ ok: false, code: 'orphan', step: 2 })
    expect(validateRepeats([a('repeat', '2'), a('repeat', '2'), a('end_repeat')])).toEqual({ ok: false, code: 'unclosed', open: 1 })
  })
})

describe('keys', () => {
  it('resolves names case-insensitively, single chars and unknowns', () => {
    expect(resolveKey('enter')).toBe(0x0d)
    expect(resolveKey('F12')).toBe(0x7b)
    expect(resolveKey('a')).toBe(0x41)
    expect(resolveKey('7')).toBe(0x37)
    expect(resolveKey('NUMPAD5')).toBe(0x65)
    expect(resolveKey('OEM_3')).toBe(0xc0)
    expect(resolveKey('nope')).toBe(0)
    expect(resolveKey('')).toBe(0)
    expect(resolveKey(null)).toBe(0)
  })

  it('maps every documented name to a non-zero VK', () => {
    const names = mappedKeyNames()
    expect(names.length).toBeGreaterThan(100)
    for (const n of names) expect(resolveKey(n), n).toBeGreaterThan(0)
  })

  it('marks navigation keys as extended', () => {
    for (const vk of [VK.UP, VK.DELETE, VK.LWIN, VK.DIVIDE]) expect(isExtendedKey(vk)).toBe(true)
    expect(isExtendedKey(VK.RETURN)).toBe(false)
  })
})

describe('hotkey / action validation', () => {
  it('checkHotkey', () => {
    expect(checkHotkey('CTRL+SHIFT+S').ok).toBe(true)
    expect(checkHotkey('')).toMatchObject({ ok: false, error: 'empty' })
    expect(checkHotkey('CTRL+CTRL+A')).toMatchObject({ ok: false, error: 'modifier-duplicate', detail: 'CTRL' })
    expect(checkHotkey('CTRL+control+A')).toMatchObject({ ok: false, error: 'modifier-duplicate' })
    expect(checkHotkey('A+B')).toMatchObject({ ok: false, error: 'main-once' })
    expect(checkHotkey('CTRL+ALT')).toMatchObject({ ok: false, error: 'need-main' })
  })

  it('validateAction messages follow the language', () => {
    expect(validateAction(a('wait', 'abc'), tEn)).toBe(tEn.get('Error_WaitNumeric'))
    expect(validateAction(a('wait', '0.5'), tEn)).toBeNull()
    expect(validateAction(a('repeat', '0'), tJa)).toBe(tJa.format('Error_RepeatCount', 9999))
    expect(validateAction(a('hotkey', 'CTRL'), tEn)).toBe(tEn.get('Error_NeedMainKey'))
    expect(validateAction(a('text', 'anything'), tEn)).toBeNull()
  })

  it('coerceValueForType sets sane defaults', () => {
    expect(coerceValueForType('key', 'zzz', tEn)).toBe('ENTER')
    expect(coerceValueForType('key', 'return', tEn)).toBe('ENTER')
    expect(coerceValueForType('hotkey', '', tEn)).toBe('CTRL+S')
    expect(coerceValueForType('mouse', 'x', tEn)).toBe('LEFT')
    expect(coerceValueForType('dialog', '1.5', tEn)).toBe(tEn.get('Dialog_DefaultMessage'))
    expect(coerceValueForType('repeat', 'x', tEn)).toBe('2')
    expect(coerceValueForType('end_repeat', '5', tEn)).toBe('')
  })
})

describe('loc', () => {
  it('ja and en tables have identical keys and no empty values', () => {
    expect(tableKeys('ja-JP').sort()).toEqual(tableKeys('en-US').sort())
    expect(tableKeys('en-US').length).toBeGreaterThan(250)
  })

  it('resolves language from preference then system locale', () => {
    expect(createTranslator('', 'ja-JP').language).toBe('ja-JP')
    expect(createTranslator('', 'fr-FR').language).toBe('en-US')
    expect(createTranslator('en', 'ja-JP').language).toBe('en-US')
    expect(createTranslator('ja-JP', 'en-US').language).toBe('ja-JP')
  })

  it('format fills {n} and leaves unknown placeholders alone', () => {
    expect(formatTemplate('a {0} b {1} c {2}', ['x', 2])).toBe('a x b 2 c {2}')
    expect(tEn.get('__missing__')).toBe('__missing__')
    expect(tEn.format('Status_Created', 5)).toBe('Created: ID 5')
  })
})

describe('cli', () => {
  const macros: MacroItem[] = [
    { id: 1, name: 'ログイン処理', alias: 'login_ok', delay_sec: 3, actions: [] },
    { id: 2, name: '全選択＆コピー', alias: 'select_copy', delay_sec: 2, actions: [] },
    { id: 3, name: 'No alias', alias: '', delay_sec: 0, actions: [] }
  ]
  const id = (args: string[]) => resolveMacro(args, macros)?.id

  it('detects cli mode and help', () => {
    expect(isCliMode([])).toBe(false)
    expect(isCliMode(['-1'])).toBe(true)
    for (const h of ['-h', '--help', '/?', '-?', 'help', 'HELP']) expect(isHelpRequest([h])).toBe(true)
    expect(isHelpRequest(['-1'])).toBe(false)
    expect(getHelpText(tEn)).toContain('KeyAutomator.exe -alias NAME')
  })

  it('extractUserArgs ignores everything before the app path in dev and Chromium switches', () => {
    expect(extractUserArgs(['electron', '--inspect=0', '--remote-debugging-port=0', '/app', '--no-sandbox'], true)).toEqual([])
    expect(extractUserArgs(['electron', '/app', '-alias', 'login_ok'], true)).toEqual(['-alias', 'login_ok'])
    expect(extractUserArgs(['electron', '/app', '--help'], true)).toEqual(['--help'])
    expect(extractUserArgs(['C:\\KeyAutomator.exe'], false)).toEqual([])
    expect(extractUserArgs(['C:\\KeyAutomator.exe', '-1'], false)).toEqual(['-1'])
    expect(extractUserArgs(['C:\\KeyAutomator.exe', '--allow-file-access-from-files', '-name', 'x y'], false)).toEqual(['-name', 'x y'])
    expect(extractUserArgs(['electron'], true)).toEqual([])
  })

  it('resolves every documented form', () => {
    expect(id(['-id', '2'])).toBe(2)
    expect(id(['-ID', '2'])).toBe(2)
    expect(id(['-alias', 'select_copy'])).toBe(2)
    expect(id(['-a', 'login_ok'])).toBe(1)
    expect(id(['-name', '全選択＆コピー'])).toBe(2)
    expect(id(['-name', 'no ALIAS'])).toBe(3)
    expect(id(['-1'])).toBe(1)
    expect(id(['-', '1'])).toBe(1)
    expect(id(['-login_ok'])).toBe(1)
    expect(id(['login_ok'])).toBe(1)
  })

  it('returns undefined for unknown targets', () => {
    expect(id(['-id', '99'])).toBeUndefined()
    expect(id(['-alias', 'missing'])).toBeUndefined()
    expect(id(['-name', '存在しない'])).toBeUndefined()
    expect(id(['no_such'])).toBeUndefined()
    expect(id([])).toBeUndefined()
    expect(id(['-9'])).toBeUndefined()
  })

  it('formatLaunchCommand prefers alias, then id, then bare path', () => {
    const p = 'C:\\Apps\\KeyAutomator.exe'
    expect(formatLaunchCommand(p, 'login_ok', 1)).toBe('"C:\\Apps\\KeyAutomator.exe" -alias login_ok')
    expect(formatLaunchCommand(p, '  ', 7)).toBe('"C:\\Apps\\KeyAutomator.exe" -id 7')
    expect(formatLaunchCommand(p)).toBe('"C:\\Apps\\KeyAutomator.exe"')
    expect(() => formatLaunchCommand('C:\\a"b.exe')).toThrow()
  })
})

describe('alias / samples / paths', () => {
  it('isValidAlias', () => {
    expect(isValidAlias('')).toBe(true)
    expect(isValidAlias('abc_123')).toBe(true)
    expect(isValidAlias('日本語')).toBe(false)
    expect(isValidAlias('a b')).toBe(false)
  })

  it('built-in samples follow the UI language', () => {
    const ja = createBuiltInSamples(tJa)
    expect(ja.map((m) => m.id)).toEqual([1, 2, 3])
    expect(ja[0].name).toBe(tJa.get('Sample_LoginName'))
    expect(ja[0].actions.find((x) => x.type === 'dialog')?.value).toBe(tJa.get('Sample_LoginDialog'))
    expect(createBuiltInSamples(tEn)[2].name).toBe('Enter 3 times')
  })

  it('cli launch path prefers the alias only when packaged and present', () => {
    const base = { processPath: 'C:\\x\\app.exe', aliasPath: 'C:\\alias\\KeyAutomator.exe', fallbackDir: 'C:\\fb' }
    expect(resolveCliLaunchPath({ ...base, packaged: true, fileExists: () => true })).toBe(base.aliasPath)
    expect(resolveCliLaunchPath({ ...base, packaged: true, fileExists: () => false })).toBe(base.processPath)
    expect(resolveCliLaunchPath({ ...base, packaged: false, fileExists: () => true })).toBe(base.processPath)
  })

  it('restricted install dirs', () => {
    const roots = ['C:\\Program Files', 'C:\\Windows']
    expect(isRestrictedInstallDirectory('', roots)).toBe(true)
    expect(isRestrictedInstallDirectory('/x/WindowsApps/KeyAutomator_1.0', roots)).toBe(true)
  })
})

import { describe, expect, it } from 'vitest'
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { loadConfig, saveConfig, loadSettings, saveSettings, createErrorLogger, backupIfExists, nextId, loadSampleMacros } from '../src/core/store'
import { join } from 'node:path'
import { tEn, tJa, tempPaths } from './helpers'
import type { MacroItem } from '../src/core/types'

const ctx = (paths = tempPaths(), t = tEn) => ({ paths, t, sampleDirs: [] as string[] })

describe('config store', () => {
  it('first run writes samples into config.json', () => {
    const c = ctx()
    const list = loadConfig(c)
    expect(list.map((m) => m.alias)).toEqual(['login_ok', 'select_copy', 'enter_x3'])
    expect(existsSync(c.paths.configPath)).toBe(true)
    expect(loadConfig(c)).toEqual(list)
  })

  it('round-trips with snake_case keys compatible with the C# app', () => {
    const c = ctx()
    const macros: MacroItem[] = [{ id: 7, name: '日本語 & "quote"', alias: 'x', delay_sec: 1.5, actions: [{ type: 'text', value: '😀\n' }] }]
    saveConfig(c.paths, macros, tEn)
    const raw = JSON.parse(readFileSync(c.paths.configPath, 'utf8'))
    expect(raw[0]).toMatchObject({ id: 7, delay_sec: 1.5, actions: [{ type: 'text', value: '😀\n' }] })
    expect(loadConfig(c)).toEqual(macros)
  })

  it('reads a UTF-8 BOM file and tolerates missing fields', () => {
    const c = ctx()
    writeFileSync(c.paths.configPath, '\uFEFF[{"id":2,"name":"n","actions":[{"value":"v"}]}]')
    expect(loadConfig(c)).toEqual([{ id: 2, name: 'n', alias: '', delay_sec: 0, actions: [{ type: 'text', value: 'v' }] }])
  })

  it('empty file → [], corrupt file → throws localized error and is not overwritten', () => {
    const c = ctx()
    writeFileSync(c.paths.configPath, '   ')
    expect(loadConfig(c)).toEqual([])
    writeFileSync(c.paths.configPath, '{broken')
    expect(() => loadConfig(c)).toThrow(tEn.get('Ex_ConfigUnreadable'))
    expect(readFileSync(c.paths.configPath, 'utf8')).toBe('{broken')
    writeFileSync(c.paths.configPath, '{"not":"array"}')
    expect(() => loadConfig(c)).toThrow()
  })

  it('leaves no temp files behind after saving', () => {
    const c = ctx()
    saveConfig(c.paths, [], tEn)
    saveConfig(c.paths, [], tEn)
    expect(readdirSync(c.paths.dataDir).filter((f) => f.endsWith('.tmp'))).toEqual([])
  })

  it('backupIfExists copies with a timestamp', () => {
    const c = ctx()
    expect(backupIfExists(c.paths.configPath)).toBe('')
    writeFileSync(c.paths.configPath, '[]')
    const b = backupIfExists(c.paths.configPath, new Date(2026, 0, 2, 3, 4, 5))
    expect(b.endsWith('.bak.20260102030405')).toBe(true)
    expect(readFileSync(b, 'utf8')).toBe('[]')
  })

  it('nextId', () => {
    expect(nextId([])).toBe(1)
    expect(nextId([{ id: 3 } as MacroItem, { id: 9 } as MacroItem])).toBe(10)
  })

  it('sample files next to the exe win over built-ins and get localized', () => {
    const paths = tempPaths()
    const dir = tempPaths().dataDir
    writeFileSync(join(dir, 'config.sample.json'), JSON.stringify([{ id: 1, name: 'x', alias: 'login_ok', delay_sec: 1, actions: [] }]))
    const list = loadSampleMacros({ paths, t: tJa, sampleDirs: [dir] })
    expect(list).toHaveLength(1)
    expect(list[0].name).toBe(tJa.get('Sample_LoginName'))
  })
})

describe('settings store', () => {
  it('creates defaults on first load and persists changes', () => {
    const paths = tempPaths()
    expect(loadSettings(paths)).toEqual({ confirm_before_delete: true, action_delay_sec: 0.2, ui_language: '' })
    saveSettings(paths, { confirm_before_delete: false, action_delay_sec: 0.5, ui_language: 'ja-JP' })
    expect(loadSettings(paths)).toEqual({ confirm_before_delete: false, action_delay_sec: 0.5, ui_language: 'ja-JP' })
  })

  it('falls back to defaults on corrupt json and reports the error', () => {
    const paths = tempPaths()
    writeFileSync(paths.settingsPath, 'oops')
    let err: unknown
    expect(loadSettings(paths, (e) => (err = e)).action_delay_sec).toBe(0.2)
    expect(err).toBeDefined()
  })

  it('rejects out-of-type values field by field', () => {
    const paths = tempPaths()
    writeFileSync(paths.settingsPath, JSON.stringify({ confirm_before_delete: 'yes', action_delay_sec: -1, ui_language: 5 }))
    expect(loadSettings(paths)).toEqual({ confirm_before_delete: true, action_delay_sec: 0.2, ui_language: '' })
  })
})

describe('error logger', () => {
  it('appends timestamped entries and records the path', () => {
    const paths = tempPaths()
    const log = createErrorLogger(paths, tEn)
    expect(log.lastWrittenPath).toBeNull()
    log.write('first')
    log.writeError(new Error('boom'), 'ctx')
    const text = readFileSync(paths.errorLogPath, 'utf8')
    expect(text).toMatch(/^\[\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\] first\n/)
    expect(text).toContain('ctx')
    expect(text).toContain('boom')
    expect(log.lastWrittenPath).toBe(paths.errorLogPath)
  })
})

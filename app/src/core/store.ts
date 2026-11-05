import { appendFileSync, copyFileSync, existsSync, mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { tmpdir } from 'node:os'
import type { Translator } from './loc'
import type { AppPaths } from './paths'
import { applySampleLanguage, createBuiltInSamples } from './samples'
import { defaultSettings, type AppSettings, type MacroItem } from './types'

/** 一時ファイル経由で置換し、途中クラッシュで JSON が壊れにくくする。 */
export const writeFileAtomic = (path: string, contents: string): void => {
  mkdirSync(dirname(path), { recursive: true })
  const tmp = `${path}.${Date.now().toString(16)}${Math.random().toString(16).slice(2)}.tmp`
  try {
    writeFileSync(tmp, contents, 'utf8')
    try {
      renameSync(tmp, path)
    } catch {
      copyFileSync(tmp, path)
    }
  } finally {
    try {
      if (existsSync(tmp)) rmSync(tmp)
    } catch {
      // ignore
    }
  }
}

const pad = (n: number, w = 2): string => String(n).padStart(w, '0')
const stamp = (d: Date): string =>
  `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}${pad(d.getHours())}${pad(d.getMinutes())}${pad(d.getSeconds())}`

export const backupIfExists = (path: string, now = new Date()): string => {
  if (!existsSync(path)) return ''
  const backup = `${path}.bak.${stamp(now)}`
  copyFileSync(path, backup)
  return backup
}

// ------------------------------------------------------------------ config

const normalizeMacro = (raw: unknown): MacroItem => {
  const o = (raw ?? {}) as Record<string, unknown>
  const actions = Array.isArray(o.actions) ? o.actions : []
  return {
    id: typeof o.id === 'number' ? o.id : Number(o.id) || 0,
    name: typeof o.name === 'string' ? o.name : '',
    alias: typeof o.alias === 'string' ? o.alias : '',
    delay_sec: typeof o.delay_sec === 'number' ? o.delay_sec : Number(o.delay_sec) || 0,
    actions: actions.map((a) => {
      const x = (a ?? {}) as Record<string, unknown>
      return {
        type: typeof x.type === 'string' && x.type !== '' ? x.type : 'text',
        value: typeof x.value === 'string' ? x.value : x.value == null ? '' : String(x.value)
      }
    })
  }
}

const parseMacros = (json: string): MacroItem[] => {
  const data: unknown = JSON.parse(json.replace(/^﻿/, ''))
  if (!Array.isArray(data)) throw new Error('config.json is not an array')
  return data.map(normalizeMacro)
}

export interface ConfigContext {
  paths: AppPaths
  t: Translator
  /** サンプル JSON を探す追加ディレクトリ（exe 横など）。 */
  sampleDirs: readonly string[]
}

/** exe 横 / データフォルダの config.sample(.en).json。無ければ内蔵サンプル。 */
export const loadSampleMacros = (ctx: ConfigContext): MacroItem[] => {
  const names =
    ctx.t.language === 'en-US'
      ? ['config.sample.en.json', 'config.sample.json']
      : ['config.sample.json', 'config.sample.en.json']
  const dirs = [...ctx.sampleDirs, ctx.paths.dataDir]
  for (const name of names) {
    for (const dir of dirs) {
      const path = join(dir, name)
      if (!existsSync(path)) continue
      try {
        const list = parseMacros(readFileSync(path, 'utf8'))
        if (list.length > 0) {
          applySampleLanguage(list, ctx.t)
          return list
        }
      } catch {
        // 次の候補へ
      }
    }
  }
  return createBuiltInSamples(ctx.t)
}

export const saveConfig = (paths: AppPaths, macros: readonly MacroItem[], t: Translator): void => {
  writeFileAtomic(paths.configPath, JSON.stringify(macros, null, 2))
  // 書き込み後に読み返して失敗を検知する
  if (!existsSync(paths.configPath)) throw new Error(t.format('Ex_ConfigNotCreated', paths.configPath))
  if (readFileSync(paths.configPath, 'utf8').trim() === '') {
    throw new Error(t.format('Ex_ConfigEmptyAfterSave', paths.configPath))
  }
}

export const loadConfig = (ctx: ConfigContext): MacroItem[] => {
  const { paths, t } = ctx
  if (!existsSync(paths.configPath)) {
    // 初回: 空ではなくサンプルを入れて「壊れてる？」を防ぐ
    const samples = loadSampleMacros(ctx)
    saveConfig(paths, samples, t)
    return samples
  }
  const json = readFileSync(paths.configPath, 'utf8')
  if (json.trim() === '') return []
  try {
    return parseMacros(json)
  } catch {
    throw new Error(t.get('Ex_ConfigUnreadable'))
  }
}

export { findByAlias, findById, findByName, nextId } from './macros'

// ---------------------------------------------------------------- settings

export const loadSettings = (paths: AppPaths, onError?: (e: unknown) => void): AppSettings => {
  try {
    if (!existsSync(paths.settingsPath)) {
      const defaults = defaultSettings()
      saveSettings(paths, defaults)
      return defaults
    }
    const raw = JSON.parse(readFileSync(paths.settingsPath, 'utf8').replace(/^﻿/, '')) as Partial<AppSettings>
    const d = defaultSettings()
    return {
      confirm_before_delete: typeof raw.confirm_before_delete === 'boolean' ? raw.confirm_before_delete : d.confirm_before_delete,
      action_delay_sec: typeof raw.action_delay_sec === 'number' && raw.action_delay_sec >= 0 ? raw.action_delay_sec : d.action_delay_sec,
      ui_language: typeof raw.ui_language === 'string' ? raw.ui_language : d.ui_language
    }
  } catch (e) {
    onError?.(e)
    return defaultSettings()
  }
}

export const saveSettings = (paths: AppPaths, settings: AppSettings, onError?: (e: unknown) => void): void => {
  try {
    writeFileAtomic(paths.settingsPath, JSON.stringify(settings, null, 2))
  } catch (e) {
    onError?.(e)
  }
}

// --------------------------------------------------------------- error log

export interface ErrorLogger {
  /** 直近に書き込めたログファイルのフルパス。書けなければ null。 */
  readonly lastWrittenPath: string | null
  write(message: string): void
  writeError(error: unknown, context?: string): void
}

const two = (n: number): string => pad(n)
const logStamp = (d: Date): string =>
  `${d.getFullYear()}-${two(d.getMonth() + 1)}-${two(d.getDate())} ${two(d.getHours())}:${two(d.getMinutes())}:${two(d.getSeconds())}`

export const createErrorLogger = (paths: AppPaths, t: Translator): ErrorLogger => {
  let last: string | null = null
  const writeRaw = (text: string): void => {
    // 主経路（データフォルダ）→ だめなら Temp
    const candidates = [paths.errorLogPath, join(tmpdir(), 'KeyAutomator-error.log')]
    for (const path of new Set(candidates)) {
      try {
        mkdirSync(dirname(path), { recursive: true })
        appendFileSync(path, text, 'utf8')
        last = path
        return
      } catch {
        // try next
      }
    }
  }
  return {
    get lastWrittenPath() {
      return last
    },
    write: (message) => writeRaw(`[${logStamp(new Date())}] ${message}\n`),
    writeError: (error, context) => {
      const detail = error instanceof Error ? (error.stack ?? error.message) : String(error)
      writeRaw(`[${logStamp(new Date())}] ${context ?? t.get('Log_Error')}\n${detail}\n${'-'.repeat(60)}\n`)
    }
  }
}

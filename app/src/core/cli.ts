import type { Translator } from './loc'
import { isValidAlias, type MacroItem } from './types'
import { quoteCliPath } from './paths'
import { findByAlias, findById, findByName } from './macros'

export const isCliMode = (args: readonly string[]): boolean => args.length > 0

/**
 * process.argv からユーザーが渡した引数だけを取り出す。
 * - 開発実行（electron <app> …）: 最初の「-」で始まらない引数がアプリのパス。それより後ろだけを見る
 *   （--inspect などアプリ前に付くスイッチを無視するため）
 * - 配布版: exe の次から
 * Chromium が付ける `--xxx` スイッチは除く（`--help` は残す）。
 */
export const extractUserArgs = (argv: readonly string[], defaultApp: boolean): string[] => {
  let rest: readonly string[]
  if (defaultApp) {
    const appIndex = argv.findIndex((a, i) => i >= 1 && !a.startsWith('-'))
    rest = appIndex < 0 ? [] : argv.slice(appIndex + 1)
  } else {
    rest = argv.slice(1)
  }
  return rest.filter((a) => !a.startsWith('--') || a.toLowerCase() === '--help')
}

const HELP_FLAGS = new Set(['-h', '--help', '/?', '-?', 'help'])

export const isHelpRequest = (args: readonly string[]): boolean =>
  args.some((a) => HELP_FLAGS.has(a.toLowerCase()))

export const getHelpText = (t: Translator): string => t.get('Cli_Help').replace(/\r\n/g, '\n').trim()

/**
 * 起動パスに引数を足した、貼り付け用のコマンド。
 * 引数名があれば -alias、マクロ ID があれば -id、どちらも無ければパスのみ。
 */
export const formatLaunchCommand = (exePath: string, alias?: string | null, id?: number | null): string => {
  const quoted = quoteCliPath(exePath)
  const trimmed = alias?.trim()
  if (trimmed) return `${quoted} -alias ${trimmed}`
  if (id != null && id >= 1) return `${quoted} -id ${id}`
  return quoted
}

const indexOfFlag = (args: readonly string[], flags: readonly string[]): number =>
  args.findIndex((a, i) => i + 1 < args.length && flags.includes(a.toLowerCase()))

/** 数値だけの文字列か（C# の int.TryParse 相当。符号と前後空白を許す）。 */
const tryParseInt = (s: string): number | null => (/^\s*[+-]?\d+\s*$/.test(s) ? Number.parseInt(s, 10) : null)

const resolveShortForm = (macros: readonly MacroItem[], args: readonly string[]): { hit: boolean; macro?: MacroItem } => {
  if (args.length === 0) return { hit: false }
  const first = args[0]
  let body: string | null = null
  if (first.startsWith('-') && first.length > 1) body = first.slice(1)
  else if (first === '-' && args.length >= 2) body = args[1]
  if (body == null || body.trim() === '') return { hit: false }

  const id = tryParseInt(body)
  if (id !== null) return { hit: true, macro: findById(macros, id) }

  if (isValidAlias(body)) {
    const macro = findByAlias(macros, body)
    return macro ? { hit: true, macro } : { hit: false }
  }
  return { hit: false }
}

/** 読み込み済みマクロ一覧から CLI 引数で対象を解決する（ディスク I/O なし）。 */
export const resolveMacro = (args: readonly string[], macros: readonly MacroItem[]): MacroItem | undefined => {
  if (args.length === 0) return undefined

  for (let i = 0; i < args.length; i++) {
    if (args[i].toLowerCase() === '-id' && i + 1 < args.length) {
      const id = tryParseInt(args[i + 1])
      if (id !== null) return findById(macros, id)
    }
  }

  const aliasAt = indexOfFlag(args, ['-alias', '-a'])
  if (aliasAt >= 0) return findByAlias(macros, args[aliasAt + 1])

  const nameAt = indexOfFlag(args, ['-name'])
  if (nameAt >= 0) return findByName(macros, args[nameAt + 1])

  const short = resolveShortForm(macros, args)
  if (short.hit) return short.macro

  const first = args[0]
  if (!first.startsWith('-')) {
    const byAlias = findByAlias(macros, first)
    if (byAlias) return byAlias
  }
  return undefined
}

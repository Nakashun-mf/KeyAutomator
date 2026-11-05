import jaJP from './i18n/ja-JP.json'
import enUS from './i18n/en-US.json'

export const LANG_EN = 'en-US'
export const LANG_JA = 'ja-JP'
/** 空なら OS の表示言語に従う。 */
export const LANG_SYSTEM = ''

export type Language = typeof LANG_EN | typeof LANG_JA
export type Preference = Language | typeof LANG_SYSTEM

type Table = Record<string, string>

const TABLES: Record<Language, Table> = { 'ja-JP': jaJP, 'en-US': enUS }

export const normalizePreference = (preference: string | null | undefined): Preference => {
  const p = (preference ?? '').trim().toLowerCase()
  if (p.startsWith('ja')) return LANG_JA
  if (p.startsWith('en')) return LANG_EN
  return LANG_SYSTEM
}

export const resolveLanguage = (preference: Preference, systemLocale: string): Language => {
  if (preference === LANG_JA) return LANG_JA
  if (preference === LANG_EN) return LANG_EN
  return systemLocale.toLowerCase().startsWith('ja') ? LANG_JA : LANG_EN
}

/** `{0}` 形式のプレースホルダーを args で置き換える。 */
export const formatTemplate = (template: string, args: readonly (string | number)[]): string =>
  template.replace(/\{(\d+)\}/g, (whole, i: string) => {
    const v = args[Number(i)]
    return v === undefined ? whole : String(v)
  })

export interface Translator {
  readonly language: Language
  readonly preference: Preference
  /** リソース識別子に対応する文字列。無ければ識別子そのもの。 */
  get(name: string): string
  format(name: string, ...args: (string | number)[]): string
}

export const createTranslator = (
  preference: string | null | undefined,
  systemLocale: string
): Translator => {
  const pref = normalizePreference(preference)
  const language = resolveLanguage(pref, systemLocale)
  const table = TABLES[language]
  const get = (name: string): string => {
    const v = table[name]
    return v !== undefined && v.length > 0 ? v : name
  }
  return {
    language,
    preference: pref,
    get,
    format: (name, ...args) => formatTemplate(get(name), args)
  }
}

/** 言語テーブルのキー集合（テスト・整合性チェック用）。 */
export const tableKeys = (language: Language): string[] => Object.keys(TABLES[language])

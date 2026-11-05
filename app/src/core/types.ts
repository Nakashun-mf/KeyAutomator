/** config.json の 1 手順。type/value は SPEC.md §4.2 のとおり。 */
export interface ActionItem {
  type: string
  value: string
}

/** config.json の 1 マクロ。キー名は既存の config.json と互換（snake_case）。 */
export interface MacroItem {
  id: number
  name: string
  /** CLI 引数用（英数字と _ のみ）。空なら未設定。 */
  alias: string
  delay_sec: number
  actions: ActionItem[]
}

/** settings.json。キー名は既存と互換。 */
export interface AppSettings {
  confirm_before_delete: boolean
  /** 各アクション実行後に挟む待機秒数（最後の手順の後は除く）。 */
  action_delay_sec: number
  /** 空なら OS の表示言語。`ja-JP` / `en-US`。 */
  ui_language: string
}

export const DEFAULT_ACTION_DELAY_SEC = 0.2

export const defaultSettings = (): AppSettings => ({
  confirm_before_delete: true,
  action_delay_sec: DEFAULT_ACTION_DELAY_SEC,
  ui_language: ''
})

export const cloneMacro = (m: MacroItem): MacroItem => ({
  id: m.id,
  name: m.name,
  alias: m.alias,
  delay_sec: m.delay_sec,
  actions: m.actions.map((a) => ({ type: a.type, value: a.value }))
})

const ALIAS_PATTERN = /^[A-Za-z0-9_]+$/

/** alias が空、または英数字と _ のみなら true。 */
export const isValidAlias = (alias: string | null | undefined): boolean => {
  if (alias == null || alias.trim() === '') return true
  return ALIAS_PATTERN.test(alias.trim())
}

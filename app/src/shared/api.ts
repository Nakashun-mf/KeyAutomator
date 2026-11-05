import type { AppSettings, MacroItem } from '../core/types'

export interface LoadError {
  message: string
  /** 壊れた設定ファイルのバックアップ名（作れなかったら空）。 */
  backup: string
  logPath: string | null
}

export interface AppState {
  macros: MacroItem[]
  settings: AppSettings
  systemLocale: string
  dataDir: string
  configPath: string
  /** exe 横ではなくユーザーフォルダを使っているか。 */
  usingFallbackDir: boolean
  loadError: LoadError | null
  version: string
}

export type SaveResult = { ok: true } | { ok: false; error: string; logPath: string | null }

export type RunResult =
  | { status: 'done' }
  | { status: 'stopped' }
  | { status: 'error'; message: string; logPath: string | null }

export type SamplesResult = { ok: true; macros: MacroItem[] } | { ok: false; error: string; logPath: string | null }

/** preload が renderer に公開する API。 */
export interface KeyAutomatorApi {
  getState(): Promise<AppState>
  saveMacros(macros: MacroItem[]): Promise<SaveResult>
  /** サンプルで置き換えて保存する。確認は呼び出し側。 */
  loadSamples(): Promise<SamplesResult>
  saveSettings(settings: AppSettings): Promise<void>
  /** 言語変更をメイン側（ログ・ダイアログ文言）へ伝える。 */
  setLanguage(preference: string): Promise<void>
  runMacro(macro: MacroItem, actionDelaySec: number): Promise<RunResult>
  stopRun(): Promise<void>
  openDataFolder(): Promise<{ ok: true; path: string } | { ok: false; path: string }>
  copyLaunchCommand(alias: string, id: number): Promise<{ ok: true; text: string } | { ok: false }>
  /** 初回描画が済んだことを伝える（スプラッシュを閉じてメインを表示）。 */
  notifyReady(): void
}

export const IPC = {
  getState: 'ka:get-state',
  saveMacros: 'ka:save-macros',
  loadSamples: 'ka:load-samples',
  saveSettings: 'ka:save-settings',
  setLanguage: 'ka:set-language',
  runMacro: 'ka:run-macro',
  stopRun: 'ka:stop-run',
  openDataFolder: 'ka:open-data-folder',
  copyLaunchCommand: 'ka:copy-launch-command',
  ready: 'ka:ready',
  dialogClose: 'ka:dialog-close'
} as const

import { BrowserWindow, clipboard, ipcMain, shell, app } from 'electron'
import { mkdirSync } from 'node:fs'
import { formatLaunchCommand } from '../core/cli'
import { CancelledError } from '../core/runner'
import { backupIfExists, loadConfig, loadSampleMacros, loadSettings, saveConfig, saveSettings } from '../core/store'
import type { AppSettings, MacroItem } from '../core/types'
import { IPC, type AppState, type LoadError, type RunResult, type SamplesResult, type SaveResult } from '../shared/api'
import { createRealRunner } from './runtime'
import type { Services } from './services'

export interface IpcHooks {
  onReady(): void
  mainWindow(): BrowserWindow | null
}

const message = (e: unknown): string => (e instanceof Error ? e.message : String(e))

export const registerIpc = (services: Services, hooks: IpcHooks): void => {
  const { t, logger, paths } = services
  let running: AbortController | null = null

  const readState = (): AppState => {
    let macros: MacroItem[] = []
    let loadError: LoadError | null = null
    try {
      macros = loadConfig(services.config())
    } catch (e) {
      logger.writeError(e, t.get('Log_GuiLoad'))
      let backup = ''
      try {
        backup = backupIfExists(paths.configPath)
      } catch {
        // バックアップできなくても続行
      }
      loadError = { message: message(e), backup, logPath: logger.lastWrittenPath }
    }
    const settings = loadSettings(paths, (e) => logger.writeError(e, t.get('Log_SettingsLoadFailed')))
    return {
      macros,
      settings,
      systemLocale: services.systemLocale,
      dataDir: paths.dataDir,
      configPath: paths.configPath,
      usingFallbackDir: paths.dataDir.toLowerCase() !== services.exeDir.toLowerCase(),
      loadError,
      version: app.getVersion()
    }
  }

  ipcMain.handle(IPC.getState, (): AppState => readState())

  ipcMain.handle(IPC.saveMacros, (_e, macros: MacroItem[]): SaveResult => {
    try {
      saveConfig(paths, macros, t)
      return { ok: true }
    } catch (e) {
      logger.writeError(e, t.get('Log_SaveFailed'))
      return { ok: false, error: message(e), logPath: logger.lastWrittenPath }
    }
  })

  ipcMain.handle(IPC.loadSamples, (): SamplesResult => {
    const samples = loadSampleMacros(services.config())
    try {
      saveConfig(paths, samples, t)
      return { ok: true, macros: samples }
    } catch (e) {
      logger.writeError(e, t.get('Log_SaveFailed'))
      return { ok: false, error: message(e), logPath: logger.lastWrittenPath }
    }
  })

  ipcMain.handle(IPC.saveSettings, (_e, settings: AppSettings): void => {
    saveSettings(paths, settings, (err) => logger.writeError(err, t.get('Log_SettingsSaveFailed')))
  })

  ipcMain.handle(IPC.setLanguage, (_e, preference: string): void => services.setLanguage(preference))

  ipcMain.handle(IPC.runMacro, async (_e, macro: MacroItem, actionDelaySec: number): Promise<RunResult> => {
    if (running) return { status: 'error', message: 'already running', logPath: null }
    const ac = new AbortController()
    running = ac
    try {
      const runner = await createRealRunner(services, ac.signal)
      await runner.executeMacro(macro, actionDelaySec, ac.signal)
      return ac.signal.aborted ? { status: 'stopped' } : { status: 'done' }
    } catch (e) {
      if (e instanceof CancelledError) return { status: 'stopped' }
      logger.writeError(e, t.get('Log_TestRun'))
      return { status: 'error', message: message(e), logPath: logger.lastWrittenPath }
    } finally {
      running = null
      const win = hooks.mainWindow()
      if (win && !win.isDestroyed()) {
        if (win.isMinimized()) win.restore()
        win.show()
        win.focus()
      }
    }
  })

  ipcMain.handle(IPC.stopRun, (): void => running?.abort())

  ipcMain.handle(IPC.openDataFolder, async () => {
    try {
      mkdirSync(paths.dataDir, { recursive: true })
      const err = await shell.openPath(paths.dataDir)
      if (err) throw new Error(err)
      return { ok: true as const, path: paths.dataDir }
    } catch (e) {
      logger.writeError(e, t.get('Log_OpenFolder'))
      return { ok: false as const, path: paths.dataDir }
    }
  })

  ipcMain.handle(IPC.copyLaunchCommand, (_e, alias: string, id: number) => {
    try {
      const path = services.cliLaunchPath()
      const text = formatLaunchCommand(path, alias, id)
      clipboard.writeText(text)
      return { ok: true as const, text }
    } catch (e) {
      logger.writeError(e, t.get('Log_CopyLaunchPath'))
      return { ok: false as const }
    }
  })

  ipcMain.on(IPC.ready, () => hooks.onReady())

}

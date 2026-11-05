import { app } from 'electron'
import { existsSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { createTranslator, type Translator } from '../core/loc'
import { createPaths, resolveCliLaunchPath, resolveDataDirectory, type AppPaths } from '../core/paths'
import { createErrorLogger, loadSettings, type ErrorLogger } from '../core/store'
import type { ConfigContext } from '../core/store'

export interface Services {
  paths: AppPaths
  exeDir: string
  packaged: boolean
  /** 言語変更に追従する翻訳（参照は固定）。 */
  t: Translator
  systemLocale: string
  logger: ErrorLogger
  config(): ConfigContext
  setLanguage(preference: string): void
  cliLaunchPath(): string
}

const env = (name: string): string => process.env[name] ?? ''

/** MSIX（Microsoft Store）として動作しているか。Electron が process.windowsStore を立てる。 */
const isPackaged = (): boolean => (process as NodeJS.Process & { windowsStore?: boolean }).windowsStore === true

const restrictedRoots = (): string[] => {
  const sys = env('SystemRoot') || env('windir')
  return [
    env('ProgramFiles'),
    env('ProgramFiles(x86)'),
    env('ProgramW6432'),
    sys,
    sys ? join(sys, 'System32') : ''
  ].filter((p) => p !== '')
}

/** app.whenReady() の後に呼ぶ。 */
export const createServices = (): Services => {
  const portableExe = env('PORTABLE_EXECUTABLE_FILE')
  const exeDir = env('PORTABLE_EXECUTABLE_DIR') || dirname(process.execPath)
  const packaged = isPackaged()
  const localDataDir = join(env('LOCALAPPDATA') || app.getPath('appData'), 'KeyAutomator')

  // 開発実行（electron . / electron-vite dev）では electron 本体のフォルダへ書かない
  const dataDir = app.isPackaged
    ? resolveDataDirectory({ exeDir, localDataDir, packaged, restrictedRoots: restrictedRoots() })
    : join(app.getAppPath(), '.devdata')

  const paths = createPaths(dataDir)
  const systemLocale = app.getPreferredSystemLanguages()[0] ?? app.getLocale()
  const settings = loadSettings(paths)

  // 言語変更で中身を差し替えても、参照側は同じ t を使い続けられるようにする
  let current = createTranslator(settings.ui_language, systemLocale)
  const t: Translator = {
    get language() {
      return current.language
    },
    get preference() {
      return current.preference
    },
    get: (name) => current.get(name),
    format: (name, ...args) => current.format(name, ...args)
  }

  const services: Services = {
    paths,
    exeDir,
    packaged,
    t,
    systemLocale,
    logger: createErrorLogger(paths, t),
    config: () => ({ paths, t, sampleDirs: [exeDir] }),
    setLanguage: (preference) => {
      current = createTranslator(preference, systemLocale)
    },
    cliLaunchPath: () =>
      resolveCliLaunchPath({
        packaged,
        processPath: portableExe || process.execPath,
        aliasPath: join(env('LOCALAPPDATA'), 'Microsoft', 'WindowsApps', 'KeyAutomator.exe'),
        fileExists: existsSync,
        fallbackDir: exeDir
      })
  }
  return services
}

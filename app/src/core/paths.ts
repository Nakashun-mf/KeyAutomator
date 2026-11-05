import { mkdirSync, rmSync, writeFileSync, copyFileSync, existsSync } from 'node:fs'
import { join, resolve, sep } from 'node:path'

/**
 * 設定・ログの保存先。
 * - パッケージ実行（MSIX）／Program Files 配下など: %LocalAppData%\KeyAutomator
 * - ポータブル（書き込み可能な場所に exe を置いた場合）: exe と同じフォルダ
 * - 書けない場所: %LocalAppData%\KeyAutomator
 */
export interface AppPaths {
  dataDir: string
  configPath: string
  settingsPath: string
  errorLogPath: string
}

export const createPaths = (dataDir: string): AppPaths => ({
  dataDir,
  configPath: join(dataDir, 'config.json'),
  settingsPath: join(dataDir, 'settings.json'),
  errorLogPath: join(dataDir, 'error.log')
})

export const CLI_EXECUTABLE_FILE_NAME = 'KeyAutomator.exe'

export const canWriteToDirectory = (dir: string): boolean => {
  try {
    mkdirSync(dir, { recursive: true })
    const probe = join(dir, `.write_probe_${Date.now()}_${Math.random().toString(16).slice(2)}`)
    writeFileSync(probe, 'ok')
    rmSync(probe)
    return true
  } catch {
    return false
  }
}

const trimSep = (p: string): string => p.replace(/[\\/]+$/, '')

/** インストール先・保護フォルダなど、ユーザーデータ向きでないパスか。 */
export const isRestrictedInstallDirectory = (
  directory: string | null | undefined,
  restrictedRoots: readonly string[]
): boolean => {
  if (directory == null || directory.trim() === '') return true
  let full: string
  try {
    full = resolve(directory)
  } catch {
    return true
  }
  const lower = trimSep(full).toLowerCase()
  for (const root of restrictedRoots) {
    if (!root || root.trim() === '') continue
    const r = trimSep(root).toLowerCase()
    if (lower === r || lower.startsWith(r + sep) || lower.startsWith(r + '\\') || lower.startsWith(r + '/')) {
      return true
    }
  }
  // WindowsApps（ストア／MSIX の実体置き場）
  return /[\\/]windowsapps[\\/]/i.test(full + sep)
}

export interface ResolveDataDirInput {
  exeDir: string
  localDataDir: string
  packaged: boolean
  restrictedRoots: readonly string[]
}

/** exe 横の設定を、書き込み可能なフォルダへ一度だけコピーする。 */
export const migrateSidecarFiles = (source: string, destination: string): void => {
  if (!source || !destination || resolve(source).toLowerCase() === resolve(destination).toLowerCase()) return
  for (const name of ['config.json', 'settings.json']) {
    const src = join(source, name)
    const dst = join(destination, name)
    if (!existsSync(src) || existsSync(dst)) continue
    try {
      copyFileSync(src, dst)
    } catch {
      // 読めない／コピー失敗でも起動は続行（空の設定で始める）
    }
  }
}

export const resolveDataDirectory = (input: ResolveDataDirInput): string => {
  const { exeDir, localDataDir, packaged, restrictedRoots } = input
  if (packaged || isRestrictedInstallDirectory(exeDir, restrictedRoots)) {
    mkdirSync(localDataDir, { recursive: true })
    migrateSidecarFiles(exeDir, localDataDir)
    return localDataDir
  }
  if (canWriteToDirectory(exeDir)) return exeDir
  mkdirSync(localDataDir, { recursive: true })
  migrateSidecarFiles(exeDir, localDataDir)
  return localDataDir
}

/** コマンドラインに貼れるよう、パスを二重引用符で囲む。 */
export const quoteCliPath = (path: string, errorMessage = 'Paths cannot contain double quotes.'): string => {
  if (path.trim() === '') throw new Error('path is empty')
  if (path.includes('"')) throw new Error(errorMessage)
  return `"${path}"`
}

/**
 * バッチやタスク スケジューラに貼る実行ファイルのフルパス。
 * パッケージ版は WindowsApps 実体ではなく AppExecutionAlias を返す。
 */
export const resolveCliLaunchPath = (input: {
  packaged: boolean
  processPath: string | null | undefined
  aliasPath: string
  fileExists: (p: string) => boolean
  fallbackDir: string
}): string => {
  const { packaged, processPath, aliasPath, fileExists, fallbackDir } = input
  if (packaged && aliasPath.trim() !== '' && fileExists(aliasPath)) return aliasPath
  if (processPath && processPath.trim() !== '') return processPath
  return join(fallbackDir, CLI_EXECUTABLE_FILE_NAME)
}

import { getHelpText, isHelpRequest, resolveMacro } from '../core/cli'
import { CancelledError } from '../core/runner'
import { loadConfig, loadSettings } from '../core/store'
import { createRealRunner } from './runtime'
import type { Services } from './services'

const writeConsole = async (text: string, services: Services): Promise<void> => {
  try {
    const { writeToParentConsole } = await import('../native/win32')
    if (await writeToParentConsole(text)) return
  } catch {
    // 下の経路へ
  }
  try {
    process.stdout.write(text + '\n')
  } catch {
    services.logger.write(text)
  }
}

/**
 * CLI 実行モード（SPEC §3.2）。管理画面は出さない。
 * 終了コード: 成功 0 / 失敗 1。dialog 手順だけは CLI でも確認ダイアログを出す。
 */
export const runCli = async (args: readonly string[], services: Services): Promise<number> => {
  const { t, logger } = services
  try {
    if (isHelpRequest(args)) {
      await writeConsole(getHelpText(t), services)
      return 0
    }

    let macros
    try {
      macros = loadConfig(services.config())
    } catch (e) {
      logger.writeError(e, t.get('Log_ConfigLoadFailed'))
      macros = null
    }

    const macro = macros ? resolveMacro(args, macros) : undefined
    if (!macro) {
      logger.write(t.format('Log_CliMacroNotFound', args.join(' ')))
      await writeConsole(t.get('Cli_MacroNotFound'), services)
      return 1
    }

    const settings = loadSettings(services.paths, (e) => logger.writeError(e, t.get('Log_SettingsLoadFailed')))
    const runner = await createRealRunner(services)
    await runner.executeMacro(macro, settings.action_delay_sec)
    return 0
  } catch (e) {
    if (e instanceof CancelledError) {
      logger.write(t.get('Log_CliCancelled'))
      return 1
    }
    logger.writeError(e, t.get('Log_CliError'))
    return 1
  }
}

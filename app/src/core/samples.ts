import type { Translator } from './loc'
import type { MacroItem } from './types'

/**
 * zip 同梱の config.sample.json と同じ手順の内蔵サンプル。
 * 表示名と確認ダイアログ文は applyLanguage で UI 言語に合わせる。
 */
const sampleMacros = (): MacroItem[] => [
  {
    id: 1,
    name: 'Login and type fixed data',
    alias: 'login_ok',
    delay_sec: 3.0,
    actions: [
      { type: 'text', value: 'user_admin' },
      { type: 'key', value: 'TAB' },
      { type: 'text', value: 'dummy_secret_do_not_use' },
      { type: 'key', value: 'ENTER' },
      { type: 'dialog', value: 'Press OK after you confirm login succeeded' },
      { type: 'wait', value: '1.0' },
      { type: 'hotkey', value: 'CTRL+S' }
    ]
  },
  {
    id: 2,
    name: 'Select all and copy',
    alias: 'select_copy',
    delay_sec: 2.0,
    actions: [
      { type: 'hotkey', value: 'CTRL+A' },
      { type: 'hotkey', value: 'CTRL+C' }
    ]
  },
  {
    id: 3,
    name: 'Enter 3 times',
    alias: 'enter_x3',
    delay_sec: 2.0,
    actions: [
      { type: 'repeat', value: '3' },
      { type: 'key', value: 'ENTER' },
      { type: 'end_repeat', value: '' }
    ]
  }
]

/** 既知のサンプル（alias）の表示名と確認ダイアログを UI 言語に合わせる。独自マクロはそのまま。 */
export const applySampleLanguage = (list: MacroItem[], t: Translator): void => {
  for (const item of list) {
    switch (item.alias) {
      case 'login_ok':
        item.name = t.get('Sample_LoginName')
        for (const a of item.actions) {
          if (a.type.toLowerCase() === 'dialog') a.value = t.get('Sample_LoginDialog')
        }
        break
      case 'select_copy':
        item.name = t.get('Sample_SelectCopyName')
        break
      case 'enter_x3':
        item.name = t.get('Sample_EnterX3Name')
        break
      default:
        break
    }
  }
}

export const createBuiltInSamples = (t: Translator): MacroItem[] => {
  const list = sampleMacros()
  applySampleLanguage(list, t)
  return list
}

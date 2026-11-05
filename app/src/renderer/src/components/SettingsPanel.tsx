import { useEffect, type ReactElement } from 'react'
import { LANG_EN, LANG_JA, LANG_SYSTEM } from '../../../core/loc'
import { changeLanguage, isBusy, openDataFolder, setSettingsOpen, updateSettings, useModel } from '../store'
import { Icon } from './Icon'

/** 設定パネル（言語・手順間隔・削除確認・データフォルダ）。 */
export function SettingsPanel(): ReactElement | null {
  const m = useModel()
  const { t, settings, settingsOpen, dataDir } = m

  useEffect(() => {
    if (!settingsOpen) return
    const onKey = (e: KeyboardEvent): void => {
      if (e.key === 'Escape' && !m.modal && !m.paletteOpen) setSettingsOpen(false)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [settingsOpen, m.modal, m.paletteOpen])

  if (!settingsOpen) return null
  return (
    <aside className="settings" aria-label={t.get('Settings_Title')} data-testid="settings">
      <div className="settings-head">
        <h2>{t.get('Settings_Title')}</h2>
        <button className="icon-btn" onClick={() => setSettingsOpen(false)} aria-label={t.get('Dialog_Cancel')}>
          <Icon name="x" />
        </button>
      </div>

      <label className="field">
        <span className="label">{t.get('Settings_Language')}</span>
        <select
          className="select"
          value={settings.ui_language}
          disabled={isBusy(m)}
          aria-label={t.get('LanguageBox.Name')}
          onChange={(e) => void changeLanguage(e.target.value)}
        >
          <option value={LANG_SYSTEM}>{t.get('Language_System')}</option>
          <option value={LANG_JA}>{t.get('Language_Japanese')}</option>
          <option value={LANG_EN}>{t.get('Language_English')}</option>
        </select>
      </label>

      <label className="field" title={t.get('ActionDelayBox.ToolTip')}>
        <span className="label">{t.get('ActionDelayLabel.Text')}</span>
        <span className="with-suffix">
          <input
            className="input"
            type="number"
            min={0}
            step={0.1}
            value={settings.action_delay_sec}
            aria-label={t.get('ActionDelayBox.Name')}
            onChange={(e) => {
              const n = Number(e.target.value)
              if (Number.isFinite(n) && n >= 0) void updateSettings({ action_delay_sec: n })
            }}
          />
          <span className="dim">{t.get('SecondsLabel.Text')}</span>
        </span>
      </label>

      <label className="switch-row" title={t.get('ConfirmDeleteSwitch.ToolTip')}>
        <input
          type="checkbox"
          role="switch"
          checked={settings.confirm_before_delete}
          aria-label={t.get('ConfirmDeleteSwitch.Name')}
          onChange={(e) => void updateSettings({ confirm_before_delete: e.target.checked })}
        />
        <span className="switch" aria-hidden="true" />
        <span>{t.get('ConfirmDeleteLabel.Text')}</span>
      </label>

      <div className="field">
        <span className="label">{t.get('Settings_Data')}</span>
        <code className="path" title={dataDir}>{dataDir}</code>
        <button className="btn btn-slim" onClick={() => void openDataFolder()} title={t.get('OpenFolderButton.ToolTip')}>
          <Icon name="folder" size={12} /> {t.get('OpenFolderButton.Content')}
        </button>
      </div>
    </aside>
  )
}

import { useEffect, useState, type ReactElement } from 'react'
import {
  isBusy,
  openDataFolder,
  setPalette,
  setSettingsOpen,
  toggleTestRun,
  useModel
} from '../store'
import { Icon } from './Icon'

export function TitleBar(): ReactElement {
  const { t } = useModel()
  return (
    <header className="titlebar">
      <div className="titlebar-inner">
        <span className="titlebar-brand">
          <Icon name="keyboard" size={16} />
          <span>KeyAutomator</span>
        </span>
        <button className="palette-trigger" onClick={() => setPalette(true)} aria-label={t.get('Palette_Trigger')} data-testid="palette-trigger">
          <Icon name="search" size={13} />
          <span>{t.get('Palette_Trigger')}</span>
          <kbd>Ctrl+K</kbd>
        </button>
      </div>
    </header>
  )
}

export function ActivityBar(): ReactElement {
  const { t, settingsOpen } = useModel()
  return (
    <nav className="activitybar" aria-label="Primary">
      <button className="activity active" title={t.get('Activity_Macros')} aria-label={t.get('Activity_Macros')} aria-current="page">
        <Icon name="list" size={22} />
      </button>
      <span className="activity-spacer" />
      <button
        className={`activity ${settingsOpen ? 'on' : ''}`}
        title={t.get('Activity_Settings')}
        aria-label={t.get('Activity_Settings')}
        aria-pressed={settingsOpen}
        onClick={() => setSettingsOpen(!settingsOpen)}
        data-testid="open-settings"
      >
        <Icon name="gear" size={22} />
      </button>
    </nav>
  )
}

export function StatusBar(): ReactElement {
  const m = useModel()
  const { t, status, macros, usingFallbackDir, dataDir, version } = m
  const running = m.run.phase !== 'idle'
  return (
    <footer className={`statusbar ${running ? 'running' : ''}`}>
      <span className={`status-msg ${status.kind}`} role="status" aria-live="polite" title={status.text}>
        {status.text || t.get('Status_Ready')}
      </span>
      <span className="statusbar-right">
        <span>{t.format('Status_Count', macros.length)}</span>
        <button
          className="status-link"
          onClick={() => void openDataFolder()}
          title={`${t.get('OpenFolderButton.ToolTip')}\n${dataDir}`}
          aria-label={t.get('OpenFolderButton.Name')}
        >
          <Icon name="folder" size={12} /> {usingFallbackDir ? 'AppData' : t.get('OpenFolderButton.Content')}
        </button>
        <span>{t.language === 'ja-JP' ? '日本語' : 'English'}</span>
        <span>v{version}</span>
      </span>
    </footer>
  )
}

/** テスト実行中の帯。開始までのカウントダウンと中断ボタン。 */
export function RunBanner(): ReactElement | null {
  const m = useModel()
  const { t, run } = m
  const [now, setNow] = useState(Date.now())
  const active = run.phase !== 'idle'

  useEffect(() => {
    if (!active) return
    const id = window.setInterval(() => setNow(Date.now()), 200)
    return () => window.clearInterval(id)
  }, [active])

  if (!active) return null
  const remaining = Math.max(0, run.delaySec - (now - run.startedAt) / 1000)
  const waiting = remaining > 0
  return (
    <div className="runbanner" role="status" aria-live="polite">
      <span className="runbanner-bar" aria-hidden="true" />
      <span className="runbanner-text">
        {waiting ? t.format('Run_WaitingStart', remaining.toFixed(1)) : t.get('Run_Running')}
      </span>
      <button className="btn btn-slim btn-stop" disabled={isBusy(m) && run.phase === 'stopping'} onClick={() => void toggleTestRun()}>
        {t.get('Run_Stop')}
      </button>
    </div>
  )
}

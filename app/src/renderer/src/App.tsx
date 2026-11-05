import { useEffect, type ReactElement } from 'react'
import { CommandPalette } from './components/CommandPalette'
import { ActivityBar, RunBanner, StatusBar, TitleBar } from './components/Chrome'
import { Editor } from './components/Editor'
import { Modal } from './components/Modal'
import { SettingsPanel } from './components/SettingsPanel'
import { Sidebar } from './components/Sidebar'
import {
  cloneMacro,
  deleteSelectedMacros,
  getModel,
  init,
  moveSelectedActions,
  newMacro,
  removeSelectedActions,
  setPalette,
  toggleTestRun,
  trySave,
  useModel
} from './store'

const isTextField = (el: EventTarget | null): boolean =>
  el instanceof HTMLElement && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.tagName === 'SELECT' || el.isContentEditable)

/** ショートカット。入力欄の中では Delete などの破壊的操作を奪わない。 */
const useShortcuts = (): void => {
  useEffect(() => {
    const onKey = (e: KeyboardEvent): void => {
      const m = getModel()
      if (m.modal) return
      const mod = e.ctrlKey || e.metaKey
      const key = e.key.toLowerCase()

      if (mod && (key === 'k' || (e.shiftKey && key === 'p'))) {
        e.preventDefault()
        setPalette(!m.paletteOpen)
        return
      }
      if (m.paletteOpen) return

      if (mod && key === 's') {
        e.preventDefault()
        void trySave()
      } else if (mod && key === 'n') {
        e.preventDefault()
        void newMacro()
      } else if (mod && key === 'd' && !isTextField(e.target)) {
        e.preventDefault()
        void cloneMacro()
      } else if (e.key === 'F5') {
        e.preventDefault()
        void toggleTestRun()
      } else if (e.altKey && (e.key === 'ArrowUp' || e.key === 'ArrowDown')) {
        e.preventDefault()
        moveSelectedActions(e.key === 'ArrowUp' ? -1 : 1)
      } else if (e.key === 'Delete' && !isTextField(e.target)) {
        e.preventDefault()
        const inSteps = (e.target as HTMLElement | null)?.closest?.('.steps') != null
        void (inSteps ? removeSelectedActions() : deleteSelectedMacros())
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])
}

export function App(): ReactElement {
  const { ready, t } = useModel()
  useShortcuts()

  useEffect(() => {
    void init().then(() => {
      // 初回描画が済んでから通知する（スプラッシュを閉じてメインを出す）
      requestAnimationFrame(() => requestAnimationFrame(() => window.api.notifyReady()))
    })
  }, [])

  useEffect(() => {
    document.documentElement.lang = t.language === 'ja-JP' ? 'ja' : 'en'
  }, [t.language])

  if (!ready) return <div className="boot" aria-busy="true" />

  return (
    <div className="app">
      <TitleBar />
      <RunBanner />
      <div className="workspace">
        <ActivityBar />
        <Sidebar />
        <main className="main">
          <Editor />
        </main>
        <SettingsPanel />
      </div>
      <StatusBar />
      <CommandPalette />
      <Modal />
    </div>
  )
}

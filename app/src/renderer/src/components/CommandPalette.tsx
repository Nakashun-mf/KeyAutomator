import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react'
import { LANG_EN, LANG_JA, LANG_SYSTEM } from '../../../core/loc'
import {
  changeLanguage,
  cloneMacro,
  copyLaunchCommand,
  deleteSelectedMacros,
  discardEdits,
  loadSamples,
  newMacro,
  openDataFolder,
  setPalette,
  setSettingsOpen,
  toggleTestRun,
  trySave,
  useModel
} from '../store'

interface Command {
  id: string
  label: string
  hint?: string
  run(): void | Promise<void>
}

/** VSCode 風のコマンドパレット（Ctrl+K / Ctrl+Shift+P）。 */
export function CommandPalette(): ReactElement | null {
  const m = useModel()
  const { t, paletteOpen } = m
  const [query, setQuery] = useState('')
  const [index, setIndex] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)

  const commands = useMemo<Command[]>(
    () => [
      { id: 'new', label: t.get('NewMacroButton.Name'), hint: 'Ctrl+N', run: newMacro },
      { id: 'save', label: t.get('SaveButton.Name'), hint: 'Ctrl+S', run: () => void trySave() },
      { id: 'discard', label: t.get('DiscardButton.Name'), run: discardEdits },
      { id: 'test', label: t.get('TestButton.Name'), hint: 'F5', run: toggleTestRun },
      { id: 'clone', label: t.get('CloneButton.Name'), run: cloneMacro },
      { id: 'delete', label: t.get('DeleteMacroButton.Name'), run: deleteSelectedMacros },
      { id: 'samples', label: t.get('LoadSampleButton.Name'), run: loadSamples },
      { id: 'copy', label: t.get('CopyLaunchInlineButton.Name'), run: copyLaunchCommand },
      { id: 'folder', label: t.get('OpenFolderButton.Name'), run: openDataFolder },
      { id: 'settings', label: t.get('Cmd_Settings'), run: () => setSettingsOpen(true) },
      { id: 'lang-sys', label: `${t.get('Settings_Language')}: ${t.get('Language_System')}`, run: () => changeLanguage(LANG_SYSTEM) },
      { id: 'lang-ja', label: `${t.get('Settings_Language')}: ${t.get('Language_Japanese')}`, run: () => changeLanguage(LANG_JA) },
      { id: 'lang-en', label: `${t.get('Settings_Language')}: ${t.get('Language_English')}`, run: () => changeLanguage(LANG_EN) }
    ],
    [t]
  )

  const shown = useMemo(() => {
    const q = query.trim().toLowerCase()
    return q === '' ? commands : commands.filter((c) => c.label.toLowerCase().includes(q))
  }, [commands, query])

  useEffect(() => {
    if (paletteOpen) {
      setQuery('')
      setIndex(0)
      requestAnimationFrame(() => inputRef.current?.focus())
    }
  }, [paletteOpen])

  useEffect(() => setIndex(0), [query])

  if (!paletteOpen || m.modal) return null

  const run = (c: Command | undefined): void => {
    if (!c) return
    setPalette(false)
    void c.run()
  }

  return (
    <div className="overlay overlay-top" role="presentation" onMouseDown={(e) => e.target === e.currentTarget && setPalette(false)}>
      <div className="palette" role="dialog" aria-modal="true" aria-label={t.get('Palette_Trigger')}>
        <input
          ref={inputRef}
          className="palette-input"
          value={query}
          placeholder={t.get('Palette_Placeholder')}
          aria-label={t.get('Palette_Placeholder')}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'ArrowDown') {
              e.preventDefault()
              setIndex((i) => Math.min(shown.length - 1, i + 1))
            } else if (e.key === 'ArrowUp') {
              e.preventDefault()
              setIndex((i) => Math.max(0, i - 1))
            } else if (e.key === 'Enter') {
              e.preventDefault()
              run(shown[index])
            } else if (e.key === 'Escape') {
              e.preventDefault()
              setPalette(false)
            }
          }}
        />
        <ul className="palette-list" role="listbox">
          {shown.map((c, i) => (
            <li
              key={c.id}
              role="option"
              aria-selected={i === index}
              className={i === index ? 'on' : ''}
              onMouseMove={() => setIndex(i)}
              onClick={() => run(c)}
            >
              <span>{c.label}</span>
              {c.hint ? <kbd>{c.hint}</kbd> : null}
            </li>
          ))}
          {shown.length === 0 ? <li className="palette-empty">{t.get('Palette_NoResults')}</li> : null}
        </ul>
      </div>
    </div>
  )
}

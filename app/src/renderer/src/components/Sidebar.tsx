import { useState, type ReactElement } from 'react'
import { activeMacro, cloneMacro, deleteSelectedMacros, isBusy, loadSamples, newMacro, reorderMacros, selectMacro, useModel } from '../store'
import { Icon } from './Icon'

/** 左サイドバー: マクロ一覧（複数選択・ドラッグで並べ替え）。 */
export function Sidebar(): ReactElement {
  const m = useModel()
  const { t, macros, selectedIds, activeId } = m
  const [dragFrom, setDragFrom] = useState<number | null>(null)
  const [dropAt, setDropAt] = useState<number | null>(null)
  const busy = isBusy(m)
  const hasActive = activeMacro(m) !== null

  const onClick = (e: React.MouseEvent, id: number): void =>
    void selectMacro(id, e.shiftKey ? 'range' : e.ctrlKey || e.metaKey ? 'toggle' : 'single')

  return (
    <aside className="sidebar" aria-label={t.get('MacroListHeading.Text')}>
      <div className="sidebar-head">
        <h2 className="sidebar-title">{t.get('MacroListHeading.Text')}</h2>
        <div className="sidebar-tools">
          <button className="icon-btn" disabled={busy} onClick={() => void newMacro()} title={t.get('NewMacroButton.Name')} aria-label={t.get('NewMacroButton.Name')} data-testid="new-macro">
            <Icon name="plus" />
          </button>
          <button className="icon-btn" disabled={busy || !hasActive} onClick={() => void cloneMacro()} title={t.get('CloneButton.Name')} aria-label={t.get('CloneButton.Name')}>
            <Icon name="copy" />
          </button>
          <button className="icon-btn" disabled={busy || !hasActive} onClick={() => void deleteSelectedMacros()} title={t.get('DeleteMacroButton.Name')} aria-label={t.get('DeleteMacroButton.Name')}>
            <Icon name="trash" />
          </button>
          <button className="icon-btn" disabled={busy} onClick={() => void loadSamples()} title={t.get('LoadSampleButton.ToolTip')} aria-label={t.get('LoadSampleButton.Name')}>
            <Icon name="sparkle" />
          </button>
        </div>
      </div>
      <p className="sidebar-hint">{t.get('MacroListHint.Text')}</p>

      {macros.length === 0 ? (
        <div className="sidebar-empty">
          <Icon name="keyboard" size={28} />
          <p>{t.get('EmptyHint.Text')}</p>
        </div>
      ) : (
        <ul className="macro-list" role="listbox" aria-multiselectable="true" aria-label={t.get('MacroList.Name')} onDragLeave={() => setDropAt(null)}>
          {macros.map((macro, index) => {
            const selected = selectedIds.includes(macro.id)
            const active = macro.id === activeId
            return (
              <li
                key={macro.id}
                role="option"
                aria-selected={selected}
                tabIndex={0}
                draggable={!busy}
                className={['macro-item', selected ? 'selected' : '', active ? 'active' : '', dropAt === index && dragFrom !== index ? 'drop-target' : ''].join(' ')}
                onClick={(e) => onClick(e, macro.id)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault()
                    void selectMacro(macro.id)
                  }
                }}
                onDragStart={(e) => {
                  setDragFrom(index)
                  e.dataTransfer.effectAllowed = 'move'
                  e.dataTransfer.setData('text/plain', String(macro.id))
                }}
                onDragOver={(e) => {
                  if (dragFrom === null) return
                  e.preventDefault()
                  setDropAt(index)
                }}
                onDrop={(e) => {
                  e.preventDefault()
                  if (dragFrom !== null && dragFrom !== index) void reorderMacros(dragFrom, index)
                  setDragFrom(null)
                  setDropAt(null)
                }}
                onDragEnd={() => {
                  setDragFrom(null)
                  setDropAt(null)
                }}
              >
                <span className="macro-id">{macro.id}</span>
                <span className="macro-body">
                  <span className="macro-name">{macro.name}</span>
                  <span className="macro-meta">
                    {macro.alias.trim() !== '' ? <span className="chip chip-alias">{macro.alias}</span> : null}
                    <span>{macro.actions.length === 0 ? t.get('Macro_NoActions') : t.format('Macro_ActionCount', macro.actions.length)}</span>
                    <span aria-hidden="true">·</span>
                    <span>{t.format('Macro_DelayLabel', macro.delay_sec.toLocaleString(t.language, { maximumFractionDigits: 2 }))}</span>
                  </span>
                </span>
              </li>
            )
          })}
        </ul>
      )}
    </aside>
  )
}

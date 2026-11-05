import { useMemo, useState, type ReactElement } from 'react'
import { computeDepths } from '../../../core/repeatBlock'
import { canMoveSelected, countLoops } from '../../../core/editor'
import {
  activeMacro,
  addAction,
  copyLaunchCommand,
  discardEdits,
  isBusy,
  isDirty,
  loadSamples,
  moveSelectedActions,
  newMacro,
  removeSelectedActions,
  reorderActions,
  selectAction,
  toggleTestRun,
  trySave,
  updateDraft,
  useModel,
  wrapSelectionInRepeat
} from '../store'
import { Icon } from './Icon'
import { StepRow } from './StepRow'

const ADD_BUTTONS: { type: string; key: string; icon?: 'repeat' }[] = [
  { type: 'text', key: 'AddTextButton' },
  { type: 'key', key: 'AddKeyButton' },
  { type: 'hotkey', key: 'AddHotkeyButton' },
  { type: 'mouse', key: 'AddMouseButton' },
  { type: 'wait', key: 'AddWaitButton' },
  { type: 'dialog', key: 'AddDialogButton' }
]

function EmptyState(): ReactElement {
  const { t } = useModel()
  return (
    <section className="editor-empty">
      <Icon name="keyboard" size={48} />
      <h2>{t.get('EmptyTitle.Text')}</h2>
      <p>{t.get('EmptyHint.Text')}</p>
      <div className="row">
        <button className="btn btn-primary" onClick={() => void newMacro()}>{t.get('EmptyNewButton.Content')}</button>
        <button className="btn" onClick={() => void loadSamples()} title={t.get('EmptyLoadSampleButton.ToolTip')}>
          {t.get('EmptyLoadSampleButton.Content')}
        </button>
      </div>
      <p className="dim">{t.get('EmptyTestHint.Text')}</p>
    </section>
  )
}

/** 右側: マクロ詳細編集（基本情報・手順一覧・操作ボタン）。 */
export function Editor(): ReactElement {
  const m = useModel()
  const { t, draft, selectedActions } = m
  const macro = activeMacro(m)
  const busy = isBusy(m)
  const dirty = isDirty(m)
  const [dragFrom, setDragFrom] = useState<number | null>(null)
  const [dropAt, setDropAt] = useState<number | null>(null)
  const depths = useMemo(() => (draft ? computeDepths(draft.actions) : []), [draft])

  if (!draft || !macro) return <EmptyState />

  const selected = new Set(selectedActions)
  const hasSelection = selectedActions.length > 0
  const canUp = !busy && canMoveSelected(draft.actions, selected, -1)
  const canDown = !busy && canMoveSelected(draft.actions, selected, 1)
  const loops = countLoops(draft.actions)

  return (
    <section className="editor" aria-label={t.get('EditHeading.Text')}>
      <div className="tabs" role="tablist">
        <div className="tab active" role="tab" aria-selected="true">
          <span className="tab-id">{macro.id}</span>
          <span className="tab-name">{draft.name.trim() || t.get('Macro_DefaultName')}</span>
          {dirty ? <span className="tab-dirty" title={t.get('Dialog_UnsavedTitle')} aria-label={t.get('Dialog_UnsavedTitle')} /> : null}
        </div>
      </div>

      <div className="editor-scroll">
        <div className="form-grid">
          <label className="field field-id">
            <span className="label">{t.get('LabelId.Text')}</span>
            <input className="input" type="number" min={1} step={1} value={draft.id} disabled={busy} aria-label={t.get('EditIdBox.Name')} onChange={(e) => updateDraft({ id: e.target.value })} />
          </label>
          <label className="field field-name">
            <span className="label">{t.get('LabelName.Text')}</span>
            <input className="input" type="text" value={draft.name} disabled={busy} placeholder={t.get('EditNameBox.PlaceholderText')} aria-label={t.get('EditNameBox.Name')} onChange={(e) => updateDraft({ name: e.target.value })} data-testid="name" />
          </label>
          <label className="field field-delay" title={t.get('LabelDelay.ToolTip')}>
            <span className="label">{t.get('LabelDelay.Text')}</span>
            <span className="with-suffix">
              <input className="input" type="number" min={0} step={0.5} value={draft.delay} disabled={busy} aria-label={t.get('EditDelayBox.Name')} title={t.get('EditDelayBox.ToolTip')} onChange={(e) => updateDraft({ delay: e.target.value })} />
              <span className="dim">{t.get('SecondsLabel.Text')}</span>
            </span>
          </label>
          <label className="field field-alias" title={t.get('LabelAlias.ToolTip')}>
            <span className="label">{t.get('LabelAlias.Text')}</span>
            <input className="input input-mono" type="text" value={draft.alias} disabled={busy} placeholder={t.get('EditAliasBox.PlaceholderText')} aria-label={t.get('EditAliasBox.Name')} title={t.get('EditAliasBox.ToolTip')} onChange={(e) => updateDraft({ alias: e.target.value })} />
          </label>
        </div>

        <div className="steps-head">
          <h3 className="section-title">{t.get('ActionsHeading.Text')}</h3>
          <span className="dim">
            {draft.actions.length === 0
              ? t.get('Status_NoActionsHint')
              : loops > 0
                ? t.format('Status_ActionSummaryWithLoops', draft.actions.length, loops)
                : t.format('Status_ActionSummary', draft.actions.length)}
          </span>
        </div>

        <div className="toolbar" role="toolbar" aria-label={t.get('ActionsHeading.Text')}>
          {ADD_BUTTONS.map((b) => (
            <button key={b.type} className="btn btn-slim" disabled={busy} title={t.get(`${b.key}.Name`)} aria-label={t.get(`${b.key}.Name`)} onClick={() => addAction(b.type)}>
              <Icon name="plus" size={12} /> {t.get(`${b.key}.Content`)}
            </button>
          ))}
          <button className="btn btn-slim" disabled={busy} title={t.get('WrapRepeatButton.ToolTip')} aria-label={t.get('WrapRepeatButton.Name')} onClick={wrapSelectionInRepeat} data-testid="wrap-repeat">
            <Icon name="repeat" size={12} /> {t.get('WrapRepeatButton.Content')}
          </button>
          <span className="toolbar-sep" />
          <button className="icon-btn" disabled={!canUp} title={t.get('MoveUpButton.ToolTip')} aria-label={t.get('MoveUpButton.Name')} onClick={() => moveSelectedActions(-1)}>
            <Icon name="up" />
          </button>
          <button className="icon-btn" disabled={!canDown} title={t.get('MoveDownButton.ToolTip')} aria-label={t.get('MoveDownButton.Name')} onClick={() => moveSelectedActions(1)}>
            <Icon name="down" />
          </button>
          <button className="icon-btn" disabled={busy || !hasSelection} title={t.get('RemoveActionButton.Name')} aria-label={t.get('RemoveActionButton.Name')} onClick={() => void removeSelectedActions()}>
            <Icon name="trash" />
          </button>
        </div>

        <div className="steps-columns" aria-hidden="true">
          <span />
          <span>#</span>
          <span>{t.get('ColType.Text')}</span>
          <span>{t.get('ColValue.Text')}</span>
        </div>
        <ul
          className="steps"
          role="listbox"
          aria-multiselectable="true"
          aria-label={t.get('ActionList.Name')}
          title={t.get('ActionList.ToolTip')}
          onDragLeave={() => setDropAt(null)}
          data-testid="steps"
        >
          {draft.actions.map((a, i) => (
            <StepRow
              key={a.uid}
              action={a}
              step={i + 1}
              depth={depths[i] ?? 0}
              selected={selected.has(a.uid)}
              disabled={busy}
              t={t}
              onSelect={(e) => {
                // 入力欄の操作では、複数選択を崩さない
                const inField = (e.target as HTMLElement).closest('input,select,button') !== null
                if (inField && selected.has(a.uid) && !e.shiftKey && !e.ctrlKey && !e.metaKey) return
                selectAction(a.uid, e.shiftKey ? 'range' : e.ctrlKey || e.metaKey ? 'toggle' : 'single')
              }}
              dragHandlers={{
                dropTarget: dropAt === i && dragFrom !== i,
                onDragStart: (e) => {
                  setDragFrom(i)
                  e.dataTransfer.effectAllowed = 'move'
                  e.dataTransfer.setData('text/plain', a.uid)
                  const row = (e.target as HTMLElement).closest('li')
                  if (row) e.dataTransfer.setDragImage(row, 12, 12)
                },
                onDragOver: (e) => {
                  if (dragFrom === null) return
                  e.preventDefault()
                  setDropAt(i)
                },
                onDrop: (e) => {
                  e.preventDefault()
                  if (dragFrom !== null && dragFrom !== i) reorderActions(dragFrom, i)
                  setDragFrom(null)
                  setDropAt(null)
                },
                onDragEnd: () => {
                  setDragFrom(null)
                  setDropAt(null)
                }
              }}
            />
          ))}
          {draft.actions.length === 0 ? <li className="steps-empty">{t.get('Status_NoActionsHint')}</li> : null}
        </ul>
      </div>

      <div className="actionbar">
        <button className="btn btn-primary" disabled={busy || !dirty} onClick={() => void trySave()} title="Ctrl+S" data-testid="save">
          <Icon name="save" /> {t.get('SaveButton.Content')}
        </button>
        <button className="btn" disabled={busy || !dirty} onClick={discardEdits}>
          <Icon name="undo" /> {t.get('DiscardButton.Content')}
        </button>
        <span className="actionbar-sep" />
        <button
          className={`btn ${m.run.phase === 'idle' ? 'btn-run' : 'btn-stop'}`}
          disabled={m.run.phase === 'stopping'}
          onClick={() => void toggleTestRun()}
          title={`${t.get('TestButton.ToolTip')} (F5)`}
          data-testid="test"
        >
          <Icon name={m.run.phase === 'idle' ? 'play' : 'stop'} /> {m.run.phase === 'idle' ? t.get('Test_Run') : t.get('Test_Stop')}
        </button>
        <button className="btn" onClick={() => void copyLaunchCommand()} title={t.get('CopyLaunchInlineButton.ToolTip')}>
          <Icon name="terminal" /> {t.get('CopyLaunchInlineButton.Content')}
        </button>
      </div>
    </section>
  )
}

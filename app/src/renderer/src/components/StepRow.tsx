import type { ReactElement } from 'react'
import {
  ACTION_TYPES,
  MOUSE_ACTIONS,
  SPECIAL_KEYS,
  getActionType,
  specialKeyLabel
} from '../../../core/actions'
import { hotkeyParts, joinHotkey, type DraftAction } from '../../../core/editor'
import { REPEAT_MAX_COUNT, isRepeatEnd, isRepeatStart, parseRepeatCount } from '../../../core/repeatBlock'
import { setActionField } from '../store'
import type { Translator } from '../../../core/loc'
import { Icon } from './Icon'

interface Props {
  action: DraftAction
  step: number
  depth: number
  selected: boolean
  disabled: boolean
  t: Translator
  onSelect(e: React.MouseEvent): void
  dragHandlers: {
    onDragStart(e: React.DragEvent): void
    onDragOver(e: React.DragEvent): void
    onDrop(e: React.DragEvent): void
    onDragEnd(): void
    dropTarget: boolean
  }
}

function ValueEditor({ action, t, disabled }: { action: DraftAction; t: Translator; disabled: boolean }): ReactElement {
  const { uid, type, value } = action
  const set = (v: string): void => setActionField(uid, { value: v })

  if (isRepeatEnd(type)) {
    return (
      <span className="step-end" aria-label={t.get('EndRepeatLabel.Name')}>
        {t.get('EndRepeatLabel.Text')}
      </span>
    )
  }

  if (isRepeatStart(type)) {
    const n = parseRepeatCount(value)
    return (
      <span className="step-repeat">
        <input
          className={`input input-num ${n === null ? 'invalid' : ''}`}
          type="number"
          min={1}
          max={REPEAT_MAX_COUNT}
          step={1}
          value={value}
          disabled={disabled}
          aria-label={t.get('RepeatCountBox.Name')}
          onChange={(e) => set(e.target.value)}
        />
        <span className="dim">{t.get('RepeatTimesLabel.Text')}</span>
      </span>
    )
  }

  switch (type) {
    case 'key':
      return (
        <select className="select" value={value} disabled={disabled} aria-label={t.get('SpecialKeyBox.Name')} onChange={(e) => set(e.target.value)}>
          {SPECIAL_KEYS.map((k) => (
            <option key={k.code} value={k.code}>
              {specialKeyLabel(k, t)}
            </option>
          ))}
        </select>
      )
    case 'mouse':
      return (
        <select className="select" value={value} disabled={disabled} aria-label={t.get('MouseActionBox.Name')} onChange={(e) => set(e.target.value)}>
          {MOUSE_ACTIONS.map((m) => (
            <option key={m.code} value={m.code}>
              {t.get(m.labelKey)}
            </option>
          ))}
        </select>
      )
    case 'hotkey': {
      const parts = hotkeyParts(value)
      const update = (next: string[]): void => set(joinHotkey(next))
      return (
        <span className="hotkey">
          {parts.map((code, i) => (
            <span className="hotkey-part" key={`${i}-${code}`}>
              {i > 0 ? <span className="hotkey-plus" aria-hidden="true">+</span> : null}
              <select
                className="select select-key"
                value={code}
                disabled={disabled}
                aria-label={t.get('HotkeyPartBox.Name')}
                onChange={(e) => update(parts.map((p, j) => (j === i ? e.target.value : p)))}
              >
                {SPECIAL_KEYS.map((k) => (
                  <option key={k.code} value={k.code}>
                    {specialKeyLabel(k, t)}
                  </option>
                ))}
              </select>
              <button
                className="icon-btn icon-btn-sm"
                disabled={disabled || parts.length <= 1}
                title={t.get('RemoveHotkeyPartButton.ToolTip')}
                aria-label={t.get('RemoveHotkeyPartButton.Name')}
                onClick={() => update(parts.filter((_, j) => j !== i))}
              >
                <Icon name="x" size={12} />
              </button>
            </span>
          ))}
          <button
            className="icon-btn icon-btn-sm"
            disabled={disabled}
            title={t.get('AddHotkeyPartButton.ToolTip')}
            aria-label={t.get('AddHotkeyPartButton.Name')}
            onClick={() => update([...parts, 'A'])}
          >
            <Icon name="plus" size={12} />
          </button>
        </span>
      )
    }
    default: {
      const placeholder = getActionType(type).placeholderKey ? t.get(getActionType(type).placeholderKey as string) : ''
      return (
        <input
          className="input input-mono"
          type="text"
          value={value}
          disabled={disabled}
          placeholder={placeholder}
          aria-label={t.get('ActionValueBox.Name')}
          onChange={(e) => set(e.target.value)}
        />
      )
    }
  }
}

/** 手順 1 行。繰り返しブロックはインデントと左のアクセントバーで示す。 */
export function StepRow({ action, step, depth, selected, disabled, t, onSelect, dragHandlers }: Props): ReactElement {
  const isMarker = isRepeatStart(action.type) || isRepeatEnd(action.type)
  return (
    <li
      className={['step', selected ? 'selected' : '', isMarker ? 'marker' : '', depth > 0 ? 'nested' : '', dragHandlers.dropTarget ? 'drop-target' : ''].join(' ')}
      style={{ ['--depth' as string]: depth }}
      aria-selected={selected}
      role="option"
      onClick={onSelect}
      onDragOver={dragHandlers.onDragOver}
      onDrop={dragHandlers.onDrop}
    >
      <span
        className="step-grip"
        draggable={!disabled}
        onDragStart={dragHandlers.onDragStart}
        onDragEnd={dragHandlers.onDragEnd}
        aria-hidden="true"
      >
        <Icon name="grip" size={14} />
      </span>
      <span className="step-no" aria-hidden="true">{step}</span>
      <span className="step-type">
        <select
          className="select"
          value={action.type}
          disabled={disabled}
          aria-label={t.get('ActionTypeBox.Name')}
          onChange={(e) => setActionField(action.uid, { type: e.target.value })}
        >
          {!ACTION_TYPES.some((x) => x.code === action.type) ? <option value={action.type}>{action.type}</option> : null}
          {ACTION_TYPES.map((x) => (
            <option key={x.code} value={x.code}>
              {t.get(x.labelKey)}
            </option>
          ))}
        </select>
      </span>
      <span className="step-value">
        <ValueEditor action={action} t={t} disabled={disabled} />
      </span>
    </li>
  )
}

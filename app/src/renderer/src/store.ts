import { useSyncExternalStore } from 'react'
import {
  changeActionType,
  draftFromMacro,
  draftToMacro,
  insertAction,
  isDraftDirty,
  moveSelected,
  removeActions,
  reorder,
  validateDraft,
  wrapInRepeat,
  type Draft
} from '../../core/editor'
import { createTranslator, type Translator } from '../../core/loc'
import { cloneMacro as cloneMacroItem, defaultSettings, type AppSettings, type MacroItem } from '../../core/types'
import { nextId } from '../../core/macros'
import type { AppState } from '../../shared/api'

export type StatusKind = 'info' | 'ok' | 'error'

export interface ModalButton {
  id: string
  label: string
  kind?: 'primary' | 'danger' | 'default'
}

export interface ModalSpec {
  title: string
  body: string
  buttons: ModalButton[]
  /** Enter で選ばれるボタン */
  defaultId: string
  /** Esc / 外側クリックで選ばれるボタン */
  cancelId: string
}

export type RunPhase = 'idle' | 'running' | 'stopping'

export interface Model {
  ready: boolean
  macros: MacroItem[]
  settings: AppSettings
  systemLocale: string
  dataDir: string
  configPath: string
  usingFallbackDir: boolean
  version: string
  t: Translator
  /** エディタで開いているマクロの ID（保存済みの ID）。 */
  activeId: number | null
  /** 一覧での複数選択。 */
  selectedIds: number[]
  draft: Draft | null
  selectedActions: string[]
  status: { text: string; kind: StatusKind }
  run: { phase: RunPhase; startedAt: number; delaySec: number }
  modal: (ModalSpec & { resolve: (id: string) => void }) | null
  paletteOpen: boolean
  settingsOpen: boolean
}

const initialT = createTranslator('', 'en-US')

let model: Model = {
  ready: false,
  macros: [],
  settings: defaultSettings(),
  systemLocale: 'en-US',
  dataDir: '',
  configPath: '',
  usingFallbackDir: false,
  version: '',
  t: initialT,
  activeId: null,
  selectedIds: [],
  draft: null,
  selectedActions: [],
  status: { text: '', kind: 'info' },
  run: { phase: 'idle', startedAt: 0, delaySec: 0 },
  modal: null,
  paletteOpen: false,
  settingsOpen: false
}

const listeners = new Set<() => void>()
const set = (patch: Partial<Model>): void => {
  model = { ...model, ...patch }
  listeners.forEach((l) => l())
}
export const getModel = (): Model => model
export const useModel = (): Model =>
  useSyncExternalStore(
    (cb) => {
      listeners.add(cb)
      return () => void listeners.delete(cb)
    },
    () => model
  )

const status = (text: string, kind: StatusKind = 'info'): void => set({ status: { text, kind } })

// ------------------------------------------------------------------ helpers

export const activeMacro = (m: Model = model): MacroItem | null =>
  m.activeId == null ? null : (m.macros.find((x) => x.id === m.activeId) ?? null)

export const isDirty = (m: Model = model): boolean => isDraftDirty(m.draft, activeMacro(m), m.t)

export const isBusy = (m: Model = model): boolean => m.run.phase !== 'idle'

/** 確認モーダルを出して、押されたボタンの id を返す。 */
export const ask = (spec: ModalSpec): Promise<string> =>
  new Promise((resolve) => {
    set({
      modal: {
        ...spec,
        resolve: (id) => {
          set({ modal: null })
          resolve(id)
        }
      }
    })
  })

const persist = async (macros: MacroItem[]): Promise<boolean> => {
  const r = await window.api.saveMacros(macros)
  if (r.ok) return true
  const hint = r.logPath ? model.t.format('Status_LogHint', r.logPath) : model.t.get('Status_LogMissing')
  status(model.t.format('Status_SaveFailed', model.configPath, hint), 'error')
  return false
}

const loadEditor = (macro: MacroItem | null): Partial<Model> => ({
  draft: macro ? draftFromMacro(macro, model.t) : null,
  selectedActions: []
})

const applyState = (s: AppState): void => {
  const t = createTranslator(s.settings.ui_language, s.systemLocale)
  const first = s.macros[0] ?? null
  set({
    ready: true,
    macros: s.macros,
    settings: s.settings,
    systemLocale: s.systemLocale,
    dataDir: s.dataDir,
    configPath: s.configPath,
    usingFallbackDir: s.usingFallbackDir,
    version: s.version,
    t,
    activeId: first?.id ?? null,
    selectedIds: first ? [first.id] : [],
    ...(first ? { draft: draftFromMacro(first, t) } : { draft: null }),
    selectedActions: []
  })
  void window.api.setLanguage(s.settings.ui_language)

  if (s.loadError) {
    const hint = s.loadError.logPath ? t.format('Status_LogHint', s.loadError.logPath) : t.get('Status_LogMissing')
    status(
      s.loadError.backup
        ? t.format('Status_LoadFailedBackup', basename(s.loadError.backup), hint)
        : t.format('Status_LoadFailed', s.configPath, hint),
      'error'
    )
  } else {
    status(
      s.usingFallbackDir
        ? t.format('Status_LoadedFallback', s.macros.length, s.configPath)
        : t.format('Status_Loaded', s.macros.length, s.configPath)
    )
  }
}

const basename = (p: string): string => p.split(/[\\/]/).pop() ?? p

// ------------------------------------------------------------------ actions

export const init = async (): Promise<void> => {
  applyState(await window.api.getState())
}

/** 未保存の編集があれば 保存／破棄／キャンセル を確認する。続行してよければ true。 */
export const ensureCanLeave = async (): Promise<boolean> => {
  if (!isDirty()) return true
  const t = model.t
  const choice = await ask({
    title: t.get('Dialog_UnsavedTitle'),
    body: t.get('Dialog_UnsavedBody'),
    buttons: [
      { id: 'save', label: t.get('Dialog_Save'), kind: 'primary' },
      { id: 'discard', label: t.get('Dialog_Discard') },
      { id: 'cancel', label: t.get('Dialog_Cancel') }
    ],
    defaultId: 'save',
    cancelId: 'cancel'
  })
  if (choice === 'save') return trySave()
  if (choice === 'discard') {
    set(loadEditor(activeMacro()))
    return true
  }
  return false
}

export const selectMacro = async (id: number, mode: 'single' | 'toggle' | 'range' = 'single'): Promise<void> => {
  if (isBusy()) return
  const { macros, selectedIds, activeId } = model

  if (mode === 'toggle') {
    const next = selectedIds.includes(id) ? selectedIds.filter((x) => x !== id) : [...selectedIds, id]
    set({ selectedIds: next })
    return
  }
  if (mode === 'range' && activeId != null) {
    const a = macros.findIndex((m) => m.id === activeId)
    const b = macros.findIndex((m) => m.id === id)
    if (a >= 0 && b >= 0) {
      const [lo, hi] = a < b ? [a, b] : [b, a]
      set({ selectedIds: macros.slice(lo, hi + 1).map((m) => m.id) })
      return
    }
  }
  if (id === activeId) {
    set({ selectedIds: [id] })
    return
  }
  if (!(await ensureCanLeave())) return
  const target = model.macros.find((m) => m.id === id) ?? null
  set({ activeId: target?.id ?? null, selectedIds: target ? [target.id] : [], ...loadEditor(target) })
}

export const newMacro = async (): Promise<void> => {
  if (isBusy() || !(await ensureCanLeave())) return
  const t = model.t
  const item: MacroItem = { id: nextId(model.macros), name: t.get('Macro_DefaultName'), alias: '', delay_sec: 3, actions: [] }
  const macros = [...model.macros, item]
  set({ macros })
  if (!(await persist(macros))) return
  set({ activeId: item.id, selectedIds: [item.id], ...loadEditor(item) })
  status(t.format('Status_Created', item.id), 'ok')
}

export const cloneMacro = async (): Promise<void> => {
  const src = activeMacro()
  if (!src || isBusy() || !(await ensureCanLeave())) return
  const t = model.t
  const copy = cloneMacroItem(src)
  copy.id = nextId(model.macros)
  copy.name = t.format('Macro_CloneName', src.name)
  copy.alias = ''
  const macros = [...model.macros, copy]
  set({ macros })
  if (!(await persist(macros))) return
  set({ activeId: copy.id, selectedIds: [copy.id], ...loadEditor(copy) })
  status(t.format('Status_Cloned', copy.id), 'ok')
}
export const deleteSelectedMacros = async (): Promise<void> => {
  if (isBusy()) return
  const { macros, selectedIds, activeId, settings, t } = model
  const ids = selectedIds.length > 0 ? selectedIds : activeId != null ? [activeId] : []
  const targets = macros.filter((m) => ids.includes(m.id))
  if (targets.length === 0) return

  if (settings.confirm_before_delete) {
    const choice = await ask({
      title: t.get('Dialog_DeleteMacroTitle'),
      body:
        targets.length === 1
          ? t.format('Dialog_DeleteMacroOne', targets[0].id, targets[0].name)
          : t.format('Dialog_DeleteMacroMany', targets.length),
      buttons: [
        { id: 'delete', label: t.get('Dialog_Delete'), kind: 'danger' },
        { id: 'cancel', label: t.get('Dialog_Cancel') }
      ],
      defaultId: 'cancel',
      cancelId: 'cancel'
    })
    if (choice !== 'delete') return
  }

  const remaining = macros.filter((m) => !ids.includes(m.id))
  set({ macros: remaining })
  if (!(await persist(remaining))) return
  const first = remaining[0] ?? null
  set({ activeId: first?.id ?? null, selectedIds: first ? [first.id] : [], ...loadEditor(first) })
  status(
    targets.length === 1 ? t.format('Status_DeletedOne', targets[0].id) : t.format('Status_DeletedMany', targets.length),
    'ok'
  )
}

export const loadSamples = async (): Promise<void> => {
  if (isBusy() || !(await ensureCanLeave())) return
  const t = model.t
  if (model.macros.length > 0) {
    const choice = await ask({
      title: t.get('Dialog_LoadSampleTitle'),
      body: t.format('Dialog_LoadSampleBody', 3),
      buttons: [
        { id: 'load', label: t.get('Dialog_Load'), kind: 'primary' },
        { id: 'cancel', label: t.get('Dialog_Cancel') }
      ],
      defaultId: 'cancel',
      cancelId: 'cancel'
    })
    if (choice !== 'load') return
  }
  const r = await window.api.loadSamples()
  if (!r.ok) {
    status(t.format('Status_SaveFailed', model.configPath, r.logPath ? t.format('Status_LogHint', r.logPath) : t.get('Status_LogMissing')), 'error')
    return
  }
  const first = r.macros[0] ?? null
  set({ macros: r.macros, activeId: first?.id ?? null, selectedIds: first ? [first.id] : [], ...loadEditor(first) })
  status(t.format('Status_SampleLoaded', r.macros.length, model.configPath), 'ok')
}

export const reorderMacros = async (from: number, to: number): Promise<void> => {
  if (isBusy()) return
  const macros = reorder(model.macros, from, to)
  set({ macros })
  if (await persist(macros)) status(model.t.get('Status_MacroOrderSaved'), 'ok')
}

// ------------------------------------------------------------------- editor

export const updateDraft = (patch: Partial<Omit<Draft, 'actions'>>): void => {
  if (model.draft) set({ draft: { ...model.draft, ...patch } })
}

export const trySave = async (): Promise<boolean> => {
  const current = activeMacro()
  const { draft, macros, t } = model
  if (!current || !draft || isBusy()) return false
  const v = validateDraft(draft, macros, current.id, t)
  if (!v.ok) {
    status(v.message, 'error')
    return false
  }
  const next = macros.map((m) => (m.id === current.id ? v.macro : m))
  const previous = macros
  set({ macros: next, activeId: v.macro.id, selectedIds: model.selectedIds.map((x) => (x === current.id ? v.macro.id : x)) })
  if (!(await persist(next))) {
    set({ macros: previous, activeId: current.id })
    return false
  }
  set({ draft: draftFromMacro(v.macro, t) })
  status(
    v.macro.alias === ''
      ? t.format('Status_Saved', model.configPath)
      : t.format('Status_SavedWithAlias', v.macro.alias, model.configPath),
    'ok'
  )
  return true
}

export const discardEdits = (): void => {
  if (!activeMacro()) return
  set(loadEditor(activeMacro()))
  status(model.t.get('Status_Discarded'))
}

const selectedSet = (): ReadonlySet<string> => new Set(model.selectedActions)

export const selectAction = (uid: string, mode: 'single' | 'toggle' | 'range'): void => {
  const { draft, selectedActions } = model
  if (!draft) return
  if (mode === 'toggle') {
    set({ selectedActions: selectedActions.includes(uid) ? selectedActions.filter((x) => x !== uid) : [...selectedActions, uid] })
  } else if (mode === 'range' && selectedActions.length > 0) {
    const anchor = draft.actions.findIndex((a) => a.uid === selectedActions[selectedActions.length - 1])
    const to = draft.actions.findIndex((a) => a.uid === uid)
    if (anchor >= 0 && to >= 0) {
      const [lo, hi] = anchor < to ? [anchor, to] : [to, anchor]
      set({ selectedActions: draft.actions.slice(lo, hi + 1).map((a) => a.uid) })
    }
  } else {
    set({ selectedActions: [uid] })
  }
}

export const addAction = (type: string): void => {
  if (!model.draft || isBusy()) return
  const r = insertAction(model.draft.actions, selectedSet(), type, model.t)
  set({ draft: { ...model.draft, actions: r.actions }, selectedActions: [r.added.uid] })
}

export const wrapSelectionInRepeat = (): void => {
  if (!model.draft || isBusy()) return
  const r = wrapInRepeat(model.draft.actions, selectedSet())
  set({ draft: { ...model.draft, actions: r.actions }, selectedActions: [r.start.uid] })
  status(
    r.range ? model.t.format('Status_WrappedRepeat', r.range.first, r.range.last) : model.t.get('Status_EmptyRepeatInserted')
  )
}

export const setActionField = (uid: string, patch: { type?: string; value?: string }): void => {
  if (!model.draft) return
  const actions = model.draft.actions.map((a) => {
    if (a.uid !== uid) return a
    const withType = patch.type !== undefined && patch.type !== a.type ? changeActionType(a, patch.type, model.t) : a
    return patch.value !== undefined ? { ...withType, value: patch.value } : withType
  })
  set({ draft: { ...model.draft, actions } })
}

export const moveSelectedActions = (delta: -1 | 1): void => {
  if (!model.draft || isBusy()) return
  set({ draft: { ...model.draft, actions: moveSelected(model.draft.actions, selectedSet(), delta) } })
}

export const reorderActions = (from: number, to: number): void => {
  if (!model.draft || isBusy()) return
  set({ draft: { ...model.draft, actions: reorder(model.draft.actions, from, to) } })
  status(model.t.get('Status_ActionOrderChanged'))
}

export const removeSelectedActions = async (): Promise<void> => {
  const { draft, selectedActions, settings, t } = model
  if (!draft || selectedActions.length === 0 || isBusy()) return
  const targets = draft.actions.map((a, i) => ({ a, i })).filter(({ a }) => selectedActions.includes(a.uid))
  if (targets.length === 0) return

  if (settings.confirm_before_delete) {
    const first = targets[0]
    const choice = await ask({
      title: t.get('Dialog_DeleteActionTitle'),
      body:
        targets.length === 1
          ? t.format('Dialog_DeleteActionOne', first.i + 1, t.get(`ActionType_${first.a.type}`))
          : t.format('Dialog_DeleteActionMany', targets.length),
      buttons: [
        { id: 'delete', label: t.get('Dialog_Delete'), kind: 'danger' },
        { id: 'cancel', label: t.get('Dialog_Cancel') }
      ],
      defaultId: 'cancel',
      cancelId: 'cancel'
    })
    if (choice !== 'delete') return
  }
  const actions = removeActions(draft.actions, new Set(selectedActions))
  const last = actions[actions.length - 1]
  set({ draft: { ...draft, actions }, selectedActions: last ? [last.uid] : [] })
}

// -------------------------------------------------------------------- 実行

export const toggleTestRun = async (): Promise<void> => {
  const t = model.t
  if (model.run.phase === 'running') {
    set({ run: { ...model.run, phase: 'stopping' } })
    status(t.get('Status_Stopping'))
    await window.api.stopRun()
    return
  }
  const { draft } = model
  if (!draft || model.run.phase !== 'idle') return
  const macro = draftToMacro(draft)

  const choice = await ask({
    title: t.get('Dialog_TestTitle'),
    body: t.format('Dialog_TestBody', macro.delay_sec.toLocaleString(model.t.language, { maximumFractionDigits: 2 })),
    buttons: [
      { id: 'run', label: t.get('Dialog_Run'), kind: 'primary' },
      { id: 'cancel', label: t.get('Dialog_Cancel') }
    ],
    defaultId: 'run',
    cancelId: 'cancel'
  })
  if (choice !== 'run') return

  set({ run: { phase: 'running', startedAt: Date.now(), delaySec: macro.delay_sec } })
  status(t.get('Status_Running'))
  const result = await window.api.runMacro(macro, model.settings.action_delay_sec)
  set({ run: { phase: 'idle', startedAt: 0, delaySec: 0 } })

  if (result.status === 'done') status(t.get('Status_RunDone'), 'ok')
  else if (result.status === 'stopped') status(t.get('Status_RunStopped'))
  else {
    status(result.logPath ? t.format('Status_RunErrorLog', result.logPath) : t.get('Status_RunErrorNoLog'), 'error')
    await ask({
      title: t.get('Dialog_RunErrorTitle'),
      body: result.message,
      buttons: [{ id: 'ok', label: t.get('Dialog_Ok'), kind: 'primary' }],
      defaultId: 'ok',
      cancelId: 'ok'
    })
  }
}

// ----------------------------------------------------------------- 設定など

export const updateSettings = async (patch: Partial<AppSettings>): Promise<void> => {
  const settings = { ...model.settings, ...patch }
  set({ settings })
  await window.api.saveSettings(settings)
}

export const changeLanguage = async (preference: string): Promise<void> => {
  if (isBusy() || preference === model.settings.ui_language) return
  if (!(await ensureCanLeave())) return
  const t = createTranslator(preference, model.systemLocale)
  await updateSettings({ ui_language: preference })
  await window.api.setLanguage(preference)
  // 言語に依存する既定値（確認ダイアログ文など）を、開いているマクロへ反映し直す
  set({ t, ...loadEditor(activeMacro()) })
  status(t.get('Status_Ready'))
}

export const openDataFolder = async (): Promise<void> => {
  const r = await window.api.openDataFolder()
  const t = model.t
  status(r.ok ? t.format('Status_FolderOpened', r.path) : t.format('Status_FolderOpenFailed', r.path), r.ok ? 'info' : 'error')
}

export const copyLaunchCommand = async (): Promise<void> => {
  const m = activeMacro()
  const r = await window.api.copyLaunchCommand(m?.alias ?? '', m?.id ?? 0)
  const t = model.t
  status(r.ok ? t.format('Status_Copied', r.text) : t.get('Status_CopyFailed'), r.ok ? 'ok' : 'error')
}

export const setPalette = (open: boolean): void => set({ paletteOpen: open })
export const setSettingsOpen = (open: boolean): void => set({ settingsOpen: open })

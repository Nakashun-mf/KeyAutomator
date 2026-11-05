import type { MacroItem } from './types'

// renderer からも使う、ファイル I/O を含まない検索ヘルパー
export const findById = (macros: readonly MacroItem[], id: number): MacroItem | undefined =>
  macros.find((m) => m.id === id)

export const findByName = (macros: readonly MacroItem[], name: string): MacroItem | undefined =>
  macros.find((m) => m.name.toLowerCase() === name.toLowerCase())

export const findByAlias = (macros: readonly MacroItem[], alias: string): MacroItem | undefined =>
  macros.find((m) => m.alias.trim() !== '' && m.alias.toLowerCase() === alias.toLowerCase())

export const nextId = (macros: readonly MacroItem[]): number =>
  macros.length === 0 ? 1 : Math.max(...macros.map((m) => m.id)) + 1

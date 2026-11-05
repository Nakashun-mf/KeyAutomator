import type { InputBackend } from '../core/runner'

/**
 * Win32 `SendInput` を koffi 経由で呼ぶ。Windows 以外では生成できない。
 * 構造体定義（defineInputTypes）は OS 非依存なので、レイアウトは Linux 上のテストでも検証できる。
 */

// koffi の型は動的ロードするため最小限だけ宣言する
interface Koffi {
  struct(name: string, def: Record<string, unknown>): unknown
  union(name: string, def: Record<string, unknown>): unknown
  sizeof(type: unknown): number
  load(lib: string): { func(decl: string): (...args: never[]) => unknown }
  pointer(type: unknown): unknown
  array(type: unknown, len: number): unknown
}

export const INPUT_MOUSE = 0
export const INPUT_KEYBOARD = 1
export const KEYEVENTF_EXTENDEDKEY = 0x0001
export const KEYEVENTF_KEYUP = 0x0002
export const KEYEVENTF_UNICODE = 0x0004
export const MOUSEEVENTF_LEFTDOWN = 0x0002
export const MOUSEEVENTF_LEFTUP = 0x0004
export const MOUSEEVENTF_RIGHTDOWN = 0x0008
export const MOUSEEVENTF_RIGHTUP = 0x0010
export const MOUSEEVENTF_MIDDLEDOWN = 0x0020
export const MOUSEEVENTF_MIDDLEUP = 0x0040
export const MAPVK_VK_TO_VSC = 0

export interface InputTypes {
  INPUT: unknown
  /** INPUT 構造体のバイト数（x64 で 40, x86 で 28）。 */
  size: number
}

const defined = new WeakMap<object, InputTypes>()

/** koffi は型名がグローバルなので、同じインスタンスには一度だけ定義する。 */
export const defineInputTypes = (koffi: Koffi): InputTypes => {
  const cached = defined.get(koffi)
  if (cached) return cached
  const KEYBDINPUT = koffi.struct('KEYBDINPUT', {
    wVk: 'uint16',
    wScan: 'uint16',
    dwFlags: 'uint32',
    time: 'uint32',
    dwExtraInfo: 'uintptr_t'
  })
  const MOUSEINPUT = koffi.struct('MOUSEINPUT', {
    dx: 'int32',
    dy: 'int32',
    mouseData: 'uint32',
    dwFlags: 'uint32',
    time: 'uint32',
    dwExtraInfo: 'uintptr_t'
  })
  const InputUnion = koffi.union('INPUT_UNION', { mi: MOUSEINPUT, ki: KEYBDINPUT })
  const INPUT = koffi.struct('INPUT', { type: 'uint32', u: InputUnion })
  const types = { INPUT, size: koffi.sizeof(INPUT) }
  defined.set(koffi, types)
  return types
}

export interface KeyboardInputValue {
  type: typeof INPUT_KEYBOARD
  u: { ki: { wVk: number; wScan: number; dwFlags: number; time: number; dwExtraInfo: number } }
}
export interface MouseInputValue {
  type: typeof INPUT_MOUSE
  u: { mi: { dx: number; dy: number; mouseData: number; dwFlags: number; time: number; dwExtraInfo: number } }
}

export const keyboardInput = (wVk: number, wScan: number, dwFlags: number): KeyboardInputValue => ({
  type: INPUT_KEYBOARD,
  u: { ki: { wVk, wScan, dwFlags, time: 0, dwExtraInfo: 0 } }
})

export const mouseInput = (dwFlags: number): MouseInputValue => ({
  type: INPUT_MOUSE,
  u: { mi: { dx: 0, dy: 0, mouseData: 0, dwFlags, time: 0, dwExtraInfo: 0 } }
})

const MOUSE_FLAGS = {
  left: [MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP],
  right: [MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP],
  middle: [MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP]
} as const

/** 本物の SendInput バックエンド。Windows 専用。 */
export const SENDINPUT_PROTOTYPE = 'uint32 __stdcall SendInput(uint32 cInputs, INPUT *pInputs, int cbSize)'
export const MAPVIRTUALKEY_PROTOTYPE = 'uint32 __stdcall MapVirtualKeyW(uint32 uCode, uint32 uMapType)'

export const createWin32Input = async (): Promise<InputBackend> => {
  if (process.platform !== 'win32') throw new Error('SendInput is only available on Windows')
  const koffi = (await import('koffi')).default as unknown as Koffi
  // INPUT は名前で登録されるので、下のプロトタイプ文字列から参照できる
  const { size } = defineInputTypes(koffi)
  const user32 = koffi.load('user32.dll')
  const SendInput = user32.func(SENDINPUT_PROTOTYPE) as unknown as (
    n: number,
    inputs: unknown[],
    cb: number
  ) => number
  const MapVirtualKeyW = user32.func(MAPVIRTUALKEY_PROTOTYPE) as unknown as (
    code: number,
    type: number
  ) => number

  const send = (input: KeyboardInputValue | MouseInputValue): boolean => SendInput(1, [input], size) === 1

  return {
    key: (vk, down, extended) => {
      const flags = (down ? 0 : KEYEVENTF_KEYUP) | (extended ? KEYEVENTF_EXTENDEDKEY : 0)
      return send(keyboardInput(vk, MapVirtualKeyW(vk, MAPVK_VK_TO_VSC) & 0xffff, flags))
    },
    unicode: (codeUnit, down) =>
      send(keyboardInput(0, codeUnit, KEYEVENTF_UNICODE | (down ? 0 : KEYEVENTF_KEYUP))),
    mouse: (button, down) => send(mouseInput(MOUSE_FLAGS[button][down ? 0 : 1]))
  }
}

/** CLI の -h 出力用。GUI サブシステムの exe でも親コンソールへ書く（AttachConsole）。 */
export const writeToParentConsole = async (text: string): Promise<boolean> => {
  if (process.platform !== 'win32') return false
  try {
    const koffi = (await import('koffi')).default as unknown as Koffi
    const kernel32 = koffi.load('kernel32.dll')
    const AttachConsole = kernel32.func('bool __stdcall AttachConsole(uint32 dwProcessId)') as unknown as (
      pid: number
    ) => boolean
    AttachConsole(0xffffffff) // ATTACH_PARENT_PROCESS
    const fs = await import('node:fs')
    const fd = fs.openSync('CONOUT$', 'w')
    try {
      fs.writeSync(fd, text.endsWith('\n') ? text : text + '\n')
    } finally {
      fs.closeSync(fd)
    }
    return true
  } catch {
    return false
  }
}

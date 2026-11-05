/** Win32 Virtual-Key コード。0 は「解決できない」を表す。 */
export const VK_NONE = 0
export const VK = {
  BACK: 0x08,
  TAB: 0x09,
  RETURN: 0x0d,
  SHIFT: 0x10,
  CONTROL: 0x11,
  MENU: 0x12,
  PAUSE: 0x13,
  CAPITAL: 0x14,
  ESCAPE: 0x1b,
  SPACE: 0x20,
  PRIOR: 0x21,
  NEXT: 0x22,
  END: 0x23,
  HOME: 0x24,
  LEFT: 0x25,
  UP: 0x26,
  RIGHT: 0x27,
  DOWN: 0x28,
  SNAPSHOT: 0x2c,
  INSERT: 0x2d,
  DELETE: 0x2e,
  LWIN: 0x5b,
  RWIN: 0x5c,
  APPS: 0x5d,
  NUMPAD0: 0x60,
  MULTIPLY: 0x6a,
  ADD: 0x6b,
  SEPARATOR: 0x6c,
  SUBTRACT: 0x6d,
  DECIMAL: 0x6e,
  DIVIDE: 0x6f,
  F1: 0x70,
  NUMLOCK: 0x90,
  SCROLL: 0x91
} as const

const KEY_MAP = new Map<string, number>()
const add = (code: number, ...names: string[]): void => {
  for (const n of names) KEY_MAP.set(n, code)
}

add(VK.RETURN, 'ENTER', 'RETURN')
add(VK.TAB, 'TAB')
add(VK.ESCAPE, 'ESC', 'ESCAPE')
add(VK.BACK, 'BACKSPACE', 'BS')
add(VK.DELETE, 'DELETE', 'DEL')
add(VK.INSERT, 'INSERT', 'INS')
add(VK.HOME, 'HOME')
add(VK.END, 'END')
add(VK.PRIOR, 'PAGEUP', 'PGUP')
add(VK.NEXT, 'PAGEDOWN', 'PGDN')
add(VK.UP, 'UP')
add(VK.DOWN, 'DOWN')
add(VK.LEFT, 'LEFT')
add(VK.RIGHT, 'RIGHT')
add(VK.SPACE, 'SPACE')
add(VK.CONTROL, 'CTRL', 'CONTROL', 'CTL')
add(VK.SHIFT, 'SHIFT')
add(VK.MENU, 'ALT', 'MENU')
add(VK.LWIN, 'LWIN', 'WIN', 'WINDOWS')
add(VK.RWIN, 'RWIN')
add(VK.APPS, 'APPS')
add(VK.CAPITAL, 'CAPITAL', 'CAPSLOCK', 'CAPS')
add(VK.NUMLOCK, 'NUMLOCK')
add(VK.SCROLL, 'SCROLL', 'SCROLLLOCK')
add(VK.SNAPSHOT, 'SNAPSHOT', 'PRINTSCREEN', 'PRTSC')
add(VK.PAUSE, 'PAUSE')
for (let i = 1; i <= 24; i++) add(VK.F1 + i - 1, `F${i}`)
for (let i = 0; i <= 9; i++) add(VK.NUMPAD0 + i, `NUMPAD${i}`)
add(VK.MULTIPLY, 'MULTIPLY')
add(VK.ADD, 'ADD')
add(VK.SEPARATOR, 'SEPARATOR')
add(VK.SUBTRACT, 'SUBTRACT')
add(VK.DECIMAL, 'DECIMAL')
add(VK.DIVIDE, 'DIVIDE')
add(0xba, 'OEM_1')
add(0xbb, 'OEM_PLUS')
add(0xbc, 'OEM_COMMA')
add(0xbd, 'OEM_MINUS')
add(0xbe, 'OEM_PERIOD')
add(0xbf, 'OEM_2')
add(0xc0, 'OEM_3')
add(0xdb, 'OEM_4')
add(0xdc, 'OEM_5')
add(0xdd, 'OEM_6')
add(0xde, 'OEM_7')
add(0xe2, 'OEM_102')
add(0xa6, 'BROWSER_BACK')
add(0xa7, 'BROWSER_FORWARD')
add(0xa8, 'BROWSER_REFRESH')
add(0xa9, 'BROWSER_STOP')
add(0xaa, 'BROWSER_SEARCH')
add(0xab, 'BROWSER_FAVORITES')
add(0xac, 'BROWSER_HOME')
add(0xad, 'VOLUME_MUTE')
add(0xae, 'VOLUME_DOWN')
add(0xaf, 'VOLUME_UP')
add(0xb0, 'MEDIA_NEXT_TRACK')
add(0xb1, 'MEDIA_PREV_TRACK')
add(0xb2, 'MEDIA_STOP')
add(0xb3, 'MEDIA_PLAY_PAUSE')
add(0xb4, 'LAUNCH_MAIL')
add(0xb5, 'LAUNCH_MEDIA_SELECT')
add(0xb6, 'LAUNCH_APP1')
add(0xb7, 'LAUNCH_APP2')

/** 拡張キー（KEYEVENTF_EXTENDEDKEY を付ける）。 */
const EXTENDED_KEYS = new Set<number>([
  VK.UP,
  VK.DOWN,
  VK.LEFT,
  VK.RIGHT,
  VK.HOME,
  VK.END,
  VK.PRIOR,
  VK.NEXT,
  VK.INSERT,
  VK.DELETE,
  VK.LWIN,
  VK.RWIN,
  VK.APPS,
  VK.NUMLOCK,
  VK.DIVIDE
])

export const isExtendedKey = (vk: number): boolean => EXTENDED_KEYS.has(vk)

/** 名前の一覧（テスト用）。 */
export const mappedKeyNames = (): string[] => [...KEY_MAP.keys()]

/** キー名（ENTER, A, F5 …）を Virtual-Key へ。解決できなければ 0。 */
export const resolveKey = (name: string | null | undefined): number => {
  if (name == null || name.trim() === '') return VK_NONE
  const n = name.trim().toUpperCase()
  const mapped = KEY_MAP.get(n)
  if (mapped !== undefined) return mapped
  if (n.length === 1) {
    const c = n.charCodeAt(0)
    if ((c >= 0x41 && c <= 0x5a) || (c >= 0x30 && c <= 0x39)) return c
  }
  return VK_NONE
}

export type ModifierName = 'CTRL' | 'ALT' | 'SHIFT' | 'WIN'

/** ホットキー文字列の修飾キー名なら VK を返す。 */
export const resolveModifier = (part: string): number => {
  switch (part.toUpperCase()) {
    case 'CTRL':
    case 'CONTROL':
    case 'CTL':
      return VK.CONTROL
    case 'ALT':
    case 'MENU':
      return VK.MENU
    case 'SHIFT':
      return VK.SHIFT
    case 'WIN':
    case 'WINDOWS':
    case 'LWIN':
      return VK.LWIN
    default:
      return VK_NONE
  }
}

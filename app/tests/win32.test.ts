import { describe, expect, it } from 'vitest'
import { createRequire } from 'node:module'
import {
  MAPVIRTUALKEY_PROTOTYPE,
  SENDINPUT_PROTOTYPE,
  defineInputTypes,
  keyboardInput,
  mouseInput,
  KEYEVENTF_KEYUP,
  KEYEVENTF_UNICODE
} from '../src/native/win32'

const require = createRequire(import.meta.url)

// koffi は Linux でも構造体のエンコード／デコードができるので、Win32 の INPUT レイアウトをここで検証する
describe('win32 INPUT struct layout (x64)', () => {
  const koffi = require('koffi')
  const { INPUT, size } = defineInputTypes(koffi)
  const encode = (value: unknown): Buffer => {
    const buf = Buffer.alloc(size)
    koffi.encode(buf, INPUT, value)
    return buf
  }

  it.runIf(process.arch === 'x64' || process.arch === 'arm64')('INPUT is 40 bytes on 64-bit', () => {
    expect(size).toBe(40)
  })

  it.runIf(process.arch === 'x64' || process.arch === 'arm64')('keyboard input puts fields at the Win32 offsets', () => {
    const b = encode(keyboardInput(0x41, 0x1e, KEYEVENTF_KEYUP))
    expect(b.readUInt32LE(0)).toBe(1) // type = INPUT_KEYBOARD
    expect(b.readUInt16LE(8)).toBe(0x41) // wVk
    expect(b.readUInt16LE(10)).toBe(0x1e) // wScan
    expect(b.readUInt32LE(12)).toBe(KEYEVENTF_KEYUP) // dwFlags
  })

  it.runIf(process.arch === 'x64' || process.arch === 'arm64')('unicode input carries the code unit in wScan', () => {
    const b = encode(keyboardInput(0, 0x3042, KEYEVENTF_UNICODE))
    expect(b.readUInt16LE(8)).toBe(0)
    expect(b.readUInt16LE(10)).toBe(0x3042)
    expect(b.readUInt32LE(12)).toBe(KEYEVENTF_UNICODE)
  })

  it.runIf(process.arch === 'x64' || process.arch === 'arm64')('mouse input puts dwFlags at offset 20', () => {
    const b = encode(mouseInput(0x0002))
    expect(b.readUInt32LE(0)).toBe(0) // INPUT_MOUSE
    expect(b.readUInt32LE(20)).toBe(0x0002) // MOUSEINPUT.dwFlags (after dx,dy,mouseData)
  })
})

describe('win32 prototypes', () => {
  it('SendInput / MapVirtualKeyW prototype strings parse against the INPUT type', () => {
    const koffi = require('koffi')
    defineInputTypes(koffi)
    // 実際の user32 は Windows でしか無いので、宣言文字列だけを検証する
    expect(() => koffi.proto(SENDINPUT_PROTOTYPE)).not.toThrow()
    expect(() => koffi.proto(MAPVIRTUALKEY_PROTOTYPE)).not.toThrow()
  })
})

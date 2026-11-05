import { describe, expect, it } from 'vitest'
import { CancelledError } from '../src/core/runner'
import { captureRunner } from './helpers'

const a = (type: string, value = '') => ({ type, value })
const uni = (cap: { events: string[] }) =>
  cap.events.filter((e) => e.startsWith('uni:') && e.endsWith(':dn')).map((e) => e.split(':')[1]).join('')

describe('runner: text', () => {
  it('sends UTF-16 code units, Enter for LF, ignores CR', async () => {
    const { runner, cap } = captureRunner()
    await runner.sendText('aあ\r\nb')
    expect(uni(cap)).toBe('aあb')
    expect(cap.events).toContain('key:d:dn') // ENTER
    expect(cap.events.filter((e) => e.startsWith('key:d:'))).toHaveLength(2)
  })

  it('splits surrogate pairs into two units like the C# char loop', async () => {
    const { runner, cap } = captureRunner()
    await runner.sendText('😀')
    expect(cap.events.filter((e) => e.endsWith(':dn'))).toHaveLength(2)
  })

  it('empty text sends nothing', async () => {
    const { runner, cap } = captureRunner()
    await runner.sendText('')
    expect(cap.events).toEqual([])
  })
})

describe('runner: keys / hotkeys / mouse', () => {
  it('key down then up, extended flag for arrows', async () => {
    const { runner, cap } = captureRunner()
    await runner.sendKey('UP')
    expect(cap.events).toEqual(['key:26:dn:ext', 'key:26:up:ext'])
  })

  it('unknown key is logged, nothing sent', async () => {
    const { runner, cap } = captureRunner()
    await runner.sendKey('nope')
    expect(cap.events).toEqual([])
    expect(cap.logs[0]).toContain('nope')
  })

  it('hotkey presses modifiers in order and releases in reverse (no stuck keys)', async () => {
    const { runner, cap } = captureRunner()
    await runner.sendHotkey('CTRL+SHIFT+S')
    expect(cap.events).toEqual([
      'key:11:dn', 'key:10:dn', 'key:53:dn', 'key:53:up', 'key:10:up', 'key:11:up'
    ])
  })

  it('hotkey without a main key logs and sends nothing', async () => {
    const { runner, cap } = captureRunner()
    await runner.sendHotkey('CTRL+ALT')
    expect(cap.events).toEqual([])
    expect(cap.logs).toHaveLength(1)
  })

  it('mouse variants', async () => {
    const { runner, cap } = captureRunner()
    await runner.sendMouse('right')
    await runner.sendMouse('LEFT_DOUBLE')
    await runner.sendMouse('bogus')
    expect(cap.events).toEqual([
      'mouse:right:dn', 'mouse:right:up',
      'mouse:left:dn', 'mouse:left:up', 'mouse:left:dn', 'mouse:left:up'
    ])
    expect(cap.logs).toHaveLength(1)
  })
})

describe('runner: actions', () => {
  it('wait sleeps in ms; invalid wait is logged', async () => {
    const { runner, cap } = captureRunner()
    await runner.executeAction(a('wait', '1.5'))
    expect(cap.sleeps).toEqual([1500])
    await runner.executeAction(a('wait', 'x'))
    expect(cap.logs).toHaveLength(1)
  })

  it('dialog shows the message (default when blank) and awaits OK', async () => {
    const { runner, cap } = captureRunner()
    await runner.executeAction(a('dialog', '  hello  '))
    await runner.executeAction(a('dialog', ''))
    expect(cap.dialogs[0]).toBe('hello')
    expect(cap.dialogs[1]).toBe('Press OK to continue to the next step.')
  })

  it('unknown type is logged', async () => {
    const { runner, cap } = captureRunner()
    await runner.executeAction(a('zzz'))
    expect(cap.logs[0]).toContain('zzz')
  })
})

describe('runner: repeats', () => {
  const run = async (actions: { type: string; value: string }[], stepDelayMs = 0) => {
    const { runner, cap } = captureRunner()
    await runner.executeActions(actions, stepDelayMs)
    return { cap, text: uni(cap) }
  }

  it('simple loop runs body count times', async () => {
    const r = await run([a('repeat', '3'), a('text', 'x'), a('end_repeat')])
    expect(r.text).toBe('xxx')
  })

  it('nested loops multiply', async () => {
    const r = await run([
      a('repeat', '2'), a('text', 'a'), a('repeat', '3'), a('text', 'b'), a('end_repeat'), a('end_repeat')
    ])
    expect(r.text).toBe('abbbabbb')
  })

  it('empty body is skipped without error', async () => {
    const r = await run([a('text', 'p'), a('repeat', '5'), a('end_repeat'), a('text', 'q')])
    expect(r.text).toBe('pq')
    expect(r.cap.logs).toEqual([])
  })

  it('unclosed repeat logs and stops; orphan end is skipped', async () => {
    const r1 = await run([a('text', 'a'), a('repeat', '2'), a('text', 'b')])
    expect(r1.text).toBe('a')
    expect(r1.cap.logs).toHaveLength(1)
    const r2 = await run([a('end_repeat'), a('text', 'z')])
    expect(r2.text).toBe('z')
    expect(r2.cap.logs).toHaveLength(1)
  })

  it('invalid repeat count is logged and the marker skipped', async () => {
    const r = await run([a('repeat', 'bad'), a('text', 'k'), a('end_repeat')])
    expect(r.text).toBe('k')
    expect(r.cap.logs.length).toBeGreaterThanOrEqual(1)
  })

  it('step delay: between steps and loop turns, never trailing', async () => {
    const r = await run([a('text', 'a'), a('repeat', '2'), a('text', 'b'), a('end_repeat')], 200)
    // a | gap | b | gap(next lap) | b   -> text sleeps of 5ms are separate
    expect(r.cap.sleeps.filter((s) => s === 200)).toHaveLength(3)
    expect(r.cap.sleeps[r.cap.sleeps.length - 1]).toBe(5) // 末尾に余分な間隔なし
  })

  it('no step delay after the last step of a plain list', async () => {
    const { runner, cap } = captureRunner()
    await runner.executeActions([a('key', 'A'), a('key', 'B')], 300)
    expect(cap.sleeps.filter((s) => s === 300)).toHaveLength(1)
  })
})

describe('runner: executeMacro & cancellation', () => {
  it('waits delay_sec first, then runs', async () => {
    const { runner, cap } = captureRunner()
    await runner.executeMacro({ id: 1, name: 'm', alias: '', delay_sec: 2.5, actions: [a('key', 'A')] }, 0.2)
    expect(cap.sleeps[0]).toBe(2500)
    expect(cap.events).toHaveLength(2)
  })

  it('aborted signal throws CancelledError before sending anything', async () => {
    const { runner, cap } = captureRunner()
    const ac = new AbortController()
    ac.abort()
    await expect(runner.executeActions([a('key', 'A')], 0, ac.signal)).rejects.toBeInstanceOf(CancelledError)
    expect(cap.events).toEqual([])
  })

  it('cancelling mid-run stops at the next step', async () => {
    const { runner, cap } = captureRunner()
    const ac = new AbortController()
    const actions = [a('text', 'a'), a('dialog', 'x'), a('text', 'b')]
    // ダイアログ表示をきっかけに中断
    const p = runner.executeActions(actions, 0, ac.signal)
    ac.abort()
    await expect(p).rejects.toBeInstanceOf(CancelledError)
    expect(cap.events.join()).not.toContain('uni:b')
  })
})

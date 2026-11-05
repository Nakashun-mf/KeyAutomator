import { describe, expect, it } from 'vitest'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  canWriteToDirectory,
  isRestrictedInstallDirectory,
  migrateSidecarFiles,
  quoteCliPath,
  resolveDataDirectory
} from '../src/core/paths'

const tmp = (): string => mkdtempSync(join(tmpdir(), 'keyauto-paths-'))

describe('resolveDataDirectory', () => {
  it('portable: a writable exe folder holds the data', () => {
    const exe = tmp()
    const local = join(tmp(), 'KeyAutomator')
    const dir = resolveDataDirectory({ exeDir: exe, localDataDir: local, packaged: false, restrictedRoots: [] })
    expect(dir).toBe(exe)
    expect(existsSync(local)).toBe(false)
  })

  it('packaged (MSIX): always the user folder, and exe-side settings are migrated once', () => {
    const exe = tmp()
    const local = join(tmp(), 'KeyAutomator')
    writeFileSync(join(exe, 'config.json'), '[{"id":1}]')
    const dir = resolveDataDirectory({ exeDir: exe, localDataDir: local, packaged: true, restrictedRoots: [] })
    expect(dir).toBe(local)
    expect(readFileSync(join(local, 'config.json'), 'utf8')).toBe('[{"id":1}]')

    // 2 回目は上書きしない
    writeFileSync(join(local, 'config.json'), '[{"id":2}]')
    resolveDataDirectory({ exeDir: exe, localDataDir: local, packaged: true, restrictedRoots: [] })
    expect(readFileSync(join(local, 'config.json'), 'utf8')).toBe('[{"id":2}]')
  })

  it('a protected install folder (e.g. Program Files) falls back to the user folder', () => {
    const root = tmp()
    const exe = join(root, 'KeyAutomator')
    mkdirSync(exe)
    const local = join(tmp(), 'KeyAutomator')
    expect(resolveDataDirectory({ exeDir: exe, localDataDir: local, packaged: false, restrictedRoots: [root] })).toBe(local)
  })
})

describe('path helpers', () => {
  it('isRestrictedInstallDirectory matches roots and their children only', () => {
    const roots = ['/opt/pf']
    expect(isRestrictedInstallDirectory('/opt/pf', roots)).toBe(true)
    expect(isRestrictedInstallDirectory('/opt/pf/app/sub', roots)).toBe(true)
    expect(isRestrictedInstallDirectory('/opt/pf-other', roots)).toBe(false)
    expect(isRestrictedInstallDirectory('/home/u/app', roots)).toBe(false)
    expect(isRestrictedInstallDirectory(undefined, roots)).toBe(true)
    expect(isRestrictedInstallDirectory('/x/WindowsApps/Pkg_1.0', [])).toBe(true)
  })

  it('migrateSidecarFiles ignores identical dirs and missing sources', () => {
    const a = tmp()
    expect(() => migrateSidecarFiles(a, a)).not.toThrow()
    expect(() => migrateSidecarFiles(join(a, 'nope'), tmp())).not.toThrow()
  })

  it('canWriteToDirectory creates the folder and leaves no probe behind', () => {
    const d = join(tmp(), 'new')
    expect(canWriteToDirectory(d)).toBe(true)
    expect(existsSync(d)).toBe(true)
  })

  it('quoteCliPath quotes and rejects quotes', () => {
    expect(quoteCliPath('C:\\a b\\x.exe')).toBe('"C:\\a b\\x.exe"')
    expect(() => quoteCliPath('a"b')).toThrow()
    expect(() => quoteCliPath('  ')).toThrow()
  })
})

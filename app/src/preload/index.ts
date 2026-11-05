import { contextBridge, ipcRenderer } from 'electron'
import { IPC, type KeyAutomatorApi } from '../shared/api'

const api: KeyAutomatorApi = {
  getState: () => ipcRenderer.invoke(IPC.getState),
  saveMacros: (macros) => ipcRenderer.invoke(IPC.saveMacros, macros),
  loadSamples: () => ipcRenderer.invoke(IPC.loadSamples),
  saveSettings: (settings) => ipcRenderer.invoke(IPC.saveSettings, settings),
  setLanguage: (preference) => ipcRenderer.invoke(IPC.setLanguage, preference),
  runMacro: (macro, delay) => ipcRenderer.invoke(IPC.runMacro, macro, delay),
  stopRun: () => ipcRenderer.invoke(IPC.stopRun),
  openDataFolder: () => ipcRenderer.invoke(IPC.openDataFolder),
  copyLaunchCommand: (alias, id) => ipcRenderer.invoke(IPC.copyLaunchCommand, alias, id),
  notifyReady: () => ipcRenderer.send(IPC.ready)
}

contextBridge.exposeInMainWorld('api', api)

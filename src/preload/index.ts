import { contextBridge, ipcRenderer } from 'electron'

const api = {
  // Store operations
  getConfig: (key: string, defaultValue: any) =>
    ipcRenderer.invoke('store:get', key, defaultValue),
  setConfig: (key: string, value: any) =>
    ipcRenderer.invoke('store:set', key, value),

  // Provider data operations
  getProviderData: () => ipcRenderer.invoke('provider:getData'),
  onProviderDataUpdate: (callback: (data: any) => void) => {
    ipcRenderer.on('provider:dataUpdated', (_, data) => callback(data))
    return () => ipcRenderer.removeAllListeners('provider:dataUpdated')
  },

  // Poller operations
  startPoller: () => ipcRenderer.invoke('poller:start'),
  stopPoller: () => ipcRenderer.invoke('poller:stop'),
  forceUpdate: () => ipcRenderer.invoke('poller:forceUpdate'),

  // Window operations
  minimizeWindow: () => ipcRenderer.invoke('window:minimize'),
  closeWindow: () => ipcRenderer.invoke('window:close')
}

contextBridge.exposeInMainWorld('tokenLuvApi', api)
contextBridge.exposeInMainWorld('ipcRenderer', ipcRenderer)

export type TokenLuvApi = typeof api

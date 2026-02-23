const { contextBridge, ipcRenderer } = require('electron')

console.log('[PRELOAD] Preload script is loading...')

try {
  const api = {
    // Store operations
    getConfig: (key, defaultValue) =>
      ipcRenderer.invoke('store:get', key, defaultValue),
    setConfig: (key, value) =>
      ipcRenderer.invoke('store:set', key, value),

    // Provider data operations
    getProviderData: () => ipcRenderer.invoke('provider:getData'),
    onProviderDataUpdate: (callback) => {
      ipcRenderer.on('provider:dataUpdated', (_, data) => callback(data))
      return () => ipcRenderer.removeAllListeners('provider:dataUpdated')
    },

    // Poller operations
    startPoller: () => ipcRenderer.invoke('poller:start'),
    stopPoller: () => ipcRenderer.invoke('poller:stop'),
    forceUpdate: () => ipcRenderer.invoke('poller:forceUpdate'),

    // Window operations
    minimizeWindow: () => ipcRenderer.invoke('window:minimize'),
    closeWindow: () => ipcRenderer.invoke('window:close'),
    resizeWindow: (count) => ipcRenderer.invoke('window:resize', count),
    openSettings: () => ipcRenderer.invoke('settings:open')
  }

  console.log('[PRELOAD] About to expose API to main world')
  contextBridge.exposeInMainWorld('tokenLuvApi', api)
  contextBridge.exposeInMainWorld('ipcRenderer', ipcRenderer)
  console.log('[PRELOAD] API exposed successfully')
} catch (error) {
  console.error('[PRELOAD] Error during setup:', error)
}

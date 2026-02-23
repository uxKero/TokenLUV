import { app, BrowserWindow, ipcMain } from 'electron'
import { join } from 'path'
import { isDev } from '@electron-toolkit/utils'
import Store from 'electron-store'
import { createTray } from './tray'
import { createWindow } from './window'
import { initPoller } from './poller'

const store = new Store()
let mainWindow: BrowserWindow | null = null
let tray: any = null

function createApp(): void {
  // Create main window
  mainWindow = createWindow()

  // Initialize tray
  tray = createTray(mainWindow)

  // Initialize poller
  initPoller()

  // Setup window IPC handlers
  setupIpcHandlers()
}

function setupIpcHandlers(): void {
  // Store handlers
  ipcMain.handle('store:get', (_event, key, defaultValue) => {
    return store.get(key, defaultValue)
  })

  ipcMain.handle('store:set', (_event, key, value) => {
    store.set(key, value)
    return true
  })

  // Provider data handler
  ipcMain.handle('provider:getData', () => {
    return store.get('providerData', {})
  })

  // Window handlers
  ipcMain.handle('window:minimize', () => {
    if (mainWindow) mainWindow.minimize()
  })

  ipcMain.handle('window:close', () => {
    if (mainWindow) mainWindow.hide()
  })

  // Navigation handler
  ipcMain.on('navigate:settings', () => {
    if (mainWindow) mainWindow.webContents.send('navigate:settings')
  })
}

app.whenReady().then(() => {
  createApp()

  app.on('activate', function () {
    if (BrowserWindow.getAllWindows().length === 0) createApp()
  })
})

app.on('window-all-closed', () => {
  // Don't quit the app, keep it in tray
  if (process.platform !== 'darwin') {
    // On Windows, we keep the app running in the tray
  }
})

process.on('uncaughtException', (error) => {
  console.error('Uncaught Exception:', error)
})

import { app, BrowserWindow, ipcMain } from 'electron'
import Store from 'electron-store'
import { join } from 'path'
import { createTray, updateTrayTooltip } from './tray'
import { createWindow } from './window'
import { initPoller } from './poller'
import { preloadPath } from './preload'

const store = new Store()
let mainWindow: BrowserWindow | null = null
let settingsWindow: BrowserWindow | null = null
let tray: any = null

function createApp(): void {
  mainWindow = createWindow()
  setupIpcHandlers()
  tray = createTray(mainWindow)
  initPoller((data, timestamp) => {
    if (tray) updateTrayTooltip(tray, data, timestamp)
  })
}

function openSettingsWindow(): void {
  // If already open, just focus it
  if (settingsWindow && !settingsWindow.isDestroyed()) {
    settingsWindow.show()
    settingsWindow.focus()
    return
  }

  settingsWindow = new BrowserWindow({
    width: 360,
    height: 490,
    show: false,
    resizable: false,
    frame: false,
    parent: mainWindow || undefined,
    webPreferences: {
      preload: preloadPath(),
      sandbox: true,
      contextIsolation: true,
      nodeIntegration: false
    }
  })

  const isDev = !app.isPackaged
  if (isDev) {
    const rendererUrl = process.env.ELECTRON_RENDERER_URL || 'http://localhost:5173'
    settingsWindow.loadURL(rendererUrl + '#settings')
  } else {
    settingsWindow.loadFile(join(__dirname, '../renderer/index.html'), { hash: 'settings' })
  }

  settingsWindow.on('ready-to-show', () => {
    settingsWindow?.show()
    settingsWindow?.focus()
  })

  settingsWindow.on('closed', () => {
    settingsWindow = null
  })
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

  ipcMain.handle('window:close', (event) => {
    // Smart close: hide main window, actually close settings window
    const sender = BrowserWindow.fromWebContents(event.sender)
    if (sender === mainWindow) {
      mainWindow?.hide()
    } else {
      sender?.close()
    }
  })

  // Dynamic resize: only applied to main widget window
  ipcMain.handle('window:resize', (_event, count: number) => {
    if (mainWindow) {
      // header(40) + rows(count×34) + footer(30) + padding(8), min 108px
      // rows are 34px each: 6px padding + ~10px text + ~8px subtext + gap
      const height = count > 0
        ? Math.max(108, 40 + count * 34 + 30 + 8)
        : 108
      mainWindow.setSize(480, height, false)
    }
  })

  // Open Settings as a separate window
  ipcMain.handle('settings:open', () => {
    openSettingsWindow()
  })

  // Navigation handler (from tray menu → navigate:settings)
  ipcMain.on('navigate:settings', () => {
    openSettingsWindow()
  })
}

app.whenReady().then(() => {
  createApp()
  app.on('activate', function () {
    if (BrowserWindow.getAllWindows().length === 0) createApp()
  })
})

app.on('window-all-closed', () => {
  // Keep app running in tray on Windows
})

process.on('uncaughtException', (error) => {
  console.error('Uncaught Exception:', error)
})

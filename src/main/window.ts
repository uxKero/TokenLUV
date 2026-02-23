import { BrowserWindow, app } from 'electron'
import { join } from 'path'
import { preloadPath } from './preload'

export function createWindow(): BrowserWindow {
  const mainWindow = new BrowserWindow({
    width: 420,
    height: 480,
    show: false,
    webPreferences: {
      preload: preloadPath(),
      sandbox: true,
      contextIsolation: true,
      enableRemoteModule: false,
      nodeIntegration: false
    }
  })

  const isDev = !app.isPackaged
  if (isDev && process.env.ELECTRON_RENDERER_URL) {
    mainWindow.loadURL(process.env.ELECTRON_RENDERER_URL)
    mainWindow.webContents.openDevTools()
  } else {
    mainWindow.loadFile(join(__dirname, '../renderer/index.html'))
  }

  mainWindow.on('ready-to-show', () => {
    mainWindow.show()
  })

  return mainWindow
}

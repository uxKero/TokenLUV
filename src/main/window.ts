import { BrowserWindow, app } from 'electron'
import { join } from 'path'
import { preloadPath } from './preload'

export function createWindow(): BrowserWindow {
  const preloadFile = preloadPath()
  console.log('[WINDOW] Creating window with preload:', preloadFile)

  // Fixed height: header(40) + 5 rows(5×28=140) + footer(28) + padding(8) = 216
  const WINDOW_WIDTH = 480
  const WINDOW_HEIGHT = 216

  const mainWindow = new BrowserWindow({
    width: WINDOW_WIDTH,
    height: WINDOW_HEIGHT,
    show: false,
    resizable: false,
    frame: false,
    transparent: false,
    alwaysOnTop: false,
    webPreferences: {
      preload: preloadFile,
      sandbox: true,
      contextIsolation: true,
      enableRemoteModule: false,
      nodeIntegration: false
    }
  })

  const isDev = !app.isPackaged

  if (isDev) {
    // Try to load from dev server (electron-vite will set this)
    const rendererUrl = process.env.ELECTRON_RENDERER_URL
    if (rendererUrl) {
      mainWindow.loadURL(rendererUrl)
    } else {
      // Fallback: try localhost on common ports
      mainWindow.loadURL('http://localhost:5173')
        .catch(() => mainWindow.loadURL('http://localhost:5174'))
        .catch(() => {
          console.error('Failed to load dev server')
          mainWindow.loadFile(join(__dirname, '../renderer/index.html'))
        })
    }
    mainWindow.webContents.openDevTools()
  } else {
    mainWindow.loadFile(join(__dirname, '../renderer/index.html'))
  }

  mainWindow.webContents.on('crashed', () => {
    console.error('[WINDOW] Renderer process crashed')
  })

  mainWindow.webContents.on('preload-error', (_event, preloadPath, error) => {
    console.error('[WINDOW] Preload error:', preloadPath, error)
  })

  mainWindow.on('ready-to-show', () => {
    console.log('[WINDOW] Window ready to show')
    mainWindow.show()
  })

  return mainWindow
}

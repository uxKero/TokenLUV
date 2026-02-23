import { Tray, Menu, BrowserWindow, app } from 'electron'
import { join } from 'path'
import Store from 'electron-store'

const store = new Store()

export function createTray(mainWindow: BrowserWindow): Tray | null {
  try {
    const trayIconPath = join(__dirname, '../../assets/tray-icon.png')
    const tray = new Tray(trayIconPath)

    const contextMenu = Menu.buildFromTemplate([
      {
        label: 'Actualizar ahora',
        click: () => {
          mainWindow.webContents.send('poller:forceUpdate')
        }
      },
      {
        label: 'Configuración',
        click: () => {
          mainWindow.show()
          mainWindow.focus()
        }
      },
      { type: 'separator' },
      {
        label: 'Salir',
        click: () => {
          app.quit()
        }
      }
    ])

    tray.setContextMenu(contextMenu)
    tray.setToolTip('TokenLUV\nCargando...')

    tray.on('click', () => {
      if (mainWindow.isVisible()) {
        mainWindow.hide()
      } else {
        mainWindow.show()
        mainWindow.focus()
      }
    })

    return tray
  } catch (error) {
    console.error('Error creating tray:', error)
    return null
  }
}

export function updateTrayTooltip(tray: Tray, data: any): void {
  if (!tray) return

  let tooltip = 'TokenLUV\n'

  if (data.anthropic) {
    tooltip += `Anthropic: ${data.anthropic.used || 0} tokens\n`
  }
  if (data.openrouter) {
    tooltip += `OpenRouter: $${(data.openrouter.used || 0).toFixed(2)} / $${(data.openrouter.limit || 0).toFixed(2)}\n`
  }
  if (data.openai) {
    tooltip += `OpenAI: $${(data.openai.used || 0).toFixed(2)} / $${(data.openai.limit || 0).toFixed(2)}\n`
  }
  if (data.xai) {
    tooltip += `xAI: ${data.xai.status || 'Sin config'}\n`
  }
  if (data.gemini) {
    tooltip += `Gemini: ${data.gemini.status || 'Sin config'}\n`
  }

  tooltip += `\nActualizado: ahora`

  tray.setToolTip(tooltip)
}

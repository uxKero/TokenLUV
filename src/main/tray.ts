import { Tray, Menu, BrowserWindow, app } from 'electron'
import { join } from 'path'
import { triggerPollNow } from './poller'

export function createTray(mainWindow: BrowserWindow): Tray | null {
  try {
    const trayIconPath = join(__dirname, '../../assets/tray-icon.png')
    const tray = new Tray(trayIconPath)

    const contextMenu = Menu.buildFromTemplate([
      {
        label: 'Mostrar / Ocultar',
        click: () => {
          if (mainWindow.isVisible()) {
            mainWindow.hide()
          } else {
            mainWindow.show()
            mainWindow.focus()
          }
        }
      },
      {
        label: 'Actualizar ahora',
        click: () => {
          // Trigger poll directly in main process (fix: before it sent to renderer incorrectly)
          triggerPollNow()
          // Show window so user sees the update
          mainWindow.show()
          mainWindow.focus()
        }
      },
      { type: 'separator' },
      {
        label: 'Configuración',
        click: () => {
          mainWindow.show()
          mainWindow.focus()
          // Signal renderer to open settings
          mainWindow.webContents.send('navigate:settings')
        }
      },
      { type: 'separator' },
      {
        label: 'Salir de TokenLUV',
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

export function updateTrayTooltip(tray: Tray, data: Record<string, any>, timestamp?: string): void {
  if (!tray) return

  let tooltip = '❤ TokenLUV\n─────────────\n'

  const lines: string[] = []

  if (data.openrouter?.status === 'ok') {
    const used = data.openrouter.used?.toFixed(2) ?? '0.00'
    const limit = data.openrouter.limit ? `$${data.openrouter.limit.toFixed(2)}` : '—'
    lines.push(`OpenRouter: $${used} / ${limit}`)
  }
  if (data.openai?.status === 'ok') {
    const used = data.openai.used?.toFixed(2) ?? '0.00'
    const limit = data.openai.limit ? `$${data.openai.limit.toFixed(2)}` : '—'
    lines.push(`OpenAI: $${used} / ${limit}`)
  }
  if (data.anthropic?.status === 'ok') {
    lines.push(`Anthropic: Key activa`)
  }
  if (data.xai?.status === 'ok') {
    lines.push(`xAI: Key activa`)
  }
  if (data.gemini?.status === 'ok') {
    lines.push(`Gemini: Key activa`)
  }

  if (lines.length === 0) {
    tooltip += 'Sin providers configurados\n'
  } else {
    tooltip += lines.join('\n') + '\n'
  }

  if (timestamp) {
    const date = new Date(timestamp)
    const timeStr = date.toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit' })
    tooltip += `─────────────\nActualizado: ${timeStr}`
  }

  tray.setToolTip(tooltip)
}

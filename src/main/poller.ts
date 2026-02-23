import Store from 'electron-store'
import { ipcMain, BrowserWindow } from 'electron'
import { OpenRouterProvider } from './providers/openrouter'
import { OpenAIProvider } from './providers/openai'
import { AnthropicProvider } from './providers/anthropic'
import { XAIProvider } from './providers/xai'
import { GeminiProvider } from './providers/gemini'
import { Provider } from './providers/base'

const store = new Store()
let pollerInterval: NodeJS.Timeout | null = null
const providers: Map<string, Provider> = new Map()
let onDataCallback: ((data: Record<string, any>, timestamp: string) => void) | null = null

// Export so tray (and others) can trigger a poll directly
export function triggerPollNow(): void {
  pollProviders()
}

export function initPoller(onData?: (data: Record<string, any>, timestamp: string) => void): void {
  if (onData) onDataCallback = onData
  // Initialize providers
  providers.set('openrouter', new OpenRouterProvider())
  providers.set('openai', new OpenAIProvider())
  providers.set('anthropic', new AnthropicProvider())
  providers.set('xai', new XAIProvider())
  providers.set('gemini', new GeminiProvider())

  ipcMain.handle('poller:start', () => {
    if (!pollerInterval) startPoller()
  })

  ipcMain.handle('poller:stop', () => {
    stopPoller()
  })

  ipcMain.handle('poller:forceUpdate', () => {
    pollProviders()
  })

  // Auto-start
  startPoller()
}

function getIntervalMs(): number {
  const minutes = store.get('refreshInterval', 5) as number
  return Math.max(1, minutes) * 60 * 1000
}

function startPoller(): void {
  console.log('Starting poller...')
  pollProviders() // Poll immediately on start

  const intervalMs = getIntervalMs()
  pollerInterval = setInterval(() => {
    pollProviders()
  }, intervalMs)
}

function stopPoller(): void {
  if (pollerInterval) {
    clearInterval(pollerInterval)
    pollerInterval = null
  }
}

export function restartPoller(): void {
  stopPoller()
  startPoller()
}

async function pollProviders(): Promise<void> {
  console.log('Polling providers...')

  const providerData: Record<string, any> = {}
  const apiKeys = store.get('apiKeys', {}) as Record<string, string>

  for (const [provId, provider] of providers) {
    const apiKey = apiKeys[provId]

    if (!apiKey) {
      providerData[provId] = {
        status: 'no-key' as const,
        used: null,
        limit: null,
        unit: 'usd' as const
      }
      continue
    }

    try {
      const data = await provider.fetch(apiKey)
      providerData[provId] = data
    } catch (error) {
      console.error(`Error fetching ${provId}:`, error)
      providerData[provId] = {
        status: 'error' as const,
        used: null,
        limit: null,
        unit: 'usd' as const,
        error: error instanceof Error ? error.message : 'Unknown error'
      }
    }
  }

  const timestamp = new Date().toISOString()

  store.set('providerData', providerData)
  store.set('lastUpdate', timestamp)

  // Notify tray tooltip callback
  if (onDataCallback) {
    onDataCallback(providerData, timestamp)
  }

  // Broadcast to all windows
  BrowserWindow.getAllWindows().forEach((win) => {
    win.webContents.send('provider:dataUpdated', {
      data: providerData,
      timestamp
    })
  })
}

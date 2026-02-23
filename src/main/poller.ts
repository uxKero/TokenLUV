import Store from 'electron-store'
import { ipcMain, BrowserWindow } from 'electron'
import { OpenRouterProvider } from './providers/openrouter'
import { OpenAIProvider } from './providers/openai'
import { AnthropicProvider } from './providers/anthropic'
import { XAIProvider } from './providers/xai'
import { GeminiProvider } from './providers/gemini'
import { Provider, ProviderData } from './providers/base'

const store = new Store()
let pollerInterval: NodeJS.Timeout | null = null
const providers: Map<string, Provider> = new Map()

export function initPoller(): void {
  // Initialize providers
  providers.set('openrouter', new OpenRouterProvider())
  providers.set('openai', new OpenAIProvider())
  providers.set('anthropic', new AnthropicProvider())
  providers.set('xai', new XAIProvider())
  providers.set('gemini', new GeminiProvider())

  ipcMain.handle('poller:start', () => {
    if (!pollerInterval) {
      startPoller()
    }
  })

  ipcMain.handle('poller:stop', () => {
    stopPoller()
  })

  ipcMain.handle('poller:forceUpdate', () => {
    pollProviders()
  })

  ipcMain.handle('provider:getData', () => {
    return store.get('providerData', {})
  })

  // Auto-start poller on app ready
  startPoller()
}

function startPoller(): void {
  console.log('Starting poller...')
  pollProviders() // Poll immediately

  // Then poll every 5 minutes
  pollerInterval = setInterval(() => {
    pollProviders()
  }, 5 * 60 * 1000)
}

function stopPoller(): void {
  if (pollerInterval) {
    clearInterval(pollerInterval)
    pollerInterval = null
  }
}

async function pollProviders(): Promise<void> {
  console.log('Polling providers...')

  const providerData: Record<string, any> = {}
  const apiKeys = store.get('apiKeys', {}) as Record<string, string>

  // Poll each configured provider
  for (const [provId, provider] of providers) {
    const apiKey = apiKeys[provId]

    if (!apiKey) {
      providerData[provId] = {
        status: 'error' as const,
        used: null,
        limit: null,
        unit: 'usd' as const,
        error: 'No API key configured'
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

  // Save to store
  store.set('providerData', providerData)
  store.set('lastUpdate', timestamp)

  // Broadcast to all windows
  BrowserWindow.getAllWindows().forEach((win) => {
    win.webContents.send('provider:dataUpdated', {
      data: providerData,
      timestamp
    })
  })
}

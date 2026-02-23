import { BaseProvider, ProviderData } from './base'
import Store from 'electron-store'

const store = new Store()

export class AnthropicProvider extends BaseProvider {
  constructor() {
    super('anthropic', 'Anthropic', '#d946ef', '🟣')
  }

  async fetch(apiKey: string): Promise<ProviderData> {
    try {
      // Anthropic doesn't have a public usage API yet
      // We'll do a simple health check with the key
      const response = await fetch('https://api.anthropic.com/v1/models', {
        headers: {
          'x-api-key': apiKey,
          'anthropic-version': '2023-06-01'
        }
      })

      if (!response.ok) {
        return {
          used: null,
          limit: null,
          unit: 'tokens' as const,
          status: 'error'
        }
      }

      // If API key is valid, return tracked local data
      const localData = store.get('anthropic-session-tokens', {
        used: 0,
        limit: 0
      }) as { used: number; limit: number }

      return {
        used: localData.used,
        limit: localData.limit || null,
        unit: 'tokens' as const,
        status: 'ok'
      }
    } catch (error) {
      console.error('Anthropic fetch error:', error)
      return {
        used: null,
        limit: null,
        unit: 'tokens' as const,
        status: 'error'
      }
    }
  }
}

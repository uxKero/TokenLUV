import { BaseProvider, ProviderData } from './base'

export class OpenRouterProvider extends BaseProvider {
  constructor() {
    super('openrouter', 'OpenRouter', '#36a3ff', '🟢')
  }

  async fetch(apiKey: string): Promise<ProviderData> {
    try {
      const response = await fetch('https://openrouter.ai/api/v1/auth/key', {
        headers: {
          Authorization: `Bearer ${apiKey}`
        }
      })

      if (!response.ok) {
        return {
          used: null,
          limit: null,
          unit: 'usd',
          status: 'error'
        }
      }

      const data = await response.json()

      return {
        used: data.data.usage || 0,
        limit: data.data.limit || 0,
        unit: 'usd',
        status: 'ok',
        raw: data
      }
    } catch (error) {
      console.error('OpenRouter fetch error:', error)
      return {
        used: null,
        limit: null,
        unit: 'usd',
        status: 'error'
      }
    }
  }
}

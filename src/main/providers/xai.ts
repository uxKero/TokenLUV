import { BaseProvider, ProviderData } from './base'

export class XAIProvider extends BaseProvider {
  constructor() {
    super('xai', 'xAI (Grok)', '#000000', '⚫')
  }

  async fetch(apiKey: string): Promise<ProviderData> {
    try {
      // xAI uses similar API structure to OpenAI
      // Check the /models endpoint for account validity
      const response = await fetch('https://api.x.ai/v1/models', {
        headers: {
          Authorization: `Bearer ${apiKey}`
        }
      })

      if (!response.ok) {
        return {
          used: null,
          limit: null,
          unit: 'usd' as const,
          status: 'error'
        }
      }

      // For V1, we'll just show API key is valid
      // Real usage data requires additional xAI dashboard scraping
      return {
        used: null,
        limit: null,
        unit: 'usd' as const,
        status: 'ok',
        raw: {
          note: 'API key is valid. Usage data requires xAI console access.'
        }
      }
    } catch (error) {
      console.error('xAI fetch error:', error)
      return {
        used: null,
        limit: null,
        unit: 'usd' as const,
        status: 'error'
      }
    }
  }
}

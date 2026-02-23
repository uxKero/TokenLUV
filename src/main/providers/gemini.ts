import { BaseProvider, ProviderData } from './base'

export class GeminiProvider extends BaseProvider {
  constructor() {
    super('gemini', 'Google Gemini', '#ea4335', '🔴')
  }

  async fetch(apiKey: string): Promise<ProviderData> {
    try {
      // Google Gemini API key health check
      const response = await fetch(
        `https://generativelanguage.googleapis.com/v1beta/models?key=${apiKey}`
      )

      if (!response.ok) {
        return {
          used: null,
          limit: null,
          unit: 'tokens' as const,
          status: 'error'
        }
      }

      // For V1, we'll just show API key is valid
      // Detailed usage requires Google Cloud Billing API (service account)
      return {
        used: null,
        limit: null,
        unit: 'tokens' as const,
        status: 'ok',
        raw: {
          note: 'API key is valid. Usage data via Google Cloud Billing (advanced).'
        }
      }
    } catch (error) {
      console.error('Gemini fetch error:', error)
      return {
        used: null,
        limit: null,
        unit: 'tokens' as const,
        status: 'error'
      }
    }
  }
}

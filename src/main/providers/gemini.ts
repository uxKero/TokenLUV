import { BaseProvider, ProviderData } from './base'

// Priority tiers for Gemini models (index 0 = highest)
const GEMINI_TIERS = ['gemini-2.0-pro', 'gemini-2.0', 'gemini-1.5-pro', 'gemini-1.5-flash', 'gemini-1.5', 'gemini-1.0']

/** Strip "models/" prefix from Gemini model name */
function cleanModelId(name: string): string {
  return name.replace(/^models\//, '')
}

function pickBestGemini(names: string[]): string | undefined {
  // names come in as "models/gemini-2.0-flash", etc.
  const cleaned = names.map(cleanModelId).filter(id => id.startsWith('gemini-'))
  if (cleaned.length === 0) return undefined

  cleaned.sort((a, b) => {
    const ta = GEMINI_TIERS.findIndex(t => a.startsWith(t))
    const tb = GEMINI_TIERS.findIndex(t => b.startsWith(t))
    const pa = ta === -1 ? 99 : ta
    const pb = tb === -1 ? 99 : tb
    if (pa !== pb) return pa - pb
    return b.localeCompare(a)
  })

  return cleaned[0]
}

export class GeminiProvider extends BaseProvider {
  constructor() {
    super('gemini', 'Google Gemini', '#EA4335', '🔴')
  }

  async fetch(apiKey: string): Promise<ProviderData> {
    try {
      // Validate key + detect best model
      const response = await fetch(
        `https://generativelanguage.googleapis.com/v1beta/models?key=${apiKey}`
      )

      if (!response.ok) {
        return { used: null, limit: null, unit: 'tokens', status: 'error' }
      }

      const json = await response.json()
      // Response: { models: [{ name: "models/gemini-2.0-flash", ... }] }
      const names: string[] = Array.isArray(json.models)
        ? json.models.map((m: any) => m.name as string)
        : []
      const model = pickBestGemini(names)

      // Gemini usage requires Google Cloud Billing API + service account
      return {
        used: null,
        limit: null,
        unit: 'tokens',
        status: 'ok',
        model,
        raw: { note: 'Key activa. Usage requiere Google Cloud Billing (service account).' }
      }
    } catch (error) {
      console.error('Gemini fetch error:', error)
      return { used: null, limit: null, unit: 'tokens', status: 'error' }
    }
  }
}

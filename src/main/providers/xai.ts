import { BaseProvider, ProviderData } from './base'

// Priority tiers for Grok models (index 0 = highest)
const XAI_TIERS = ['grok-3', 'grok-2', 'grok-1', 'grok']

/** Strip date suffix: grok-2-1212 → grok-2 */
function cleanModelId(id: string): string {
  return id.replace(/-\d{4}$/, '').replace(/-\d{8}$/, '')
}

function pickBestGrok(ids: string[]): string | undefined {
  const candidates = ids.filter(id => id.startsWith('grok'))
  if (candidates.length === 0) return undefined

  candidates.sort((a, b) => {
    const ta = XAI_TIERS.findIndex(t => a.startsWith(t))
    const tb = XAI_TIERS.findIndex(t => b.startsWith(t))
    const pa = ta === -1 ? 99 : ta
    const pb = tb === -1 ? 99 : tb
    if (pa !== pb) return pa - pb
    return b.localeCompare(a)
  })

  return cleanModelId(candidates[0])
}

export class XAIProvider extends BaseProvider {
  constructor() {
    super('xai', 'xAI (Grok)', '#AAAAAA', '⚫')
  }

  async fetch(apiKey: string): Promise<ProviderData> {
    try {
      // xAI uses OpenAI-compatible API structure
      const response = await fetch('https://api.x.ai/v1/models', {
        headers: { Authorization: `Bearer ${apiKey}` }
      })

      if (!response.ok) {
        return { used: null, limit: null, unit: 'usd', status: 'error' }
      }

      const json = await response.json()
      const ids: string[] = Array.isArray(json.data)
        ? json.data.map((m: any) => m.id as string)
        : []
      const model = pickBestGrok(ids)

      // xAI doesn't expose a public usage/billing API yet
      return {
        used: null,
        limit: null,
        unit: 'usd',
        status: 'ok',
        model,
        raw: { note: 'Key activa. xAI no expone API de uso público aún.' }
      }
    } catch (error) {
      console.error('xAI fetch error:', error)
      return { used: null, limit: null, unit: 'usd', status: 'error' }
    }
  }
}

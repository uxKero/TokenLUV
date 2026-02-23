import { BaseProvider, ProviderData } from './base'
import Store from 'electron-store'

const store = new Store()

export class AnthropicProvider extends BaseProvider {
  constructor() {
    super('anthropic', 'Anthropic', '#E87040', '🟠')
  }

  async fetch(apiKey: string): Promise<ProviderData> {
    try {
      // ── Step 1: Validate regular inference key ────────────────────────────
      // /v1/models lists ALL Anthropic catalog models (not user-specific),
      // so we only use it to confirm the key is valid.
      const response = await fetch('https://api.anthropic.com/v1/models', {
        headers: {
          'x-api-key':         apiKey,
          'anthropic-version': '2023-06-01'
        }
      })

      if (!response.ok) {
        console.error(`[Anthropic] key validation error: ${response.status}`)
        return { used: null, limit: null, unit: 'usd', status: 'error' }
      }

      let used: number | null = null
      let limit: number | null = null
      let costStatus: number | null = null

      // ── Step 2: Admin Key → real monthly cost ─────────────────────────────
      // Admin keys start with sk-ant-admin...
      // Generate at: console.anthropic.com → Settings → Admin Keys
      // Endpoint: GET /v1/organizations/cost_report
      // Returns: amount in cents as decimal string (e.g. "123.45" = $1.23 USD)
      const mgmtKeys = store.get('provisioningKeys', {}) as Record<string, string>
      const adminKey = (mgmtKeys['anthropic'] || '').trim()

      if (adminKey) {
        try {
          const now = new Date()
          // First day of current month, at midnight UTC
          const monthStart = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1))
            .toISOString()
          // Tomorrow (exclusive end bound)
          const tomorrow = new Date(now.getTime() + 86_400_000).toISOString()

          const costRes = await fetch(
            `https://api.anthropic.com/v1/organizations/cost_report` +
            `?starting_at=${encodeURIComponent(monthStart)}` +
            `&ending_at=${encodeURIComponent(tomorrow)}` +
            `&bucket_width=1d`,
            {
              headers: {
                'x-api-key':         adminKey,
                'anthropic-version': '2023-06-01'
              }
            }
          )
          costStatus = costRes.status
          console.log(`[Anthropic] /cost_report HTTP ${costRes.status}`)

          if (costRes.ok) {
            const costBody = await costRes.json()
            let totalCents = 0
            for (const bucket of (costBody?.data || [])) {
              for (const result of (bucket?.results || [])) {
                totalCents += parseFloat(result.amount || '0')
              }
            }
            used = totalCents / 100   // cents → USD
            console.log(`[Anthropic] monthly cost: $${used?.toFixed(4)} (${totalCents} cents)`)
          }
        } catch (e) {
          console.error('[Anthropic] cost_report error:', e)
        }
      }

      return {
        used,
        limit,
        unit: 'usd',
        status: 'ok',
        raw: {
          hasAdminKey: !!adminKey,
          costStatus
        }
      }
    } catch (error) {
      console.error('[Anthropic] fetch exception:', error)
      return { used: null, limit: null, unit: 'usd', status: 'error' }
    }
  }
}

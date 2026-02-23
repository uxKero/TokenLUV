import { BaseProvider, ProviderData } from './base'
import Store from 'electron-store'

const store = new Store()

// Priority tiers for picking "best" OpenAI model (index 0 = highest priority)
const OPENAI_TIERS = ['o1', 'o3', 'gpt-4o', 'gpt-4-turbo', 'gpt-4', 'gpt-3.5']

/** Strip date suffix: gpt-4o-2024-11-20 → gpt-4o */
function cleanModelId(id: string): string {
  return id.replace(/-\d{4}-\d{2}-\d{2}$/, '')
}

function pickBestOpenAI(ids: string[]): string | undefined {
  // Only real GPT / o-series models, skip fine-tuned or whisper/dall-e etc.
  const candidates = ids.filter(id =>
    (id.startsWith('gpt-') || id.startsWith('o1') || id.startsWith('o3')) &&
    !id.includes('instruct') && !id.includes('vision')
  )
  if (candidates.length === 0) return undefined

  candidates.sort((a, b) => {
    const ta = OPENAI_TIERS.findIndex(t => a.startsWith(t))
    const tb = OPENAI_TIERS.findIndex(t => b.startsWith(t))
    const pa = ta === -1 ? 99 : ta
    const pb = tb === -1 ? 99 : tb
    if (pa !== pb) return pa - pb
    return b.localeCompare(a) // higher version within same tier
  })

  return cleanModelId(candidates[0])
}

// Per-model pricing in USD per 1M tokens (input / output)
// Approximate values — used to estimate cost from org usage endpoint
const OPENAI_PRICING: Record<string, { input: number; output: number }> = {
  'gpt-4o':        { input: 2.50,  output: 10.00 },
  'gpt-4o-mini':   { input: 0.15,  output: 0.60  },
  'o1':            { input: 15.00, output: 60.00  },
  'o1-mini':       { input: 3.00,  output: 12.00  },
  'o3':            { input: 10.00, output: 40.00  },
  'o3-mini':       { input: 1.10,  output: 4.40   },
  'gpt-4-turbo':   { input: 10.00, output: 30.00  },
  'gpt-4':         { input: 30.00, output: 60.00  },
  'gpt-3.5-turbo': { input: 0.50,  output: 1.50   }
}
const DEFAULT_PRICING = { input: 2.50, output: 10.00 }

function estimateCost(
  modelUsage: Record<string, { input: number; output: number }>
): number {
  let total = 0
  for (const [m, tokens] of Object.entries(modelUsage)) {
    const tier = OPENAI_PRICING[
      Object.keys(OPENAI_PRICING).find(k => m.startsWith(k)) || ''
    ] || DEFAULT_PRICING
    total += (tokens.input  / 1_000_000) * tier.input
    total += (tokens.output / 1_000_000) * tier.output
  }
  return total
}

export class OpenAIProvider extends BaseProvider {
  constructor() {
    super('openai', 'OpenAI', '#74AA9C', '🔵')
  }

  async fetch(apiKey: string): Promise<ProviderData> {
    try {
      // ── Step 1: Validate key + detect best available model ────────────────
      const modelsResponse = await fetch('https://api.openai.com/v1/models', {
        headers: { Authorization: `Bearer ${apiKey}` }
      })

      if (!modelsResponse.ok) {
        return { used: null, limit: null, unit: 'usd', status: 'error' }
      }

      const modelsJson = await modelsResponse.json()
      const ids: string[] = Array.isArray(modelsJson.data)
        ? modelsJson.data.map((m: any) => m.id as string)
        : []
      const model = pickBestOpenAI(ids)

      let used:  number | null = null
      let limit: number | null = null
      let costStatus: number | null = null

      // ── Step 2: Try old billing endpoint with regular key ─────────────────
      // GET /v1/dashboard/billing/subscription → hard_limit_usd (credit cap)
      // GET /v1/dashboard/billing/usage → total_usage (in cents)
      // These endpoints may still work for some key types.
      try {
        const subRes = await fetch(
          'https://api.openai.com/v1/dashboard/billing/subscription',
          { headers: { Authorization: `Bearer ${apiKey}` } }
        )
        if (subRes.ok) {
          const subData = await subRes.json()
          if (subData.hard_limit_usd != null) {
            limit = subData.hard_limit_usd
            console.log(`[OpenAI] billing limit: $${limit}`)
          }
        }
      } catch (e) { /* ignore — endpoint may not exist */ }

      try {
        const now = new Date()
        const yyyy = now.getFullYear()
        const mm   = String(now.getMonth() + 1).padStart(2, '0')
        const dd   = String(now.getDate()).padStart(2, '0')
        const usageRes = await fetch(
          `https://api.openai.com/v1/dashboard/billing/usage?start_date=${yyyy}-${mm}-01&end_date=${yyyy}-${mm}-${dd}`,
          { headers: { Authorization: `Bearer ${apiKey}` } }
        )
        if (usageRes.ok) {
          const usageData = await usageRes.json()
          if (usageData.total_usage != null) {
            used = usageData.total_usage / 100   // cents → USD
            console.log(`[OpenAI] billing usage: $${used}`)
          }
        }
      } catch (e) { /* ignore */ }

      // ── Step 3: Admin/Org key → detailed organization usage ───────────────
      // Requires an API key with 'usage.read' permission (org-level key)
      // GET /v1/organization/usage/completions returns token counts per model
      // We estimate cost using per-model pricing table above.
      const mgmtKeys = store.get('provisioningKeys', {}) as Record<string, string>
      const adminKey = (mgmtKeys['openai'] || '').trim()

      if (adminKey) {
        try {
          const now = new Date()
          const startTime = Math.floor(
            new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1)).getTime() / 1000
          )
          const endTime = Math.floor(now.getTime() / 1000)

          const orgRes = await fetch(
            `https://api.openai.com/v1/organization/usage/completions` +
            `?start_time=${startTime}&end_time=${endTime}&bucket_width=1d`,
            { headers: { Authorization: `Bearer ${adminKey}` } }
          )
          costStatus = orgRes.status
          console.log(`[OpenAI] /organization/usage/completions HTTP ${orgRes.status}`)

          if (orgRes.ok) {
            const orgData = await orgRes.json()
            const modelUsage: Record<string, { input: number; output: number }> = {}

            for (const bucket of (orgData?.data || [])) {
              for (const result of (bucket?.results || [])) {
                const m = result.model || 'gpt-4o'
                if (!modelUsage[m]) modelUsage[m] = { input: 0, output: 0 }
                modelUsage[m].input  += result.input_tokens  || 0
                modelUsage[m].output += result.output_tokens || 0
              }
            }

            const estimatedCost = estimateCost(modelUsage)
            used = estimatedCost
            console.log(`[OpenAI] org usage estimated cost: $${estimatedCost.toFixed(4)}`)
          }
        } catch (e) {
          console.error('[OpenAI] org usage error:', e)
        }
      }

      // ── Step 4: Fallback — legacy /v1/usage endpoint ──────────────────────
      // Works for some org/project keys; returns monthly totals
      if (used === null) {
        try {
          const now    = new Date()
          const year   = now.getFullYear()
          const month  = String(now.getMonth() + 1).padStart(2, '0')
          const usageResponse = await fetch(
            `https://api.openai.com/v1/usage?date=${year}-${month}-01`,
            { headers: { Authorization: `Bearer ${apiKey}` } }
          )
          if (usageResponse.ok) {
            const usageData = await usageResponse.json()
            let totalCost = 0
            if (Array.isArray(usageData.data)) {
              for (const day of usageData.data) {
                totalCost += day.total_usage || 0
              }
              totalCost = totalCost / 100 // cents → USD
            }
            if (totalCost > 0) {
              used = totalCost
              console.log(`[OpenAI] legacy /v1/usage: $${totalCost}`)
            }
          }
        } catch (e) { /* ignore */ }
      }

      return {
        used,
        limit,
        unit: 'usd',
        status: 'ok',
        model,
        raw: {
          hasAdminKey: !!adminKey,
          costStatus
        }
      }
    } catch (error) {
      console.error('[OpenAI] fetch exception:', error)
      return { used: null, limit: null, unit: 'usd', status: 'error' }
    }
  }
}

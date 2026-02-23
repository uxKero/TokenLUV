import { BaseProvider, ProviderData } from './base'
import Store from 'electron-store'

const store = new Store()

/** Strip provider prefix for compact display: "openai/gpt-4o-mini" → "gpt-4o-mini" */
function cleanModelSlug(slug: string): string {
  const parts = slug.split('/')
  return parts.length > 1 ? parts.slice(1).join('/') : slug
}

type ActivityItem = {
  date: string; model: string; usage: number; requests: number
  promptTokens: number; completionTokens: number
}

function parseActivityItems(rawItems: Array<{
  date: string; model: string; usage: number; requests: number
  prompt_tokens: number; completion_tokens: number
}>): ActivityItem[] {
  if (!Array.isArray(rawItems) || rawItems.length === 0) return []

  // Sort: most recent date first, then most usage
  rawItems.sort((a, b) => {
    const diff = (b.date || '').localeCompare(a.date || '')
    return diff !== 0 ? diff : (b.usage || 0) - (a.usage || 0)
  })

  return rawItems.slice(0, 8).map(i => ({
    date:             i.date,
    model:            cleanModelSlug(i.model || ''),
    usage:            i.usage            ?? 0,
    requests:         i.requests          ?? 0,
    promptTokens:     i.prompt_tokens     ?? 0,
    completionTokens: i.completion_tokens ?? 0
  }))
}

export class OpenRouterProvider extends BaseProvider {
  constructor() {
    super('openrouter', 'OpenRouter', '#36a3ff', '🟢')
  }

  async fetch(apiKey: string): Promise<ProviderData> {
    try {
      // ── Step 1: Validate inference key + get key-level usage ───────────────
      const keyRes = await fetch('https://openrouter.ai/api/v1/auth/key', {
        headers: { Authorization: `Bearer ${apiKey}` }
      })

      if (!keyRes.ok) {
        console.error(`[OpenRouter] auth/key error: ${keyRes.status}`)
        return { used: null, limit: null, unit: 'usd', status: 'error' }
      }

      const keyBody = await keyRes.json()
      const d = keyBody?.data ?? keyBody
      let used:  number | null = d.usage  ?? 0
      let limit: number | null = d.limit  ?? null  // null = no spending cap on key

      let model: string | undefined = undefined
      let creditsStatus: number | null = null
      let activityStatus: number | null = null
      let activityItems: ActivityItem[] = []

      // ── Step 2: Try /activity with inference key (works per-key, no mgmt needed) ──
      // This gives us model history even without a management key
      try {
        const actRes = await fetch('https://openrouter.ai/api/v1/activity', {
          headers: { Authorization: `Bearer ${apiKey}` }
        })
        activityStatus = actRes.status
        console.log(`[OpenRouter] /activity (inference key) HTTP ${actRes.status}`)

        if (actRes.ok) {
          const actBody = await actRes.json()
          const rawItems = actBody?.data || actBody?.activity || actBody || []
          const items = Array.isArray(rawItems) ? rawItems : []

          activityItems = parseActivityItems(items)

          // Top model from most recent activity
          const top = items.find((i: any) => i.model && i.requests > 0)
          if (top?.model) {
            model = cleanModelSlug(top.model)
            console.log(`[OpenRouter] Last used model (inference key): ${top.model} → ${model}`)
          }
        }
      } catch (e) {
        console.error('[OpenRouter] activity (inference key) error:', e)
      }

      // ── Step 3: Management key unlocks real account-wide data ──────────────
      // Generate at: openrouter.ai/settings → Keys → Management Key
      const mgmtKeys = store.get('provisioningKeys', {}) as Record<string, string>
      const mgmtKey  = (mgmtKeys['openrouter'] || '').trim()

      if (mgmtKey) {
        // ── 3a: Real account balance ─────────────────────────────────────────
        try {
          const credRes = await fetch('https://openrouter.ai/api/v1/credits', {
            headers: { Authorization: `Bearer ${mgmtKey}` }
          })
          creditsStatus = credRes.status
          console.log(`[OpenRouter] /credits HTTP ${credRes.status}`)

          if (credRes.ok) {
            const credBody = await credRes.json()
            const c = credBody?.data ?? credBody
            const totalCredits: number | null = c.total_credits ?? null
            const totalUsage:   number        = c.total_usage   ?? 0
            used  = totalUsage
            limit = totalCredits
            console.log(`[OpenRouter] balance: purchased=${totalCredits}, used=${totalUsage}`)
          }
        } catch (e) {
          console.error('[OpenRouter] credits error:', e)
        }

        // ── 3b: Account-wide model history (overrides inference key activity) ─
        try {
          const actRes = await fetch('https://openrouter.ai/api/v1/activity', {
            headers: { Authorization: `Bearer ${mgmtKey}` }
          })
          const mgmtActStatus = actRes.status
          console.log(`[OpenRouter] /activity (mgmt key) HTTP ${mgmtActStatus}`)

          if (actRes.ok) {
            const actBody = await actRes.json()
            const rawItems = actBody?.data || actBody?.activity || actBody || []
            const items = Array.isArray(rawItems) ? rawItems : []

            if (items.length > 0) {
              activityStatus = mgmtActStatus
              activityItems = parseActivityItems(items)

              const top = items.find((i: any) => i.model && i.requests > 0)
              if (top?.model) {
                model = cleanModelSlug(top.model)
                console.log(`[OpenRouter] Last used model (mgmt key): ${top.model} → ${model}`)
              }
            }
          }
        } catch (e) {
          console.error('[OpenRouter] activity (mgmt key) error:', e)
        }
      }

      console.log(`[OpenRouter] Final: model=${model}, activityItems=${activityItems.length}`)

      return {
        used,
        limit,
        unit: 'usd',
        status: 'ok',
        model,
        raw: {
          hasMgmtKey:    !!mgmtKey,
          creditsStatus,
          activityStatus,
          activityItems  // passed to ProviderRow for tooltip
        }
      }
    } catch (error) {
      console.error('[OpenRouter] Fetch exception:', error)
      return { used: null, limit: null, unit: 'usd', status: 'error' }
    }
  }
}

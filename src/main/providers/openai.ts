import { BaseProvider, ProviderData } from './base'

export class OpenAIProvider extends BaseProvider {
  constructor() {
    super('openai', 'OpenAI', '#00a67e', '🔵')
  }

  async fetch(apiKey: string): Promise<ProviderData> {
    try {
      // Get subscription info
      const subResponse = await fetch('https://api.openai.com/v1/dashboard/billing/subscription', {
        headers: {
          Authorization: `Bearer ${apiKey}`
        }
      })

      if (!subResponse.ok) {
        return {
          used: null,
          limit: null,
          unit: 'usd' as const,
          status: 'error'
        }
      }

      const subData = await subResponse.json()
      const softLimit = subData.soft_limit || 0
      const hardLimit = subData.hard_limit || softLimit

      // Get usage for current month
      const now = new Date()
      const startDate = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`
      const endDate = now.toISOString().split('T')[0]

      const usageResponse = await fetch(
        `https://api.openai.com/v1/dashboard/billing/usage?start_date=${startDate}&end_date=${endDate}`,
        {
          headers: {
            Authorization: `Bearer ${apiKey}`
          }
        }
      )

      let totalUsage = 0
      if (usageResponse.ok) {
        const usageData = await usageResponse.json()
        totalUsage = usageData.total_usage / 100 || 0 // Convert from cents to dollars
      }

      return {
        used: totalUsage,
        limit: softLimit,
        unit: 'usd' as const,
        status: 'ok',
        raw: {
          softLimit,
          hardLimit,
          period: `${startDate} to ${endDate}`
        }
      }
    } catch (error) {
      console.error('OpenAI fetch error:', error)
      return {
        used: null,
        limit: null,
        unit: 'usd' as const,
        status: 'error'
      }
    }
  }
}

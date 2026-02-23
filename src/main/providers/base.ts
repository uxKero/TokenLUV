export interface ProviderData {
  used: number | null
  limit: number | null
  unit: 'tokens' | 'usd'
  status: 'ok' | 'error' | 'loading' | 'no-key'
  model?: string   // auto-detected best model available for this key
  raw?: any
}

export interface Provider {
  id: string
  name: string
  color: string
  icon: string
  fetch(apiKey: string): Promise<ProviderData>
}

export class BaseProvider implements Provider {
  id: string
  name: string
  color: string
  icon: string

  constructor(id: string, name: string, color: string, icon: string) {
    this.id = id
    this.name = name
    this.color = color
    this.icon = icon
  }

  async fetch(apiKey: string): Promise<ProviderData> {
    throw new Error('fetch() must be implemented by subclass')
  }
}

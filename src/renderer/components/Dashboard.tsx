import { useState, useEffect } from 'react'
import { RefreshCw } from 'lucide-react'
import ProviderCard from './ProviderCard'

interface ProviderCardData {
  used: number | null
  limit: number | null
  unit: 'tokens' | 'usd'
  status: 'ok' | 'error' | 'loading'
}

interface ProviderDataMap {
  [key: string]: ProviderCardData
}

const PROVIDERS = [
  { id: 'anthropic', name: 'Anthropic', icon: '🟣' },
  { id: 'openai', name: 'OpenAI', icon: '🔵' },
  { id: 'openrouter', name: 'OpenRouter', icon: '🟢' },
  { id: 'xai', name: 'xAI', icon: '⚫' },
  { id: 'gemini', name: 'Gemini', icon: '🔴' }
]

export default function Dashboard() {
  const [data, setData] = useState<ProviderDataMap>({})
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null)
  const [isLoading, setIsLoading] = useState(false)

  useEffect(() => {
    // Initial load
    window.tokenLuvApi.getProviderData().then((data: any) => {
      setData(data || {})
    })

    // Setup event listener for updates
    const ipcRenderer = (window as any).ipcRenderer || {}

    if (ipcRenderer.on) {
      ipcRenderer.on('provider:dataUpdated', (_event: any, update: any) => {
        setData(update.data || {})
        setLastUpdate(new Date(update.timestamp))
        setIsLoading(false)
      })
    }
  }, [])

  const handleUpdate = async () => {
    setIsLoading(true)
    await window.tokenLuvApi.forceUpdate()
    // Data will be received via event
  }

  const timeAgo = lastUpdate
    ? (() => {
        const seconds = Math.floor((Date.now() - lastUpdate.getTime()) / 1000)
        if (seconds < 60) return 'hace segundos'
        const minutes = Math.floor(seconds / 60)
        if (minutes < 60) return `hace ${minutes} min`
        const hours = Math.floor(minutes / 60)
        return `hace ${hours}h`
      })()
    : 'nunca'

  const getCardData = (provId: string): ProviderCardData => {
    return (
      data[provId] || {
        used: null,
        limit: null,
        unit: 'usd' as const,
        status: 'loading' as const
      }
    )
  }

  return (
    <div className="p-4 space-y-4">
      {/* Provider Cards Grid */}
      <div className="grid grid-cols-2 gap-3">
        {PROVIDERS.map((provider) => {
          const provData = getCardData(provider.id)
          return (
            <ProviderCard
              key={provider.id}
              name={provider.name}
              icon={provider.icon}
              used={provData.used}
              limit={provData.limit}
              unit={provData.unit}
              status={provData.status}
            />
          )
        })}
      </div>

      {/* Footer */}
      <div className="flex items-center justify-between text-sm text-slate-400 mt-6 pt-4 border-t border-slate-700">
        <div>
          <p>Última actualiz.</p>
          <p className="text-slate-300">{timeAgo}</p>
        </div>
        <button
          onClick={handleUpdate}
          disabled={isLoading}
          className="bg-purple-600 hover:bg-purple-700 disabled:bg-slate-700 text-white px-4 py-2 rounded-lg flex items-center gap-2 transition"
        >
          <RefreshCw size={16} className={isLoading ? 'animate-spin' : ''} />
          {isLoading ? 'Actualizando...' : 'Actualizar ahora'}
        </button>
      </div>
    </div>
  )
}

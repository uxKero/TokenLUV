import { useState, useEffect, useCallback } from 'react'
import ProviderRow from './ProviderRow'

interface ProviderCardData {
  used: number | null
  limit: number | null
  unit: 'tokens' | 'usd'
  status: 'ok' | 'error' | 'loading' | 'no-key'
  model?: string   // auto-detected by the main process
  raw?: {
    hasMgmtKey?: boolean
    creditsStatus?: number | null
    activityStatus?: number | null
    activityItems?: any[]
  }
}

interface ProviderDataMap {
  [key: string]: ProviderCardData
}

const ALL_PROVIDERS = [
  { id: 'anthropic', name: 'Anthropic' },
  { id: 'openai', name: 'OpenAI' },
  { id: 'openrouter', name: 'OpenRouter' },
  { id: 'xai', name: 'xAI' },
  { id: 'gemini', name: 'Gemini' }
]

function formatTimestamp(iso: string): string {
  try {
    const date = new Date(iso)
    return date.toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
  } catch {
    return '--:--'
  }
}

export default function Dashboard() {
  const [data,        setData]        = useState<ProviderDataMap>({})
  const [modelLabels, setModelLabels] = useState<Record<string, string>>({})
  const [lastUpdate,  setLastUpdate]  = useState<string>('')
  const [isRefreshing, setIsRefreshing] = useState(false)

  const reloadModelLabels = useCallback(async () => {
    const api = (window as any).tokenLuvApi
    if (!api) return
    const labels = await api.getConfig('modelLabels', {})
    setModelLabels(labels || {})
  }, [])

  const resizeWindow = useCallback((count: number) => {
    const tokenLuvApi = (window as any).tokenLuvApi
    if (tokenLuvApi) tokenLuvApi.resizeWindow(count)
  }, [])

  const handleUpdate = useCallback((update: { data: ProviderDataMap; timestamp: string }) => {
    const newData = update.data || {}
    setData(newData)
    setLastUpdate(update.timestamp || '')
    setIsRefreshing(false)
    // Reload model labels so new names show immediately after saving settings
    reloadModelLabels()

    const activeCount = ALL_PROVIDERS.filter(p => {
      const d = newData[p.id]
      return d && d.status !== 'no-key'
    }).length
    resizeWindow(activeCount)
  }, [resizeWindow, reloadModelLabels])

  useEffect(() => {
    const tokenLuvApi = (window as any).tokenLuvApi
    if (!tokenLuvApi) return

    Promise.all([
      tokenLuvApi.getProviderData(),
      tokenLuvApi.getConfig('lastUpdate',   ''),
      tokenLuvApi.getConfig('modelLabels',  {})
    ]).then(([provData, ts, labels]: [ProviderDataMap, string, Record<string, string>]) => {
      const safeData = provData || {}
      setData(safeData)
      setLastUpdate(ts || '')
      setModelLabels(labels || {})

      const activeCount = ALL_PROVIDERS.filter(p => {
        const d = safeData[p.id]
        return d && d.status !== 'no-key'
      }).length
      resizeWindow(activeCount)
    })

    const cleanup = tokenLuvApi.onProviderDataUpdate(handleUpdate)
    return () => { if (typeof cleanup === 'function') cleanup() }
  }, [handleUpdate, resizeWindow])

  const handleRefresh = async () => {
    const tokenLuvApi = (window as any).tokenLuvApi
    if (!tokenLuvApi || isRefreshing) return
    setIsRefreshing(true)
    await tokenLuvApi.forceUpdate()
    setTimeout(() => setIsRefreshing(false), 5000)
  }

  // Only show providers that have a key configured (status !== 'no-key' and !== undefined/loading-initial)
  const activeProviders = ALL_PROVIDERS.filter(p => {
    const d = data[p.id]
    return d && d.status !== 'no-key'
  })

  const getProviderData = (provId: string): ProviderCardData => {
    return data[provId] || { used: null, limit: null, unit: 'usd', status: 'loading' }
  }

  return (
    <div
      className="w-full h-full flex flex-col"
      style={{
        backgroundColor: '#111118',
        fontFamily: "'JetBrains Mono', monospace",
        fontSize: '12px',
        color: '#f8fafc'
      }}
    >
      {/* Providers — solo los activos */}
      <div className="flex-1 px-2 pt-2 space-y-0.5">
        {activeProviders.length > 0 ? (
          activeProviders.map(provider => (
            <ProviderRow
              key={provider.id}
              id={provider.id}
              name={provider.name}
              used={getProviderData(provider.id).used}
              limit={getProviderData(provider.id).limit}
              unit={getProviderData(provider.id).unit}
              status={getProviderData(provider.id).status}
              modelLabel={
                (modelLabels[provider.id] && modelLabels[provider.id].trim())
                  ? modelLabels[provider.id].trim()
                  : getProviderData(provider.id).model
              }
              rawData={getProviderData(provider.id).raw}
            />
          ))
        ) : (
          <div style={{
            padding: '8px 6px',
            fontSize: '10px',
            color: '#334155',
            textAlign: 'center'
          }}>
            Sin API keys · Abrí ⚙️ para configurar
          </div>
        )}
      </div>

      {/* Footer */}
      <div style={{
        borderTop: '1px solid #1e1e2e',
        padding: '4px 8px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between'
      }}>
        <span style={{ fontSize: '9px', color: '#334155' }}>
          {lastUpdate ? `↺ ${formatTimestamp(lastUpdate)}` : '↺ sin datos aún'}
        </span>
        <button
          onClick={handleRefresh}
          disabled={isRefreshing}
          title="Actualizar ahora"
          style={{
            backgroundColor: 'transparent',
            border: 'none',
            cursor: isRefreshing ? 'not-allowed' : 'pointer',
            color: isRefreshing ? '#334155' : '#8b5cf6',
            display: 'flex',
            alignItems: 'center',
            gap: '3px',
            opacity: isRefreshing ? 0.5 : 1,
            padding: '2px 4px'
          }}
          onMouseEnter={e => { if (!isRefreshing) (e.currentTarget as HTMLElement).style.color = '#a78bfa' }}
          onMouseLeave={e => { if (!isRefreshing) (e.currentTarget as HTMLElement).style.color = '#8b5cf6' }}
        >
          <span style={{ fontSize: '12px' }}>⟳</span>
          <span style={{ fontSize: '9px' }}>{isRefreshing ? 'actualizando...' : 'refresh'}</span>
        </button>
      </div>
    </div>
  )
}

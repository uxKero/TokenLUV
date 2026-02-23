import { useState, useEffect } from 'react'
import { Eye, EyeOff } from 'lucide-react'

interface SettingsProps {
  onClose: () => void
}

interface ApiKeys {
  anthropic?: string
  openai?: string
  openrouter?: string
  xai?: string
  gemini?: string
}

interface ModelLabels {
  anthropic?: string
  openai?: string
  openrouter?: string
  xai?: string
  gemini?: string
}

interface ProviderStatus {
  status?: 'ok' | 'error' | 'loading' | 'no-key'
  model?: string
  used?: number | null
  raw?: {
    hasAdminKey?:    boolean
    costStatus?:     number | null
    creditsStatus?:  number | null
    activityStatus?: number | null
    [key: string]:   any
  }
}

// Config for Admin/Management Key per provider
interface AdminKeyConfig {
  placeholder:  string  // input placeholder hint
  label:        string  // label shown in the sub-section
  statusField:  string  // key in raw.{field} to check HTTP status (200 = ok)
  successText:  string  // shown when status is 200
  helpText:     string  // shown when no key yet
}

const PROVIDER_COLORS: Record<string, string> = {
  anthropic:  '#E87040',
  openai:     '#74AA9C',
  openrouter: '#67e8f9',
  xai:        '#AAAAAA',
  gemini:     '#EA4335'
}

const ALL_PROVIDERS: Array<{
  id:              string
  name:            string
  placeholder:     string
  modelPlaceholder:string
  note:            string | null
  adminKey?:       AdminKeyConfig
}> = [
  {
    id:              'anthropic',
    name:            'Anthropic',
    placeholder:     'sk-ant-...',
    modelPlaceholder:'claude-haiku-4-5',
    note:            'Sin API de consumo pública · el modelo no se puede auto-detectar',
    adminKey: {
      placeholder:  'sk-ant-admin... → Console → Settings → Admin Keys',
      label:        'Admin Key (costo mensual)',
      statusField:  'costStatus',
      successText:  '✓ costo real OK',
      helpText:     'opcional · desbloquea el gasto real del mes'
    }
  },
  {
    id:              'openai',
    name:            'OpenAI',
    placeholder:     'sk-...',
    modelPlaceholder:'gpt-4o',
    note:            null,
    adminKey: {
      placeholder:  'sk-... con permiso usage.read → Dashboard → API Keys',
      label:        'Org Key (uso detallado)',
      statusField:  'costStatus',
      successText:  '✓ uso org OK',
      helpText:     'opcional · key con usage.read desbloquea costo estimado'
    }
  },
  {
    id:              'openrouter',
    name:            'OpenRouter',
    placeholder:     'sk-or-v1-...',
    modelPlaceholder:'openai/gpt-4o-mini',
    note:            'Auto-detecta el último modelo usado desde tu historial de actividad',
    adminKey: {
      placeholder:  'openrouter.ai/settings → Keys → Management Key',
      label:        'Management Key (saldo real)',
      statusField:  'creditsStatus',
      successText:  '✓ saldo + actividad OK',
      helpText:     'opcional · desbloquea saldo real + historial de modelos'
    }
  },
  {
    id:              'xai',
    name:            'xAI (Grok)',
    placeholder:     'xai-...',
    modelPlaceholder:'grok-2',
    note:            'Sin API de consumo pública · el modelo no se puede auto-detectar'
  },
  {
    id:              'gemini',
    name:            'Google Gemini',
    placeholder:     'AIza...',
    modelPlaceholder:'gemini-2.0-flash',
    note:            'Sin API de consumo pública · el modelo no se puede auto-detectar'
  }
]

const REFRESH_OPTIONS = [
  { label: '1 min',  value: 1  },
  { label: '5 min',  value: 5  },
  { label: '15 min', value: 15 },
  { label: '30 min', value: 30 }
]

function StatusBadge({ status, model }: { status?: string; model?: string }) {
  if (!status || status === 'no-key') {
    return <span style={{ color: '#334155', fontSize: '9px', marginLeft: 'auto' }}>sin key</span>
  }
  if (status === 'loading') {
    return <span style={{ color: '#8b5cf6', fontSize: '9px', marginLeft: 'auto' }}>⟳ consultando</span>
  }
  if (status === 'error') {
    return <span style={{ color: '#ef4444', fontSize: '9px', marginLeft: 'auto' }}>✗ key inválida</span>
  }
  // ok
  const label = model ? model : 'activa'
  return <span style={{ color: '#22c55e', fontSize: '9px', marginLeft: 'auto' }}>✓ {label}</span>
}

export default function Settings({ onClose }: SettingsProps) {
  const [apiKeys,          setApiKeys]          = useState<ApiKeys>({})
  const [modelLabels,      setModelLabels]      = useState<ModelLabels>({})
  const [provisioningKeys, setProvisioningKeys] = useState<Record<string, string>>({})
  const [refreshInterval,  setRefreshInterval]  = useState<number>(5)
  const [isSaving,         setIsSaving]         = useState(false)
  const [showKey,          setShowKey]          = useState<string | null>(null)
  // Track which admin keys are visible (by provider id)
  const [showAdminKey,     setShowAdminKey]     = useState<Record<string, boolean>>({})
  const [saved,            setSaved]            = useState(false)
  const [providerStatus,   setProviderStatus]   = useState<Record<string, ProviderStatus>>({})

  useEffect(() => {
    const api = (window as any).tokenLuvApi
    if (!api) return

    Promise.all([
      api.getConfig('apiKeys',          {}),
      api.getConfig('modelLabels',      {}),
      api.getConfig('provisioningKeys', {}),
      api.getConfig('refreshInterval',   5),
      api.getProviderData()
    ]).then(([keys, labels, provKeys, interval, provData]: [ApiKeys, ModelLabels, Record<string,string>, number, Record<string, any>]) => {
      setApiKeys(keys          || {})
      setModelLabels(labels    || {})
      setProvisioningKeys(provKeys || {})
      setRefreshInterval(interval || 5)
      setProviderStatus(provData || {})
    }).catch(console.error)

    // Live updates from poller while settings is open
    const cleanup = api.onProviderDataUpdate((update: { data: Record<string, any> }) => {
      if (update?.data) setProviderStatus(update.data)
    })
    return () => { if (typeof cleanup === 'function') cleanup() }
  }, [])

  const handleKeyChange      = (id: string, value: string) =>
    setApiKeys(prev => ({ ...prev, [id]: value }))

  const handleModelChange    = (id: string, value: string) =>
    setModelLabels(prev => ({ ...prev, [id]: value }))

  const handleAdminKeyChange = (id: string, value: string) =>
    setProvisioningKeys(prev => ({ ...prev, [id]: value }))

  const handleClearKey       = (id: string) =>
    setApiKeys(prev => ({ ...prev, [id]: '' }))

  const toggleAdminKeyVis = (id: string) =>
    setShowAdminKey(prev => ({ ...prev, [id]: !prev[id] }))

  const handleSave = async () => {
    const api = (window as any).tokenLuvApi
    if (!api) return
    try {
      setIsSaving(true)
      await api.setConfig('apiKeys',          apiKeys)
      await api.setConfig('modelLabels',     modelLabels)
      await api.setConfig('provisioningKeys', provisioningKeys)
      await api.setConfig('refreshInterval', refreshInterval)
      await api.forceUpdate()
      setSaved(true)
      setTimeout(() => { setSaved(false); onClose() }, 800)
    } catch (err) {
      console.error('Error saving settings:', err)
    } finally {
      setIsSaving(false)
    }
  }

  // ─── Render ───────────────────────────────────────────────────────────────
  return (
    <div style={{
      width:           '100%',
      height:          '100%',
      display:         'flex',
      flexDirection:   'column',
      backgroundColor: '#111118',
      fontFamily:      "'Inter', sans-serif",
      color:           '#f8fafc',
      overflow:        'hidden'
    }}>

      {/* ── Header — drag region so window is movable ── */}
      <div
        className="drag-region"
        style={{
          backgroundColor: '#1a1a24',
          borderBottom:    '1px solid #2a2a3a',
          padding:         '10px 14px',
          display:         'flex',
          alignItems:      'center',
          justifyContent:  'space-between',
          flexShrink:      0,
          cursor:          'move'
        }}
      >
        <span style={{
          fontFamily:    "'JetBrains Mono', monospace",
          fontSize:      '11px',
          fontWeight:    'bold',
          letterSpacing: '0.5px'
        }}>
          TokenLUV<span style={{ color: '#ef4444' }}>♥</span>
          <span style={{ color: '#475569', marginLeft: '6px', fontWeight: 400 }}>· APIs</span>
        </span>
        <button
          className="no-drag"
          onClick={onClose}
          style={{
            backgroundColor: 'transparent', border: 'none',
            color: '#475569', cursor: 'pointer',
            fontSize: '13px', padding: '3px 6px', borderRadius: '3px'
          }}
          onMouseEnter={e => { e.currentTarget.style.color = '#ef4444'; e.currentTarget.style.backgroundColor = '#2a2a3a' }}
          onMouseLeave={e => { e.currentTarget.style.color = '#475569'; e.currentTarget.style.backgroundColor = 'transparent' }}
        >✕</button>
      </div>

      {/* ── Scrollable content ── */}
      <div style={{ flex: 1, overflowY: 'auto', padding: '12px 14px' }}>
        <p style={{ fontSize: '10px', color: '#475569', margin: '0 0 12px 0' }}>
          Las keys se guardan encriptadas localmente.
        </p>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
          {ALL_PROVIDERS.map(({ id, name, placeholder, modelPlaceholder, note, adminKey }) => {
            const color     = PROVIDER_COLORS[id]
            const hasKey    = !!(apiKeys[id as keyof ApiKeys])
            const ps        = providerStatus[id] as ProviderStatus | undefined
            const autoModel = ps?.model
            const lastStatus= ps?.status

            // Admin key status
            const rawData       = ps?.raw
            const adminKeyValue = provisioningKeys[id] || ''
            const hasAdminKey   = !!adminKeyValue
            // The HTTP status for this provider's admin endpoint
            const adminStatus   = adminKey ? (rawData?.[adminKey.statusField] as number | null | undefined) : null

            return (
              <div key={id}>
                {/* Provider label row */}
                <div style={{
                  fontSize: '10px', display: 'flex', alignItems: 'center',
                  gap: '5px', marginBottom: '4px', color: '#94a3b8'
                }}>
                  <span style={{ color, fontSize: '8px' }}>●</span>
                  <span style={{ fontWeight: 600 }}>{name}</span>
                  <StatusBadge status={lastStatus} model={autoModel} />
                </div>

                {/* API Key row */}
                <div style={{ display: 'flex', gap: '4px', alignItems: 'center', marginBottom: '4px' }}>
                  <input
                    type={showKey === id ? 'text' : 'password'}
                    value={apiKeys[id as keyof ApiKeys] || ''}
                    onChange={e => handleKeyChange(id, e.target.value)}
                    placeholder={placeholder}
                    style={{
                      flex: 1, backgroundColor: '#0f0f14',
                      border: `1px solid ${lastStatus === 'error' ? '#3a1a1a' : hasKey ? '#2a3a2a' : '#2a2a3a'}`,
                      borderRadius: '3px', padding: '5px 8px',
                      fontSize: '9px', fontFamily: "'JetBrains Mono', monospace",
                      color: '#f8fafc', outline: 'none'
                    }}
                    onFocus={e  => { e.currentTarget.style.borderColor = color }}
                    onBlur={e   => { e.currentTarget.style.borderColor = lastStatus === 'error' ? '#3a1a1a' : hasKey ? '#2a3a2a' : '#2a2a3a' }}
                  />
                  <button onClick={() => setShowKey(showKey === id ? null : id)}
                    style={{ backgroundColor: 'transparent', border: 'none', cursor: 'pointer', padding: '3px', color: '#475569' }}>
                    {showKey === id ? <Eye size={11} /> : <EyeOff size={11} />}
                  </button>
                  {hasKey && (
                    <button onClick={() => handleClearKey(id)}
                      style={{ backgroundColor: 'transparent', border: 'none', cursor: 'pointer', padding: '3px', color: '#ef4444', fontSize: '10px' }}>
                      ✕
                    </button>
                  )}
                </div>

                {/* ── Admin / Management Key sub-section ───────────────────── */}
                {adminKey && hasKey && (
                  <div style={{
                    marginBottom: '4px',
                    marginLeft:   '6px',
                    paddingLeft:  '8px',
                    borderLeft:   `2px solid ${adminStatus === 200 ? '#22c55e44' : '#334155'}`
                  }}>
                    {/* Sub-label + status */}
                    <div style={{
                      fontSize: '9px', color: '#475569',
                      display: 'flex', alignItems: 'center', gap: '4px',
                      marginBottom: '3px'
                    }}>
                      <span style={{ color: '#334155' }}>└ {adminKey.label}</span>
                      {adminStatus === 200
                        ? <span style={{ color: '#22c55e', marginLeft: 'auto', fontSize: '8px' }}>{adminKey.successText}</span>
                        : adminStatus != null
                          ? <span style={{ color: '#ef4444', marginLeft: 'auto', fontSize: '8px' }}>✗ HTTP {adminStatus}</span>
                          : <span style={{ color: '#334155', marginLeft: 'auto', fontSize: '8px' }}>{adminKey.helpText}</span>
                      }
                    </div>

                    {/* Admin key input */}
                    <div style={{ display: 'flex', gap: '4px', alignItems: 'center' }}>
                      <input
                        type={showAdminKey[id] ? 'text' : 'password'}
                        value={adminKeyValue}
                        onChange={e => handleAdminKeyChange(id, e.target.value)}
                        placeholder={adminKey.placeholder}
                        style={{
                          flex: 1, backgroundColor: '#0a0a10',
                          border: '1px solid #1a1a28',
                          borderRadius: '3px', padding: '4px 8px',
                          fontSize: '8px', fontFamily: "'JetBrains Mono', monospace",
                          color: '#94a3b8', outline: 'none'
                        }}
                        onFocus={e  => { e.currentTarget.style.borderColor = color }}
                        onBlur={e   => { e.currentTarget.style.borderColor = '#1a1a28' }}
                      />
                      <button onClick={() => toggleAdminKeyVis(id)}
                        style={{ backgroundColor: 'transparent', border: 'none', cursor: 'pointer', padding: '2px', color: '#334155' }}>
                        {showAdminKey[id] ? <Eye size={10} /> : <EyeOff size={10} />}
                      </button>
                    </div>
                  </div>
                )}

                {/* Model override / manual entry */}
                <input
                  type="text"
                  value={modelLabels[id as keyof ModelLabels] || ''}
                  onChange={e => handleModelChange(id, e.target.value)}
                  placeholder={
                    autoModel
                      ? `Override modelo (auto: ${autoModel})`
                      : `Modelo que usás (ej: ${modelPlaceholder})`
                  }
                  style={{
                    width: '100%', boxSizing: 'border-box',
                    backgroundColor: '#0d0d12',
                    border: '1px solid #1e1e2e',
                    borderRadius: '3px', padding: '4px 8px',
                    fontSize: '9px', fontFamily: "'JetBrains Mono', monospace",
                    color: '#67e8f9', outline: 'none',
                    opacity: hasKey ? 1 : 0.4
                  }}
                  onFocus={e  => { e.currentTarget.style.borderColor = '#334155' }}
                  onBlur={e   => { e.currentTarget.style.borderColor = '#1e1e2e' }}
                />
                {/* Auto-detect note */}
                {note && hasKey && (
                  <p style={{
                    margin: '2px 0 0 0', fontSize: '8px',
                    color: autoModel ? '#22c55e' : '#334155',
                    lineHeight: '1.3'
                  }}>
                    {autoModel ? `⚡ auto: ${autoModel}` : `ⓘ ${note}`}
                  </p>
                )}
              </div>
            )
          })}
        </div>
      </div>

      {/* ── Divider ── */}
      <div style={{ borderTop: '1px solid #2a2a3a', flexShrink: 0 }} />

      {/* ── Refresh interval ── */}
      <div style={{ padding: '10px 14px', flexShrink: 0 }}>
        <label style={{ fontSize: '10px', color: '#94a3b8', display: 'block', marginBottom: '6px', fontWeight: 600 }}>
          Intervalo de actualización
        </label>
        <div style={{ display: 'flex', gap: '6px' }}>
          {REFRESH_OPTIONS.map(({ label, value }) => (
            <button
              key={value}
              onClick={() => setRefreshInterval(value)}
              style={{
                flex: 1, padding: '4px 6px',
                backgroundColor: refreshInterval === value ? '#8b5cf6' : '#1a1a2e',
                border: `1px solid ${refreshInterval === value ? '#8b5cf6' : '#2a2a3a'}`,
                borderRadius: '3px',
                color: refreshInterval === value ? '#fff' : '#475569',
                fontSize: '10px', cursor: 'pointer',
                fontWeight: refreshInterval === value ? 600 : 400
              }}
            >{label}</button>
          ))}
        </div>
      </div>

      {/* ── Footer — Info + Cancel + Save ── */}
      <div style={{
        backgroundColor: '#0f0f14',
        borderTop:       '1px solid #2a2a3a',
        padding:         '8px 14px',
        display:         'flex',
        alignItems:      'center',
        gap:             '8px',
        flexShrink:      0
      }}>
        <button
          title="Sobre TokenLUV · GitHub (próximamente)"
          style={{
            backgroundColor: 'transparent', border: 'none',
            cursor: 'pointer', color: '#334155',
            fontSize: '14px', padding: '2px 4px',
            fontFamily: 'monospace', lineHeight: 1,
            borderRadius: '3px'
          }}
          onMouseEnter={e => { e.currentTarget.style.color = '#8b5cf6' }}
          onMouseLeave={e => { e.currentTarget.style.color = '#334155' }}
        >ⓘ</button>

        <div style={{ flex: 1 }} />

        <button
          onClick={onClose}
          style={{
            padding: '5px 12px', backgroundColor: '#2a2a3a',
            border: 'none', borderRadius: '3px',
            color: '#94a3b8', fontSize: '10px', fontWeight: 500, cursor: 'pointer'
          }}
          onMouseEnter={e => { e.currentTarget.style.backgroundColor = '#3a3a4a' }}
          onMouseLeave={e => { e.currentTarget.style.backgroundColor = '#2a2a3a' }}
        >Cancelar</button>

        <button
          onClick={handleSave}
          disabled={isSaving}
          style={{
            padding: '5px 14px',
            backgroundColor: saved ? '#22c55e' : isSaving ? '#475569' : '#8b5cf6',
            border: 'none', borderRadius: '3px',
            color: '#fff', fontSize: '10px', fontWeight: 600,
            cursor: isSaving ? 'not-allowed' : 'pointer',
            minWidth: '70px', transition: 'background-color 0.2s'
          }}
          onMouseEnter={e => { if (!isSaving && !saved) e.currentTarget.style.backgroundColor = '#a78bfa' }}
          onMouseLeave={e => { if (!isSaving && !saved) e.currentTarget.style.backgroundColor = '#8b5cf6' }}
        >{saved ? '✓ Guardado' : isSaving ? 'Guardando...' : 'Guardar'}</button>
      </div>
    </div>
  )
}

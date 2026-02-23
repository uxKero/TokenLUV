import { useState, useEffect } from 'react'
import { ChevronLeft, Save } from 'lucide-react'

interface SettingsProps {
  setView: (view: 'dashboard' | 'settings') => void
}

interface ApiKeys {
  anthropic?: string
  openai?: string
  openrouter?: string
  xai?: string
  gemini?: string
}

export default function Settings({ setView }: SettingsProps) {
  const [apiKeys, setApiKeys] = useState<ApiKeys>({})
  const [isSaving, setIsSaving] = useState(false)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    window.tokenLuvApi.getConfig('apiKeys', {}).then((keys: ApiKeys) => {
      setApiKeys(keys)
    })
  }, [])

  const handleChange = (provider: string, value: string) => {
    setApiKeys({
      ...apiKeys,
      [provider]: value
    })
    setSaved(false)
  }

  const handleSave = async () => {
    setIsSaving(true)
    await window.tokenLuvApi.setConfig('apiKeys', apiKeys)
    setIsSaving(false)
    setSaved(true)
    setTimeout(() => setSaved(false), 2000)
  }

  const providers = [
    { id: 'anthropic', name: 'Anthropic', icon: '🟣' },
    { id: 'openai', name: 'OpenAI', icon: '🔵' },
    { id: 'openrouter', name: 'OpenRouter', icon: '🟢' },
    { id: 'xai', name: 'xAI (Grok)', icon: '⚫' },
    { id: 'gemini', name: 'Google Gemini', icon: '🔴' }
  ]

  return (
    <div className="p-4 space-y-4">
      <div className="flex items-center gap-2 mb-4">
        <button
          onClick={() => setView('dashboard')}
          className="p-1 hover:bg-slate-700 rounded transition"
        >
          <ChevronLeft size={20} />
        </button>
        <h2 className="text-lg font-bold">Configuración</h2>
      </div>

      <div className="space-y-4">
        {providers.map(({ id, name, icon }) => (
          <div key={id} className="bg-slate-800 border border-slate-700 rounded-lg p-4">
            <label className="block mb-2">
              <span className="text-sm font-semibold flex items-center gap-2">
                <span>{icon}</span>
                {name} API Key
              </span>
            </label>
            <input
              type="password"
              value={apiKeys[id as keyof ApiKeys] || ''}
              onChange={(e) => handleChange(id, e.target.value)}
              placeholder={`Ingresa tu API key de ${name}`}
              className="w-full bg-slate-700 border border-slate-600 rounded px-3 py-2 text-sm focus:outline-none focus:border-purple-500"
            />
            <p className="text-xs text-slate-400 mt-1">
              Se encripta localmente y nunca se envía a servidores externos
            </p>
          </div>
        ))}
      </div>

      {/* Save button */}
      <div className="flex gap-2 mt-6">
        <button
          onClick={handleSave}
          disabled={isSaving}
          className="flex-1 bg-purple-600 hover:bg-purple-700 disabled:bg-slate-700 text-white px-4 py-2 rounded-lg flex items-center justify-center gap-2 transition font-semibold"
        >
          <Save size={16} />
          {isSaving ? 'Guardando...' : 'Guardar'}
        </button>
        {saved && <div className="text-green-400 py-2 px-3 rounded bg-green-900/30">✓ Guardado</div>}
      </div>

      {/* Info */}
      <div className="bg-slate-800 border border-slate-700 rounded-lg p-3 text-xs text-slate-400 mt-4">
        <p>💡 Las claves se guardan encriptadas en tu máquina. TokenLUV nunca las envía a terceros.</p>
      </div>
    </div>
  )
}

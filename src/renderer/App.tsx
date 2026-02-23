import { useState, useEffect } from 'react'
import Dashboard from './components/Dashboard'
import Settings from './components/Settings'
import { Moon, Sun, Settings as SettingsIcon } from 'lucide-react'

declare global {
  interface Window {
    tokenLuvApi: any
  }
}

type View = 'dashboard' | 'settings'

export default function App() {
  const [view, setView] = useState<View>('dashboard')
  const [isDark, setIsDark] = useState(true)

  useEffect(() => {
    // Apply dark/light mode
    if (isDark) {
      document.documentElement.classList.add('dark')
    } else {
      document.documentElement.classList.remove('dark')
    }
  }, [isDark])

  return (
    <div className={isDark ? 'dark' : ''}>
      <div className="min-h-screen bg-slate-900 text-slate-100">
        {/* Header */}
        <header className="bg-slate-800 border-b border-slate-700 p-4 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="text-xl">💜</span>
            <h1 className="font-bold text-lg">TokenLUV</h1>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setIsDark(!isDark)}
              className="p-2 hover:bg-slate-700 rounded-lg transition"
              title={isDark ? 'Light mode' : 'Dark mode'}
            >
              {isDark ? <Sun size={18} /> : <Moon size={18} />}
            </button>
            <button
              onClick={() => setView(view === 'dashboard' ? 'settings' : 'dashboard')}
              className="p-2 hover:bg-slate-700 rounded-lg transition"
              title="Configuración"
            >
              <SettingsIcon size={18} />
            </button>
            <button
              onClick={() => window.tokenLuvApi.closeWindow()}
              className="p-2 hover:bg-red-900/50 rounded-lg transition"
              title="Cerrar"
            >
              ×
            </button>
          </div>
        </header>

        {/* Content */}
        <main className="overflow-y-auto" style={{ height: 'calc(100vh - 56px)' }}>
          {view === 'dashboard' ? <Dashboard /> : <Settings setView={setView} />}
        </main>
      </div>
    </div>
  )
}

import { useState } from 'react'
import Dashboard from './components/Dashboard'
import Settings from './components/Settings'

declare global {
  interface Window {
    tokenLuvApi: any
  }
}

// ─── Settings Window ──────────────────────────────────────────────────────────
// Electron carga index.html#settings → muestra solo el panel de configuración.
// El header del propio Settings es drag-region → ventana movible.

function SettingsWindow() {
  const handleClose = () => {
    const api = (window as any).tokenLuvApi
    if (api) api.closeWindow()
  }

  return (
    <div style={{
      width:    '100vw',
      height:   '100vh',
      backgroundColor: '#111118',
      display:  'flex',
      alignItems: 'flex-start',
      justifyContent: 'center',
      overflow: 'hidden'
    }}>
      <Settings onClose={handleClose} />
    </div>
  )
}

// ─── Main Widget Window ───────────────────────────────────────────────────────

function WidgetWindow() {
  const handleOpenSettings = () => {
    const api = (window as any).tokenLuvApi
    if (api) api.openSettings()
  }

  const handleClose = () => {
    const api = (window as any).tokenLuvApi
    if (api) api.closeWindow()
  }

  return (
    <div
      className="w-full h-screen overflow-hidden flex flex-col"
      style={{ backgroundColor: '#111118' }}
    >
      {/* Header — drag region */}
      <div
        className="drag-region flex items-center justify-between px-3 border-b"
        style={{
          height:          '36px',
          backgroundColor: '#1a1a24',
          borderColor:     '#2a2a3a',
          fontFamily:      "'JetBrains Mono', monospace",
          fontSize:        '11px',
          letterSpacing:   '0.8px',
          flexShrink:      0
        }}
      >
        {/* Title: TokenLUV♥ with terminal heart */}
        <div style={{ color: '#67e8f9', fontWeight: 'bold', display: 'flex', alignItems: 'center', gap: '4px' }}>
          <span style={{ color: '#334155' }}>┌─</span>
          <span> TokenLUV</span>
          <span style={{ color: '#ef4444' }}>♥</span>
        </div>

        {/* Controls */}
        <div className="no-drag flex items-center gap-1">
          <button
            onClick={handleOpenSettings}
            style={{
              backgroundColor: 'transparent',
              border:          'none',
              cursor:          'pointer',
              color:           '#475569',
              fontFamily:      "'JetBrains Mono', monospace",
              fontSize:        '13px',
              padding:         '3px 6px',
              borderRadius:    '3px',
              lineHeight:      1
            }}
            title="Configuración"
            onMouseEnter={e => {
              e.currentTarget.style.color = '#8b5cf6'
              e.currentTarget.style.backgroundColor = '#2a2a3a'
            }}
            onMouseLeave={e => {
              e.currentTarget.style.color = '#475569'
              e.currentTarget.style.backgroundColor = 'transparent'
            }}
          >
            ⚙
          </button>

          <button
            onClick={handleClose}
            style={{
              backgroundColor: 'transparent',
              border:          'none',
              cursor:          'pointer',
              color:           '#334155',
              fontSize:        '13px',
              padding:         '3px 6px',
              borderRadius:    '3px',
              lineHeight:      1
            }}
            title="Ocultar a bandeja"
            onMouseEnter={e => {
              e.currentTarget.style.color = '#ef4444'
              e.currentTarget.style.backgroundColor = '#2a2a3a'
            }}
            onMouseLeave={e => {
              e.currentTarget.style.color = '#334155'
              e.currentTarget.style.backgroundColor = 'transparent'
            }}
          >
            ✕
          </button>
        </div>
      </div>

      <main className="flex-1 overflow-hidden">
        <Dashboard />
      </main>
    </div>
  )
}

// ─── Root ─────────────────────────────────────────────────────────────────────

export default function App() {
  const [isSettingsWindow] = useState(() => window.location.hash === '#settings')
  return isSettingsWindow ? <SettingsWindow /> : <WidgetWindow />
}

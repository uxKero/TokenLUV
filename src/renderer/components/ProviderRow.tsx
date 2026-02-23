import { useState, useRef, useCallback } from 'react'

interface ActivityItem {
  date: string
  model: string
  usage: number
  requests: number
  promptTokens: number
  completionTokens: number
}

interface ProviderRowProps {
  name:        string
  id:          string
  used:        number | null
  limit:       number | null
  unit:        'tokens' | 'usd'
  status:      'ok' | 'error' | 'loading' | 'no-key'
  modelLabel?: string
  rawData?:    any    // raw provider data — used for tooltip
}

const COLORS: Record<string, string> = {
  anthropic:  '#E87040',
  openai:     '#74AA9C',
  openrouter: '#67e8f9',
  xai:        '#AAAAAA',
  gemini:     '#EA4335'
}

function fmtUSD(n: number): string {
  if (n === 0)       return '$0.00'
  if (n < 0.0001)    return `$${n.toFixed(6)}`
  if (n < 0.01)      return `$${n.toFixed(4)}`
  return `$${n.toFixed(2)}`
}

function fmtTokens(n: number): string {
  if (n >= 1_000_000) return `${(n / 1_000_000).toFixed(1)}M`
  if (n >= 1_000)     return `${(n / 1_000).toFixed(0)}K`
  return String(n)
}

function fmtNum(n: number): string {
  return n.toLocaleString()
}

function formatValue(
  used: number | null, limit: number | null,
  unit: 'tokens' | 'usd', status: string
): { line1: string; line2: string | null } {
  if (status === 'loading') return { line1: '─ cargando ─',  line2: null }
  if (status === 'error')   return { line1: '✗ key inválida', line2: null }
  if (status === 'no-key')  return { line1: '─ sin key ─',   line2: null }
  if (used === null && limit === null) return { line1: 'key activa', line2: 'sin API de consumo' }

  if (unit === 'usd') {
    const u = used ?? 0
    const l = limit ?? null
    if (l !== null && l > 0) {
      const rem = Math.max(0, l - u)
      return u === 0
        ? { line1: `${fmtUSD(l)} disponible`,  line2: 'sin consumo aún' }
        : { line1: `${fmtUSD(rem)} restante`,   line2: `gastado ${fmtUSD(u)} / ${fmtUSD(l)}` }
    }
    return u === 0
      ? { line1: '$0.00 gastado',    line2: 'sin consumo aún' }
      : { line1: `${fmtUSD(u)} gastado`, line2: 'sin límite' }
  } else {
    const u = used ?? 0
    if (limit !== null && limit > 0) {
      const rem = Math.max(0, limit - u)
      return { line1: `${fmtTokens(rem)} restantes`, line2: `${fmtTokens(u)} / ${fmtTokens(limit)}` }
    }
    return u === 0
      ? { line1: '0 tokens', line2: 'sin consumo aún' }
      : { line1: `${fmtTokens(u)} tokens`, line2: 'sin límite' }
  }
}

// ── Tooltip component ───────────────────────────────────────────────────────
function ActivityTooltip({
  items, color, anchorTop
}: {
  items: ActivityItem[]
  color: string
  anchorTop: number
}) {
  if (!items || items.length === 0) return null

  // Show tooltip above row if near bottom of screen, below otherwise
  const TOOLTIP_H = Math.min(items.length, 6) * 20 + 36
  const windowH   = window.innerHeight
  const showAbove = anchorTop + TOOLTIP_H + 8 > windowH - 8

  const top = showAbove ? anchorTop - TOOLTIP_H - 4 : anchorTop + 4

  return (
    <div style={{
      position:        'fixed',
      top,
      left:            8,
      right:           8,
      zIndex:          9999,
      backgroundColor: '#0f0f18',
      border:          `1px solid ${color}44`,
      borderRadius:    '4px',
      padding:         '6px 0',
      boxShadow:       `0 4px 16px rgba(0,0,0,0.6), 0 0 0 1px ${color}22`,
      pointerEvents:   'none'
    }}>
      {/* Header */}
      <div style={{
        padding:       '0 10px 4px',
        borderBottom:  '1px solid #1e1e2e',
        marginBottom:  '4px',
        display:       'flex',
        justifyContent: 'space-between',
        fontSize:      '8px',
        color:         '#475569',
        fontFamily:    "'JetBrains Mono', monospace"
      }}>
        <span style={{ color }}>▸ actividad reciente</span>
        <span>últ. 30 días · top {Math.min(items.length, 6)}</span>
      </div>

      {/* Rows */}
      {items.slice(0, 6).map((item, i) => (
        <div key={i} style={{
          display:    'flex',
          alignItems: 'center',
          padding:    '1px 10px',
          gap:        '6px',
          fontSize:   '9px',
          fontFamily: "'JetBrains Mono', monospace"
        }}>
          {/* Model name */}
          <span style={{
            color:        i === 0 ? color : '#94a3b8',
            flex:         1,
            overflow:     'hidden',
            textOverflow: 'ellipsis',
            whiteSpace:   'nowrap',
            fontWeight:   i === 0 ? 600 : 400
          }}>
            {i === 0 ? '▸ ' : '  '}{item.model}
          </span>
          {/* Cost */}
          <span style={{ color: '#64748b', flexShrink: 0, minWidth: '44px', textAlign: 'right' }}>
            {fmtUSD(item.usage)}
          </span>
          {/* Requests */}
          <span style={{ color: '#475569', flexShrink: 0, minWidth: '36px', textAlign: 'right' }}>
            {fmtNum(item.requests)}r
          </span>
          {/* Tokens */}
          <span style={{ color: '#334155', flexShrink: 0, minWidth: '44px', textAlign: 'right' }}>
            {fmtTokens(item.promptTokens + item.completionTokens)}t
          </span>
          {/* Date */}
          <span style={{ color: '#1e293b', flexShrink: 0, fontSize: '8px' }}>
            {item.date.slice(5)}
          </span>
        </div>
      ))}
    </div>
  )
}

// ── Main ProviderRow ────────────────────────────────────────────────────────
export default function ProviderRow({
  name, id, used, limit, unit, status, modelLabel, rawData
}: ProviderRowProps) {
  const color   = COLORS[id] ?? '#AAAAAA'
  const isNoKey = status === 'no-key'
  const isOk    = status === 'ok'

  const [hovered,     setHovered]     = useState(false)
  const [tooltipTop,  setTooltipTop]  = useState(0)
  const rowRef = useRef<HTMLDivElement>(null)

  // Activity items for tooltip (only OpenRouter for now)
  const activityItems: ActivityItem[] = rawData?.activityItems ?? []
  const hasTooltip = activityItems.length > 0

  const handleMouseEnter = useCallback(() => {
    if (rowRef.current) {
      const rect = rowRef.current.getBoundingClientRect()
      setTooltipTop(rect.bottom)
    }
    setHovered(true)
  }, [])

  const handleMouseLeave = useCallback(() => setHovered(false), [])

  // ── Bar math ──────────────────────────────────────────────────────────────
  const hasRealLimit = isOk && used != null && limit != null && limit > 0
  const isUnlimited  = isOk && used != null && limit === null
  const isNoUsage    = isOk && (used === 0 || used === null)

  const percentage = hasRealLimit
    ? Math.min(100, Math.round(((used ?? 0) / limit!) * 100))
    : 0

  let barFill = 'var(--term-cyan)'
  if (percentage > 80)      barFill = 'var(--term-red)'
  else if (percentage > 60) barFill = 'var(--term-amber)'

  const barWidth = isNoUsage    ? '0%'
                 : isUnlimited  ? '18%'
                 : hasRealLimit ? `${percentage}%`
                 : '0%'
  const barColor = isUnlimited ? '#2d3a4a' : barFill
  const barPulse = isUnlimited && !isNoUsage

  const { line1, line2 } = formatValue(used, limit, unit, status)
  const displayName = (modelLabel && modelLabel.trim()) ? modelLabel.trim() : name

  return (
    <>
      <div
        ref={rowRef}
        onMouseEnter={hasTooltip ? handleMouseEnter : undefined}
        onMouseLeave={hasTooltip ? handleMouseLeave : undefined}
        style={{
          display:    'flex',
          alignItems: 'center',
          width:      '100%',
          gap:        '5px',
          padding:    '3px 8px',
          opacity:    isNoKey ? 0.28 : 1,
          fontFamily: "'JetBrains Mono', monospace",
          fontSize:   '11px',
          color:      'var(--text)',
          boxSizing:  'border-box',
          cursor:     hasTooltip ? 'default' : 'default',
          backgroundColor: hovered && hasTooltip ? '#1a1a26' : 'transparent',
          borderRadius: '2px',
          transition: 'background-color 0.1s'
        }}
      >
        {/* ● bullet */}
        <span style={{ color, flexShrink: 0, fontSize: '10px' }}>●</span>

        {/* Provider / model name */}
        <span style={{
          width:        '92px',
          flexShrink:   0,
          color:        isNoKey ? '#334155' : modelLabel ? color : '#cbd5e1',
          overflow:     'hidden',
          textOverflow: 'ellipsis',
          whiteSpace:   'nowrap',
          fontSize:     modelLabel ? '9px' : '11px',
          lineHeight:   '1.1'
        }}>
          {displayName}
          {hasTooltip && !hovered && (
            <span style={{ color: '#334155', fontSize: '7px', marginLeft: '2px' }}>▾</span>
          )}
        </span>

        {/* Terminal bar */}
        <div style={{
          flex: 1, display: 'flex', alignItems: 'center',
          gap: '2px', minWidth: 0, overflow: 'hidden'
        }}>
          <span style={{ color: '#334155', flexShrink: 0 }}>[</span>
          <div style={{
            flex: 1, position: 'relative', height: '12px',
            overflow: 'hidden', display: 'flex', alignItems: 'center'
          }}>
            <span style={{
              position: 'absolute', top: '50%', transform: 'translateY(-50%)',
              left: 0, right: 0, color: '#1e293b',
              whiteSpace: 'nowrap', lineHeight: '1'
            }}>{'░'.repeat(80)}</span>

            {isOk && !isNoUsage && (
              <span style={{
                position: 'absolute', top: '50%', transform: 'translateY(-50%)',
                left: 0, width: barWidth, overflow: 'hidden',
                color: barColor, whiteSpace: 'nowrap', lineHeight: '1',
                animation:  barPulse ? 'pulse-bar 2.5s ease-in-out infinite' : 'none',
                transition: 'width 0.4s ease'
              }}>{'█'.repeat(80)}</span>
            )}
          </div>
          <span style={{ color: '#334155', flexShrink: 0 }}>]</span>
        </div>

        {/* Value — two lines */}
        <div style={{
          flexShrink: 0, textAlign: 'right', minWidth: '100px',
          display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '1px'
        }}>
          <span style={{
            color:    isNoKey || status === 'error' ? '#334155'
                    : isOk && !hasRealLimit && !isUnlimited ? '#475569' : '#94a3b8',
            fontSize: '10px', whiteSpace: 'nowrap'
          }}>{line1}</span>
          {line2 && (
            <span style={{ color: '#334155', fontSize: '8px', whiteSpace: 'nowrap' }}>{line2}</span>
          )}
        </div>
      </div>

      {/* Tooltip — rendered in fixed position, overlays other content */}
      {hovered && hasTooltip && (
        <ActivityTooltip
          items={activityItems}
          color={color}
          anchorTop={tooltipTop}
        />
      )}
    </>
  )
}

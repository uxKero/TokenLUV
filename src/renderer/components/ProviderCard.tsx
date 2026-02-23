import StatusBadge from './StatusBadge'

interface ProviderCardProps {
  name: string
  icon: string
  used: number | null | undefined
  limit: number | null | undefined
  unit: 'tokens' | 'usd'
  status: 'ok' | 'error' | 'loading'
}

export default function ProviderCard({
  name,
  icon,
  used,
  limit,
  unit,
  status
}: ProviderCardProps) {
  const percentage = used && limit ? Math.round((used / limit) * 100) : 0
  const displayValue =
    unit === 'usd'
      ? `$${used?.toFixed(2) || '0.00'} / $${limit?.toFixed(2) || '0.00'}`
      : `${(used || 0).toLocaleString()} / ${(limit || 0).toLocaleString()}`

  const barColor =
    percentage > 80 ? 'bg-term-red' : percentage > 60 ? 'bg-term-amber' : 'bg-term-green'

  const barClasses =
    percentage > 80 ? 'bg-term-red animate-pulse' : barColor

  return (
    <div className="bg-card border border-border rounded-xl p-4 hover:border-accent/50 hover:shadow-lg transition-all duration-200 glow-accent">
      {/* Header: emoji + name + status */}
      <div className="flex items-start justify-between mb-3">
        <div className="flex items-center gap-2.5">
          <span className="text-lg">{icon}</span>
          <h3 className="font-semibold text-sm font-sans">{name}</h3>
        </div>
        <StatusBadge status={status} />
      </div>

      {/* Divider */}
      <div className="h-px bg-border/40 mb-3"></div>

      {status === 'ok' && used !== null && limit !== null ? (
        <>
          {/* Value in JetBrains Mono + cyan */}
          <p className="text-xs font-mono text-term-cyan mb-3">{displayValue}</p>

          {/* Progress bar */}
          <div className="w-full bg-border rounded-full h-2.5 overflow-hidden mb-2">
            <div
              className={`h-full ${barClasses} transition-all duration-600 ease-out`}
              style={{ width: `${Math.min(percentage, 100)}%` }}
            />
          </div>

          {/* Percentage */}
          <p className="text-xs font-mono text-term-cyan">{percentage}%</p>
        </>
      ) : status === 'loading' ? (
        <div className="space-y-2">
          <div className="h-4 bg-border rounded animate-pulse w-20"></div>
          <div className="h-2 bg-border rounded animate-pulse"></div>
        </div>
      ) : status === 'error' ? (
        <p className="text-xs text-term-red font-mono">Error de conexión</p>
      ) : (
        <p className="text-xs text-text-muted font-mono">Sin configurar</p>
      )}
    </div>
  )
}

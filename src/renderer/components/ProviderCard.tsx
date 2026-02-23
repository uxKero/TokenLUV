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

  const progressBarColor =
    percentage > 80 ? 'bg-red-500' : percentage > 50 ? 'bg-yellow-500' : 'bg-green-500'

  return (
    <div className="bg-slate-800 border border-slate-700 rounded-lg p-4 hover:border-slate-600 transition">
      <div className="flex items-start justify-between mb-3">
        <div className="flex items-center gap-2">
          <span className="text-xl">{icon}</span>
          <h3 className="font-semibold text-sm">{name}</h3>
        </div>
        <StatusBadge status={status} />
      </div>

      {status === 'ok' && used !== null && limit !== null ? (
        <>
          <p className="text-xs text-slate-400 mb-2">{displayValue}</p>

          {/* Progress bar */}
          <div className="w-full bg-slate-700 rounded-full h-2 overflow-hidden mb-2">
            <div
              className={`h-full ${progressBarColor} transition-all duration-300`}
              style={{ width: `${Math.min(percentage, 100)}%` }}
            />
          </div>

          <p className="text-xs text-slate-300">{percentage}%</p>
        </>
      ) : status === 'loading' ? (
        <p className="text-xs text-slate-400 animate-pulse">Cargando...</p>
      ) : status === 'error' ? (
        <p className="text-xs text-red-400">Error de conexión</p>
      ) : (
        <p className="text-xs text-slate-400">Sin configurar</p>
      )}
    </div>
  )
}

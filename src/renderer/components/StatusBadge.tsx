interface StatusBadgeProps {
  status: 'ok' | 'error' | 'loading'
}

export default function StatusBadge({ status }: StatusBadgeProps) {
  const styles =
    status === 'ok'
      ? 'bg-green-900/50 text-green-300'
      : status === 'error'
        ? 'bg-red-900/50 text-red-300'
        : 'bg-slate-700 text-slate-300 animate-pulse'

  const symbol = status === 'ok' ? '✓' : status === 'error' ? '✕' : '⟳'

  return (
    <span className={`text-xs px-2 py-1 rounded ${styles} font-semibold`}>
      {symbol}
    </span>
  )
}

interface StatusBadgeProps {
  status: 'ok' | 'error' | 'loading'
}

export default function StatusBadge({ status }: StatusBadgeProps) {
  if (status === 'ok') {
    return (
      <span className="text-xs font-mono flex items-center gap-1">
        <span className="w-2 h-2 rounded-full bg-term-green dot-ping" />
      </span>
    )
  }

  if (status === 'error') {
    return (
      <span className="text-xs font-mono flex items-center gap-1">
        <span className="w-2 h-2 rounded-full bg-term-red" />
      </span>
    )
  }

  return (
    <span className="text-xs font-mono flex items-center gap-1 animate-spin">
      <span className="w-2 h-2 rounded-full bg-term-cyan" />
    </span>
  )
}

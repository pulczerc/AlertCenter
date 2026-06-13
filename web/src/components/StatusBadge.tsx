import type { NotificationStatus } from '../types'

const MAP: Record<NotificationStatus, { icon: string; label: string }> = {
  pending: { icon: '⏳', label: 'pending' },
  sent: { icon: '✅', label: 'sent' },
  failed: { icon: '❌', label: 'failed' },
}

// Status conveyed by text + icon, not colour alone (a11y, 07 §8).
export function StatusBadge({ status }: { status: NotificationStatus }) {
  const s = MAP[status]
  return <span className={`badge badge-${status}`}>{s.icon} {s.label}</span>
}

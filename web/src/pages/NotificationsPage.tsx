import { useCallback, useEffect, useState } from 'react'
import { api } from '../api/client'
import { StatusBadge } from '../components/StatusBadge'
import type { NotificationDto, NotificationStatus } from '../types'

const STATUSES: ('' | NotificationStatus)[] = ['', 'pending', 'sent', 'failed']

export function NotificationsPage() {
  const [items, setItems] = useState<NotificationDto[]>([])
  const [status, setStatus] = useState<'' | NotificationStatus>('')
  const [auto, setAuto] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try { setItems((await api.notifications.list(status || undefined)).items); setError(null) }
    catch (e) { setError((e as Error).message) }
  }, [status])

  useEffect(() => { void load() }, [load])
  // Off-by-default auto-refresh (RF-004-E).
  useEffect(() => {
    if (!auto) return
    const id = setInterval(() => void load(), 15000)
    return () => clearInterval(id)
  }, [auto, load])

  return (
    <section>
      <h2>Notifications</h2>
      <div className="toolbar">
        <label>Status:{' '}
          <select value={status} onChange={e => setStatus(e.target.value as '' | NotificationStatus)}>
            {STATUSES.map(s => <option key={s} value={s}>{s || 'all'}</option>)}
          </select>
        </label>
        <label><input type="checkbox" checked={auto} onChange={e => setAuto(e.target.checked)} /> auto-refresh (15s)</label>
        <button onClick={() => void load()}>⟳ Refresh</button>
      </div>
      {error && <div role="alert" className="field-error">{error}</div>}
      {items.length === 0 ? <p className="empty">No notifications yet.</p> : (
        <table>
          <thead><tr><th>When</th><th>Article</th><th>Channel</th><th>Status</th></tr></thead>
          <tbody>
            {items.map(n => (
              <tr key={n.id}>
                <td>{new Date(n.createdAt).toLocaleString()}</td>
                <td><a href={n.article.link} target="_blank" rel="noreferrer">{n.article.title}</a></td>
                <td>{n.channel}</td>
                <td><StatusBadge status={n.status} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}

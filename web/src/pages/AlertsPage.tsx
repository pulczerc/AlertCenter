import { useEffect, useState, type FormEvent } from 'react'
import { api, ApiError } from '../api/client'
import { KeywordChipsInput } from '../components/KeywordChipsInput'
import type { AlertDto, Channel, UserDto } from '../types'

export function AlertsPage() {
  const [alerts, setAlerts] = useState<AlertDto[]>([])
  const [users, setUsers] = useState<UserDto[]>([])
  const [userId, setUserId] = useState('')
  const [keywords, setKeywords] = useState<string[]>([])
  const [channel, setChannel] = useState<Channel>('email')
  const [error, setError] = useState<string | null>(null)

  async function load() {
    setAlerts((await api.alerts.list()).items)
    setUsers((await api.users.list()).items)
  }
  useEffect(() => { void load() }, [])

  const enabledUsers = users.filter(u => u.enabled)

  async function create(e: FormEvent) {
    e.preventDefault()
    if (!userId) { setError('Pick an owner.'); return }
    if (keywords.length === 0) { setError('Add at least one keyword.'); return }
    try { await api.alerts.create(userId, keywords, channel); setKeywords([]); setError(null); await load() }
    catch (err) { setError(err instanceof ApiError ? err.message : String(err)) }
  }

  async function toggle(a: AlertDto) { await api.alerts.setEnabled(a.id, !a.enabled); await load() }

  return (
    <section>
      <h2>Alerts</h2>
      <form className="row" onSubmit={create}>
        <select aria-label="owner" value={userId} onChange={e => setUserId(e.target.value)}>
          <option value="">— owner —</option>
          {enabledUsers.map(u => <option key={u.id} value={u.id}>{u.name}</option>)}
        </select>
        <KeywordChipsInput value={keywords} onChange={setKeywords} />
        <label><input type="radio" checked={channel === 'email'} onChange={() => setChannel('email')} /> email</label>
        <label><input type="radio" checked={channel === 'slack'} onChange={() => setChannel('slack')} /> slack</label>
        <button type="submit">+ New alert</button>
      </form>
      <p className="muted">Applies to news ingested from now on — existing articles aren't back-matched.</p>
      {error && <div role="alert" className="field-error">{error}</div>}
      {alerts.length === 0 ? <p className="empty">No alerts yet.</p> : (
        <table>
          <thead><tr><th>Owner</th><th>Keywords</th><th>Channel</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {alerts.map(a => (
              <tr key={a.id}>
                <td>{a.ownerName}</td>
                <td>{a.keywords.join(', ')}</td>
                <td>{a.channel}</td>
                <td>{a.enabled ? '● Enabled' : '○ Disabled'}</td>
                <td><button onClick={() => void toggle(a)}>{a.enabled ? 'Disable' : 'Enable'}</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}

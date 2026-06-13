import { useEffect, useState, type FormEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { UserDto } from '../types'

export function UsersPage() {
  const [users, setUsers] = useState<UserDto[]>([])
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function load() { setUsers((await api.users.list()).items) }
  useEffect(() => { void load() }, [])

  async function create(e: FormEvent) {
    e.preventDefault()
    try { await api.users.create(name, email); setName(''); setEmail(''); setError(null); await load() }
    catch (err) { setError(err instanceof ApiError ? err.message : String(err)) }
  }

  async function toggle(u: UserDto) { await api.users.setEnabled(u.id, !u.enabled); await load() }

  return (
    <section>
      <h2>Users</h2>
      <form className="row" onSubmit={create}>
        <input aria-label="name" placeholder="Name" value={name} onChange={e => setName(e.target.value)} required />
        <input aria-label="email" placeholder="Email" type="email" value={email} onChange={e => setEmail(e.target.value)} required />
        <button type="submit">+ New user</button>
      </form>
      {error && <div role="alert" className="field-error">{error}</div>}
      {users.length === 0 ? <p className="empty">No users yet.</p> : (
        <table>
          <thead><tr><th>Name</th><th>Email</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {users.map(u => (
              <tr key={u.id}>
                <td>{u.name}</td><td>{u.email}</td>
                <td>{u.enabled ? '● Enabled' : '○ Disabled'}</td>
                <td><button onClick={() => void toggle(u)}>{u.enabled ? 'Disable' : 'Enable'}</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}

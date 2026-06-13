import { useEffect, useState } from 'react'
import { api } from '../api/client'

export function OpsPage() {
  const [health, setHealth] = useState<{ status: string; outboxPending: number } | null>(null)
  const [last, setLast] = useState<string>('')
  const [busy, setBusy] = useState(false)

  async function refresh() { try { setHealth(await api.health()) } catch { /* ignore */ } }
  useEffect(() => { void refresh() }, [])

  async function run(label: string, fn: () => Promise<unknown>) {
    setBusy(true)
    try { const r = await fn(); setLast(`${label}: ${JSON.stringify(r)}`); await refresh() }
    catch (e) { setLast(`${label} failed: ${(e as Error).message}`) }
    finally { setBusy(false) }
  }

  return (
    <section>
      <h2>Ops / Health</h2>
      <p className="banner">Operator tools — unauthenticated in MVP.</p>
      <p>Status: <strong>{health?.status ?? '…'}</strong> · Outbox pending: <strong>{health?.outboxPending ?? '…'}</strong></p>
      <div className="toolbar">
        <button disabled={busy} onClick={() => run('Poll', api.poll)}>Poll feeds now</button>
        <button disabled={busy} onClick={() => run('Dispatch', api.dispatch)}>Dispatch outbox now</button>
        <button disabled={busy} onClick={() => void refresh()}>⟳ Health</button>
      </div>
      {last && <p className="muted">{last} <em>(session-only)</em></p>}
    </section>
  )
}

import { useState, type KeyboardEvent } from 'react'

const MAX = 60

export interface KeywordChipsInputProps {
  value: string[]
  onChange: (next: string[]) => void
}

/** Single-token keyword input (RF-003-C / RF-004-G): rejects whitespace and >60 chars inline,
 *  de-dupes case-insensitively. */
export function KeywordChipsInput({ value, onChange }: KeywordChipsInputProps) {
  const [text, setText] = useState('')
  const [error, setError] = useState<string | null>(null)

  function add(raw: string) {
    const token = raw.trim()
    if (!token) return
    if (/\s/.test(token)) { setError('Keywords must be a single word (no spaces).'); return }
    if (token.length > MAX) { setError(`Keywords must be at most ${MAX} characters.`); return }
    if (value.some(k => k.toLowerCase() === token.toLowerCase())) { setText(''); return }
    onChange([...value, token])
    setText('')
    setError(null)
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' || e.key === ',') { e.preventDefault(); add(text) }
  }

  return (
    <div>
      <div className="chips">
        {value.map(k => (
          <span key={k} className="chip">
            {k}
            <button type="button" aria-label={`remove ${k}`} onClick={() => onChange(value.filter(x => x !== k))}>×</button>
          </span>
        ))}
      </div>
      <input
        aria-label="keyword"
        placeholder="single word, then Enter (max 60 chars)"
        value={text}
        onChange={e => { setText(e.target.value); setError(null) }}
        onKeyDown={onKeyDown}
      />
      {error && <div role="alert" className="field-error">{error}</div>}
    </div>
  )
}

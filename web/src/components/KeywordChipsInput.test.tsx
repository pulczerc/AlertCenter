import { test, expect } from 'vitest'
import { useState } from 'react'
import { render, screen, fireEvent } from '@testing-library/react'
import { KeywordChipsInput } from './KeywordChipsInput'

function Harness() {
  const [value, setValue] = useState<string[]>([])
  return <KeywordChipsInput value={value} onChange={setValue} />
}

test('adds a single-word keyword on Enter', () => {
  render(<Harness />)
  const input = screen.getByLabelText('keyword')
  fireEvent.change(input, { target: { value: 'openai' } })
  fireEvent.keyDown(input, { key: 'Enter' })
  expect(screen.getByText('openai')).toBeInTheDocument()
})

test('rejects a keyword containing whitespace (RF-003-C)', () => {
  render(<Harness />)
  const input = screen.getByLabelText('keyword')
  fireEvent.change(input, { target: { value: 'interest rate' } })
  fireEvent.keyDown(input, { key: 'Enter' })
  expect(screen.getByRole('alert')).toHaveTextContent(/single word/i)
})

import { test, expect } from 'vitest'
import { ApiError } from './client'

test('ApiError surfaces the RFC-7807 detail as its message', () => {
  const err = new ApiError(409, { status: 409, title: 'Conflict', detail: 'Email is already in use' })
  expect(err.status).toBe(409)
  expect(err.message).toBe('Email is already in use')
})

test('ApiError falls back to title then status', () => {
  expect(new ApiError(422, { title: 'Unprocessable entity' }).message).toBe('Unprocessable entity')
  expect(new ApiError(500).message).toBe('HTTP 500')
})

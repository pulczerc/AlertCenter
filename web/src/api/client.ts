import type { AlertDto, Channel, NotificationDto, NotificationStatus, Page, ProblemDetails, UserDto } from '../types'

/** An HTTP error carrying the parsed RFC-7807 body (05 §8). */
export class ApiError extends Error {
  readonly status: number
  readonly problem?: ProblemDetails
  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.detail || problem?.title || `HTTP ${status}`)
    this.status = status
    this.problem = problem
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  if (!res.ok) {
    let problem: ProblemDetails | undefined
    try { problem = await res.json() } catch { /* non-JSON error body */ }
    throw new ApiError(res.status, problem)
  }
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  health: () => request<{ status: string; outboxPending: number }>('GET', '/api/v1/ops/health'),
  poll: () => request<{ added: number; enqueued: number }>('POST', '/api/v1/ops/poll'),
  dispatch: () => request<{ dispatched: number; failed: number }>('POST', '/api/v1/ops/dispatch'),

  users: {
    list: () => request<Page<UserDto>>('GET', '/api/v1/users'),
    create: (name: string, email: string) => request<UserDto>('POST', '/api/v1/users', { name, email }),
    setEnabled: (id: string, enabled: boolean) => request<UserDto>('PATCH', `/api/v1/users/${id}`, { enabled }),
  },
  alerts: {
    list: () => request<Page<AlertDto>>('GET', '/api/v1/alerts'),
    create: (userId: string, keywords: string[], channel: Channel) =>
      request<AlertDto>('POST', '/api/v1/alerts', { userId, keywords, channel }),
    setEnabled: (id: string, enabled: boolean) => request<AlertDto>('PATCH', `/api/v1/alerts/${id}`, { enabled }),
  },
  notifications: {
    list: (status?: NotificationStatus) =>
      request<Page<NotificationDto>>('GET', `/api/v1/notifications${status ? `?status=${status}` : ''}`),
  },
}

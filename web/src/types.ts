export type Channel = 'email' | 'slack'
export type NotificationStatus = 'pending' | 'sent' | 'failed'

export interface UserDto { id: string; name: string; email: string; enabled: boolean; createdAt: string }
export interface AlertDto { id: string; userId: string; ownerName: string; keywords: string[]; channel: Channel; enabled: boolean; createdAt: string }
export interface ArticleSummary { title: string; link: string; source: string; publishedAt?: string | null }
export interface NotificationDto {
  id: string; alertId: string; articleId: string; channel: Channel; status: NotificationStatus
  createdAt: string; sentAt?: string | null; lastError?: string | null; article: ArticleSummary
}
export interface Page<T> { items: T[]; page: number; pageSize: number; total: number }
export interface ProblemDetails { type?: string; title?: string; status?: number; detail?: string; errors?: Record<string, string[]> }

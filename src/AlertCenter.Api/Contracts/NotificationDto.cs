using AlertCenter.Core.Ingestion;
using AlertCenter.Core.Notifications;

namespace AlertCenter.Api.Contracts;

public sealed record ArticleSummaryDto(string Title, string Link, string Source, DateTimeOffset? PublishedAt);

/// <summary>Notification read model with an embedded article summary (avoids N+1 in the history view, FR-13).</summary>
public sealed record NotificationDto(
    Guid Id,
    Guid AlertId,
    Guid ArticleId,
    string Channel,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    string? LastError,
    ArticleSummaryDto Article)
{
    public static NotificationDto From(Notification n, Article a) => new(
        n.Id, n.AlertId, n.ArticleId,
        n.Channel.ToString().ToLowerInvariant(),
        n.Status.ToString().ToLowerInvariant(),
        n.CreatedAt, n.SentAt, n.LastError,
        new ArticleSummaryDto(a.Title, a.Link, a.Source, a.PublishedAt));
}

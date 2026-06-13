using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Notifications;

/// <summary>
/// The record that an alert matched an article (FR-6) plus its delivery status (FR-10).
/// Uniqueness of <c>(AlertId, ArticleId)</c> is enforced at the DB (FR-7, N1).
/// <see cref="Channel"/> is snapshotted at match time (N3). Retry bookkeeping lives on
/// the outbox, not here (RF-003-D).
/// </summary>
public sealed class Notification
{
    public Guid Id { get; private set; }
    public Guid AlertId { get; private set; }
    public Guid ArticleId { get; private set; }
    public Channel Channel { get; private set; }
    public NotificationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? LastError { get; private set; }

    private Notification() { } // EF

    public Notification(Guid id, Guid alertId, Guid articleId, Channel channel, DateTimeOffset createdAt)
    {
        Id = id;
        AlertId = alertId;
        ArticleId = articleId;
        Channel = channel;
        Status = NotificationStatus.Pending;
        CreatedAt = createdAt;
    }

    public void MarkSent(DateTimeOffset at)
    {
        EnsurePending();
        Status = NotificationStatus.Sent;
        SentAt = at;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        EnsurePending();
        Status = NotificationStatus.Failed;
        LastError = error;
    }

    private void EnsurePending()
    {
        if (Status != NotificationStatus.Pending)
            throw new InvalidOperationException($"Notification {Id} is already terminal ({Status})."); // N2
    }
}

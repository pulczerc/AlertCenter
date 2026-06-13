using AlertCenter.Core.Channels;

namespace AlertCenter.Core.Notifications;

/// <summary>
/// The durable dispatch work-item, 1:1 with a <see cref="Notification"/> and written in the
/// same transaction (O1/O3, ADR-001). Carries the rendered <see cref="Payload"/> (RF-005-D)
/// so dispatch needs no cross-module reads. Retry is bounded; on exhaustion the entry is Dead.
/// </summary>
public sealed class OutboxEntry
{
    public const int MaxAttempts = 5;

    public Guid Id { get; private set; }
    public Guid NotificationId { get; private set; }
    public OutboxMessage Payload { get; private set; } = default!;
    public OutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset AvailableAt { get; private set; }
    public DateTimeOffset? LeasedUntil { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private OutboxEntry() { } // EF

    public OutboxEntry(Guid id, Guid notificationId, OutboxMessage payload, DateTimeOffset now)
    {
        Id = id;
        NotificationId = notificationId;
        Payload = payload;
        Status = OutboxStatus.Pending;
        Attempts = 0;
        AvailableAt = now;
        CreatedAt = now;
    }

    /// <summary>Reserve this entry with a visibility timeout so a re-select can't grab it
    /// after the lease commits (RF-003-A).</summary>
    public void Lease(DateTimeOffset until)
    {
        LeasedUntil = until;
        AvailableAt = until;
    }

    public void MarkDone()
    {
        Status = OutboxStatus.Done;
        LeasedUntil = null;
        LastError = null;
    }

    /// <summary>Record a failed attempt; back off, or dead-letter once attempts are exhausted.</summary>
    public void Fail(string error, DateTimeOffset now, TimeSpan backoff)
    {
        Attempts++;
        LastError = error;
        LeasedUntil = null;
        if (Attempts >= MaxAttempts)
            Status = OutboxStatus.Dead;
        else
            AvailableAt = now + backoff;
    }
}

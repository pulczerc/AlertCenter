namespace AlertCenter.Core.Notifications;

/// <summary>Lifecycle of the dispatch work-item (the outbox mechanism, ADR-001).</summary>
public enum OutboxStatus
{
    Pending,
    Done,
    Dead
}

namespace AlertCenter.Core.Notifications;

/// <summary>Business-visible delivery outcome (FR-10). Pending is the only non-terminal state.</summary>
public enum NotificationStatus
{
    Pending,
    Sent,
    Failed
}

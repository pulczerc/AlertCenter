namespace AlertCenter.Core.Notifications;

/// <summary>Driven port for notification persistence (FR-6/FR-13).</summary>
public interface INotificationRepository
{
    Task<bool> ExistsAsync(Guid alertId, Guid articleId, CancellationToken ct = default); // FR-7 / N1
    Task<Notification?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Notification notification, CancellationToken ct = default);
}

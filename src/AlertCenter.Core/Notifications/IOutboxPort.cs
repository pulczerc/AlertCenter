namespace AlertCenter.Core.Notifications;

/// <summary>Driven port for the outbox: enqueue work-items and lease due ones for dispatch.</summary>
public interface IOutboxPort
{
    Task AddAsync(OutboxEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Atomically claim up to <paramref name="batch"/> due entries (pending, available, unleased),
    /// applying a visibility timeout so a concurrent dispatcher can't re-grab them (RF-003-A),
    /// and return the claimed entries (payload included) for sending.
    /// </summary>
    Task<IReadOnlyList<OutboxEntry>> LeaseAsync(int batch, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken ct = default);
}

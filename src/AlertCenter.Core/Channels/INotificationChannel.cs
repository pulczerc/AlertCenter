using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Channels;

/// <summary>
/// Driven port for a delivery channel. Receives a fully rendered <see cref="OutboxMessage"/>
/// (RF-005-D) — it performs no domain lookups. Mock is the default binding (Q-5); real
/// senders read credentials from config only (NFR-4).
/// </summary>
public interface INotificationChannel
{
    Channel Channel { get; }
    Task SendAsync(OutboxMessage message, CancellationToken ct = default);
}

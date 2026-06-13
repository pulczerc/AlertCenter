using AlertCenter.Core.Channels;
using AlertCenter.Core.Notifications;
using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Application;

/// <summary>Outcome of one dispatch pass.</summary>
public sealed record DispatchResult(int Sent, int Failed);

/// <summary>
/// Leases due outbox entries and sends each via the resolved channel, transitioning the
/// notification pending → sent | failed with bounded retry/backoff (FR-10, NFR-2, AC-3).
/// Delivery is at-least-once (RF-003-A/H): a crash after send but before commit may re-send.
/// </summary>
public sealed class DispatchOutboxUseCase
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryBackoff = TimeSpan.FromMinutes(1);

    private readonly IOutboxPort _outbox;
    private readonly INotificationRepository _notifications;
    private readonly IChannelResolver _channels;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public DispatchOutboxUseCase(
        IOutboxPort outbox, INotificationRepository notifications, IChannelResolver channels,
        IUnitOfWork uow, IClock clock)
    {
        _outbox = outbox;
        _notifications = notifications;
        _channels = channels;
        _uow = uow;
        _clock = clock;
    }

    public async Task<DispatchResult> ExecuteAsync(int batch = 20, CancellationToken ct = default)
    {
        var entries = await _outbox.LeaseAsync(batch, _clock.UtcNow, LeaseDuration, ct);
        var sent = 0;
        var failed = 0;

        foreach (var entry in entries)
        {
            var notification = await _notifications.GetAsync(entry.NotificationId, ct);
            try
            {
                await _channels.Resolve(entry.Payload.Channel).SendAsync(entry.Payload, ct);
                entry.MarkDone();
                notification?.MarkSent(_clock.UtcNow);
                sent++;
            }
            catch (Exception ex)
            {
                entry.Fail(ex.Message, _clock.UtcNow, RetryBackoff);
                if (entry.Status == OutboxStatus.Dead)
                    notification?.MarkFailed(ex.Message); // terminal only once retries are exhausted
                failed++;
            }
        }

        await _uow.SaveChangesAsync(ct);
        return new DispatchResult(sent, failed);
    }
}

using AlertCenter.Core.Alerts;
using AlertCenter.Core.Channels;
using AlertCenter.Core.Ingestion;
using AlertCenter.Core.Notifications;
using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Application;

/// <summary>
/// Drains the un-evaluated article backlog (RF-003-B), matches each against active alerts
/// (FR-5), and for each match writes a Notification + its outbox entry (with a rendered
/// payload, RF-005-D), idempotently (FR-7). The notifications, outbox rows, and each
/// article's watermark are committed in one transaction (ADR-001 §Decision.4).
/// </summary>
public sealed class EvaluateAlertsUseCase
{
    private readonly IArticleRepository _articles;
    private readonly IAlertQuery _alerts;
    private readonly INotificationRepository _notifications;
    private readonly IOutboxPort _outbox;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public EvaluateAlertsUseCase(
        IArticleRepository articles, IAlertQuery alerts, INotificationRepository notifications,
        IOutboxPort outbox, IUnitOfWork uow, IClock clock)
    {
        _articles = articles;
        _alerts = alerts;
        _notifications = notifications;
        _outbox = outbox;
        _uow = uow;
        _clock = clock;
    }

    /// <returns>Number of notifications enqueued.</returns>
    public async Task<int> ExecuteAsync(int batch = 100, CancellationToken ct = default)
    {
        var articles = await _articles.GetUnevaluatedAsync(batch, ct);
        if (articles.Count == 0)
            return 0;

        var activeAlerts = await _alerts.GetActiveWithOwnersAsync(ct);
        var enqueued = 0;

        foreach (var article in articles)
        {
            foreach (var view in activeAlerts)
            {
                if (!KeywordMatcher.Matches(article.Title, article.Summary, view.Alert.Keywords))
                    continue;
                if (await _notifications.ExistsAsync(view.Alert.Id, article.Id, ct))
                    continue; // FR-7: never duplicate (alert, article)

                var notification = new Notification(
                    Guid.NewGuid(), view.Alert.Id, article.Id, view.Alert.Channel, _clock.UtcNow);
                await _notifications.AddAsync(notification, ct);

                var message = Render(view, article);
                await _outbox.AddAsync(new OutboxEntry(Guid.NewGuid(), notification.Id, message, _clock.UtcNow), ct);
                enqueued++;
            }

            article.MarkEvaluated(_clock.UtcNow); // watermark — advanced in the same txn
        }

        await _uow.SaveChangesAsync(ct);
        return enqueued;
    }

    private static OutboxMessage Render(ActiveAlertView view, Article article)
    {
        // Recipient resolved from match-time data (N3): Email -> owner address;
        // Slack -> a display target (the real webhook is system config, NFR-4).
        var recipient = view.Alert.Channel == Channel.Email ? view.Owner.Email : view.Owner.Name;
        var subject = $"AlertCenter: {article.Title}";
        var body = $"{article.Title}{Environment.NewLine}{article.Link}{Environment.NewLine}{Environment.NewLine}{article.Summary}";
        return new OutboxMessage(view.Alert.Channel, recipient, subject, body);
    }
}

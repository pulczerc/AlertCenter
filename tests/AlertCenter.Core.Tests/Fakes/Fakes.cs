using AlertCenter.Core.Alerts;
using AlertCenter.Core.Channels;
using AlertCenter.Core.Ingestion;
using AlertCenter.Core.Notifications;
using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Tests.Fakes;

public sealed class FixedClock : IClock
{
    public FixedClock(DateTimeOffset now) => UtcNow = now;
    public DateTimeOffset UtcNow { get; set; }
    public void Advance(TimeSpan by) => UtcNow += by;
}

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task SaveChangesAsync(CancellationToken ct = default) { SaveCount++; return Task.CompletedTask; }
}

public sealed class FakeFeedSource : IFeedSource
{
    private readonly IReadOnlyList<FeedItem> _items;
    public FakeFeedSource(params FeedItem[] items) => _items = items;
    public Task<IReadOnlyList<FeedItem>> FetchAsync(CancellationToken ct = default) => Task.FromResult(_items);
}

public sealed class InMemoryArticleRepository : IArticleRepository
{
    public List<Article> Items { get; } = new();
    public Task<bool> ExistsAsync(string source, string sourceGuid, CancellationToken ct = default)
        => Task.FromResult(Items.Any(a => a.Source == source && a.SourceGuid == sourceGuid));
    public Task AddAsync(Article article, CancellationToken ct = default) { Items.Add(article); return Task.CompletedTask; }
    public Task<IReadOnlyList<Article>> GetUnevaluatedAsync(int max, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<Article>)Items.Where(a => !a.IsEvaluated).Take(max).ToList());
}

public sealed class FakeAlertQuery : IAlertQuery
{
    private readonly IReadOnlyList<ActiveAlertView> _views;
    public FakeAlertQuery(params ActiveAlertView[] views) => _views = views;
    public Task<IReadOnlyList<ActiveAlertView>> GetActiveWithOwnersAsync(CancellationToken ct = default)
        => Task.FromResult(_views);
}

public sealed class InMemoryNotificationRepository : INotificationRepository
{
    public List<Notification> Items { get; } = new();
    public Task<bool> ExistsAsync(Guid alertId, Guid articleId, CancellationToken ct = default)
        => Task.FromResult(Items.Any(n => n.AlertId == alertId && n.ArticleId == articleId));
    public Task<Notification?> GetAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(n => n.Id == id));
    public Task AddAsync(Notification notification, CancellationToken ct = default) { Items.Add(notification); return Task.CompletedTask; }
}

public sealed class InMemoryOutbox : IOutboxPort
{
    public List<OutboxEntry> Items { get; } = new();
    public Task AddAsync(OutboxEntry entry, CancellationToken ct = default) { Items.Add(entry); return Task.CompletedTask; }
    public Task<IReadOnlyList<OutboxEntry>> LeaseAsync(int batch, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        var due = Items
            .Where(e => e.Status == OutboxStatus.Pending && e.AvailableAt <= now && (e.LeasedUntil is null || e.LeasedUntil < now))
            .Take(batch)
            .ToList();
        foreach (var e in due) e.Lease(now + leaseDuration);
        return Task.FromResult((IReadOnlyList<OutboxEntry>)due);
    }
}

public sealed class RecordingChannel : INotificationChannel
{
    private readonly bool _fail;
    public RecordingChannel(Channel channel, bool fail = false) { Channel = channel; _fail = fail; }
    public Channel Channel { get; }
    public List<OutboxMessage> Sent { get; } = new();
    public Task SendAsync(OutboxMessage message, CancellationToken ct = default)
    {
        if (_fail) throw new InvalidOperationException("channel down");
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

public sealed class FakeChannelResolver : IChannelResolver
{
    private readonly INotificationChannel _channel;
    public FakeChannelResolver(INotificationChannel channel) => _channel = channel;
    public INotificationChannel Resolve(Channel channel) => _channel;
}

/// <summary>Builders for terse arrange blocks.</summary>
public static class Build
{
    public static User User(string name = "Ada", string email = "ada@example.com", bool enabled = true)
    {
        var u = new User(Guid.NewGuid(), name, email, DateTimeOffset.UtcNow);
        if (!enabled) u.Disable();
        return u;
    }

    public static Alert Alert(Guid ownerId, Channel channel = Channel.Email, params string[] keywords)
        => new(Guid.NewGuid(), ownerId, keywords.Select(Keyword.Create), channel, DateTimeOffset.UtcNow);

    public static Article Article(string title, string? summary = null, DateTimeOffset? ingestedAt = null)
        => new(Guid.NewGuid(), "reuters", Guid.NewGuid().ToString(), title, summary, "https://x/1", null,
            ingestedAt ?? DateTimeOffset.UtcNow);
}

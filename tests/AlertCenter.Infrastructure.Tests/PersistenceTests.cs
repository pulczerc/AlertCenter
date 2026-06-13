using AlertCenter.Core.Alerts;
using AlertCenter.Core.Application;
using AlertCenter.Core.Channels;
using AlertCenter.Core.Ingestion;
using AlertCenter.Core.Notifications;
using AlertCenter.Core.Shared;
using AlertCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlertCenter.Infrastructure.Tests;

public class PersistenceTests : IDisposable
{
    // Fresh, isolated in-memory DB per test (xUnit instantiates the class once per test).
    private readonly SqliteFixture _fx = new();
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-13T10:00:00Z");

    public void Dispose() => _fx.Dispose();

    private static Article Article(string title, string guid)
        => new(Guid.NewGuid(), "reuters", guid, title, "summary", "https://x/" + guid, Now, Now);

    [Fact]
    public async Task Article_source_guid_is_unique() // FR-3 / AC-1
    {
        await using var db = _fx.NewContext();
        db.Articles.Add(Article("A", "dup-guid"));
        await db.SaveChangesAsync();

        await using var db2 = _fx.NewContext();
        db2.Articles.Add(Article("B", "dup-guid"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task Alert_round_trips_with_keywords()
    {
        var userId = Guid.NewGuid();
        await using (var db = _fx.NewContext())
        {
            db.Users.Add(new User(userId, "Ada", "ada+kw@x.io", Now));
            db.Alerts.Add(new Alert(Guid.NewGuid(), userId, new[] { Keyword.Create("openai"), Keyword.Create("merger") }, Channel.Slack, Now));
            await db.SaveChangesAsync();
        }

        await using var read = _fx.NewContext();
        var alert = await read.Alerts.FirstAsync(a => a.OwnerUserId == userId);
        Assert.Equal(2, alert.Keywords.Count);
        Assert.Equal(Channel.Slack, alert.Channel);
    }

    [Fact]
    public async Task ActiveAlertQuery_excludes_disabled_owner()
    {
        var enabledUser = new User(Guid.NewGuid(), "On", "on@x.io", Now);
        var disabledUser = new User(Guid.NewGuid(), "Off", "off@x.io", Now);
        disabledUser.Disable();
        await using (var db = _fx.NewContext())
        {
            db.Users.AddRange(enabledUser, disabledUser);
            db.Alerts.Add(new Alert(Guid.NewGuid(), enabledUser.Id, new[] { Keyword.Create("alpha") }, Channel.Email, Now));
            db.Alerts.Add(new Alert(Guid.NewGuid(), disabledUser.Id, new[] { Keyword.Create("beta") }, Channel.Email, Now));
            await db.SaveChangesAsync();
        }

        await using var read = _fx.NewContext();
        var active = await new AlertQuery(read).GetActiveWithOwnersAsync();
        Assert.DoesNotContain(active, v => v.Owner.Id == disabledUser.Id);
        Assert.Contains(active, v => v.Owner.Id == enabledUser.Id);
    }

    [Fact]
    public async Task Evaluate_then_dispatch_persists_a_sent_notification() // AC-2 + AC-3 through real EF
    {
        var userId = Guid.NewGuid();
        var clock = new TestClock(Now);
        await using (var seed = _fx.NewContext())
        {
            seed.Users.Add(new User(userId, "Ada", "ada+slice@x.io", Now));
            seed.Alerts.Add(new Alert(Guid.NewGuid(), userId, new[] { Keyword.Create("openai") }, Channel.Email, Now));
            seed.Articles.Add(Article("OpenAI announces a merger", "slice-1"));
            await seed.SaveChangesAsync();
        }

        // evaluate
        await using (var db = _fx.NewContext())
        {
            var evaluate = new EvaluateAlertsUseCase(
                new ArticleRepository(db), new AlertQuery(db), new NotificationRepository(db),
                new OutboxRepository(db), db, clock);
            Assert.Equal(1, await evaluate.ExecuteAsync());
        }

        // dispatch
        var channel = new RecordingChannel(Channel.Email);
        await using (var db = _fx.NewContext())
        {
            var dispatch = new DispatchOutboxUseCase(
                new OutboxRepository(db), new NotificationRepository(db),
                new SingleChannelResolver(channel), db, clock);
            Assert.Equal(new DispatchResult(1, 0), await dispatch.ExecuteAsync());
        }

        await using var verify = _fx.NewContext();
        var notification = await verify.Notifications.SingleAsync();
        Assert.Equal(NotificationStatus.Sent, notification.Status);
        Assert.Single(channel.Sent);
        Assert.Equal("ada+slice@x.io", channel.Sent[0].Recipient);
        Assert.True((await verify.Articles.SingleAsync()).IsEvaluated);
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class RecordingChannel : INotificationChannel
    {
        public RecordingChannel(Channel channel) => Channel = channel;
        public Channel Channel { get; }
        public List<OutboxMessage> Sent { get; } = new();
        public Task SendAsync(OutboxMessage message, CancellationToken ct = default) { Sent.Add(message); return Task.CompletedTask; }
    }

    private sealed class SingleChannelResolver : IChannelResolver
    {
        private readonly INotificationChannel _channel;
        public SingleChannelResolver(INotificationChannel channel) => _channel = channel;
        public INotificationChannel Resolve(Channel channel) => _channel;
    }
}

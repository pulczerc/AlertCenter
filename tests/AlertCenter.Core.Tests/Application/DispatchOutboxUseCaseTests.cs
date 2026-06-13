using AlertCenter.Core.Application;
using AlertCenter.Core.Channels;
using AlertCenter.Core.Notifications;
using AlertCenter.Core.Shared;
using AlertCenter.Core.Tests.Fakes;

namespace AlertCenter.Core.Tests.Application;

public class DispatchOutboxUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-13T10:00:00Z");

    private static (InMemoryOutbox outbox, InMemoryNotificationRepository notifications, Notification notification)
        Seed()
    {
        var notification = new Notification(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Channel.Email, Now);
        var notifications = new InMemoryNotificationRepository();
        notifications.Items.Add(notification);
        var outbox = new InMemoryOutbox();
        outbox.Items.Add(new OutboxEntry(Guid.NewGuid(), notification.Id,
            new OutboxMessage(Channel.Email, "a@b.c", "subj", "body"), Now));
        return (outbox, notifications, notification);
    }

    [Fact]
    public async Task Sends_then_marks_notification_sent_and_outbox_done() // AC-3
    {
        var (outbox, notifications, notification) = Seed();
        var channel = new RecordingChannel(Channel.Email);
        var sut = new DispatchOutboxUseCase(outbox, notifications, new FakeChannelResolver(channel),
            new FakeUnitOfWork(), new FixedClock(Now));

        var result = await sut.ExecuteAsync();

        Assert.Equal(new DispatchResult(1, 0), result);
        Assert.Single(channel.Sent);
        Assert.Equal(OutboxStatus.Done, outbox.Items[0].Status);
        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }

    [Fact]
    public async Task Failed_send_backs_off_and_keeps_notification_pending()
    {
        var (outbox, notifications, notification) = Seed();
        var sut = new DispatchOutboxUseCase(outbox, notifications, new FakeChannelResolver(new RecordingChannel(Channel.Email, fail: true)),
            new FakeUnitOfWork(), new FixedClock(Now));

        var result = await sut.ExecuteAsync();

        Assert.Equal(new DispatchResult(0, 1), result);
        Assert.Equal(1, outbox.Items[0].Attempts);
        Assert.Equal(OutboxStatus.Pending, outbox.Items[0].Status);   // still retryable
        Assert.Equal(NotificationStatus.Pending, notification.Status); // not terminal yet
    }

    [Fact]
    public async Task Dead_letters_and_marks_failed_after_max_attempts()
    {
        var (outbox, notifications, notification) = Seed();
        var clock = new FixedClock(Now);
        var sut = new DispatchOutboxUseCase(outbox, notifications, new FakeChannelResolver(new RecordingChannel(Channel.Email, fail: true)),
            new FakeUnitOfWork(), clock);

        for (var i = 0; i < OutboxEntry.MaxAttempts; i++)
        {
            await sut.ExecuteAsync();
            clock.Advance(TimeSpan.FromMinutes(5)); // move past the backoff so the entry is due again
        }

        Assert.Equal(OutboxStatus.Dead, outbox.Items[0].Status);
        Assert.Equal(NotificationStatus.Failed, notification.Status);
    }
}

using AlertCenter.Core.Notifications;
using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Tests.Notifications;

public class NotificationTests
{
    private static Notification New()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Channel.Email, DateTimeOffset.UtcNow);

    [Fact]
    public void Starts_pending() => Assert.Equal(NotificationStatus.Pending, New().Status);

    [Fact]
    public void MarkSent_sets_status_and_time()
    {
        var n = New();
        var at = DateTimeOffset.UtcNow;
        n.MarkSent(at);
        Assert.Equal(NotificationStatus.Sent, n.Status);
        Assert.Equal(at, n.SentAt);
    }

    [Fact]
    public void MarkFailed_records_error()
    {
        var n = New();
        n.MarkFailed("smtp timeout");
        Assert.Equal(NotificationStatus.Failed, n.Status);
        Assert.Equal("smtp timeout", n.LastError);
    }

    [Fact]
    public void Cannot_transition_out_of_a_terminal_state() // N2
    {
        var n = New();
        n.MarkSent(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => n.MarkFailed("nope"));
    }
}

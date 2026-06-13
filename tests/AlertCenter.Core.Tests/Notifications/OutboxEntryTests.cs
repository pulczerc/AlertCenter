using AlertCenter.Core.Channels;
using AlertCenter.Core.Notifications;
using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Tests.Notifications;

public class OutboxEntryTests
{
    private static OutboxEntry New(DateTimeOffset now)
        => new(Guid.NewGuid(), Guid.NewGuid(), new OutboxMessage(Channel.Email, "a@b.c", "subject", "body"), now);

    [Fact]
    public void Lease_pushes_available_at_as_a_visibility_timeout() // RF-003-A
    {
        var now = DateTimeOffset.UtcNow;
        var entry = New(now);
        var until = now.AddSeconds(30);

        entry.Lease(until);

        Assert.Equal(until, entry.LeasedUntil);
        Assert.Equal(until, entry.AvailableAt);
    }

    [Fact]
    public void Fail_increments_attempts_and_backs_off()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = New(now);

        entry.Fail("boom", now, TimeSpan.FromMinutes(1));

        Assert.Equal(1, entry.Attempts);
        Assert.Equal(OutboxStatus.Pending, entry.Status);
        Assert.Equal(now.AddMinutes(1), entry.AvailableAt);
        Assert.Null(entry.LeasedUntil);
    }

    [Fact]
    public void Dead_letters_after_max_attempts()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = New(now);

        for (var i = 0; i < OutboxEntry.MaxAttempts; i++)
            entry.Fail("boom", now, TimeSpan.FromMinutes(1));

        Assert.Equal(OutboxStatus.Dead, entry.Status);
    }

    [Fact]
    public void MarkDone_completes_the_entry()
    {
        var entry = New(DateTimeOffset.UtcNow);
        entry.MarkDone();
        Assert.Equal(OutboxStatus.Done, entry.Status);
        Assert.Null(entry.LeasedUntil);
    }
}

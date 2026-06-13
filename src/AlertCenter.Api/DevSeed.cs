using AlertCenter.Core.Alerts;
using AlertCenter.Core.Shared;
using AlertCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlertCenter.Api;

/// <summary>Seeds a demo user + a couple of alerts in Development so the SPA shows data
/// on first launch (RF-005-G). Idempotent: only seeds an empty database.</summary>
public static class DevSeed
{
    public static async Task EnsureSeededAsync(AlertCenterDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        var now = DateTimeOffset.UtcNow;
        var user = new User(Guid.NewGuid(), "Demo Admin", "demo@alertcenter.local", now);
        db.Users.Add(user);
        db.Alerts.Add(new Alert(Guid.NewGuid(), user.Id,
            new[] { Keyword.Create("police"), Keyword.Create("government") }, Channel.Email, now));
        db.Alerts.Add(new Alert(Guid.NewGuid(), user.Id,
            new[] { Keyword.Create("world"), Keyword.Create("ukraine") }, Channel.Slack, now));

        await db.SaveChangesAsync();
    }
}

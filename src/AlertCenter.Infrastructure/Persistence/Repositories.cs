using AlertCenter.Core.Alerts;
using AlertCenter.Core.Ingestion;
using AlertCenter.Core.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AlertCenter.Infrastructure.Persistence;

public sealed class ArticleRepository : IArticleRepository
{
    private readonly AlertCenterDbContext _db;
    public ArticleRepository(AlertCenterDbContext db) => _db = db;

    public Task<bool> ExistsAsync(string source, string sourceGuid, CancellationToken ct = default)
        => _db.Articles.AnyAsync(a => a.Source == source && a.SourceGuid == sourceGuid, ct);

    public async Task AddAsync(Article article, CancellationToken ct = default)
        => await _db.Articles.AddAsync(article, ct);

    public async Task<IReadOnlyList<Article>> GetUnevaluatedAsync(int max, CancellationToken ct = default)
    {
        // SQLite can't ORDER BY a DateTimeOffset (RF-005-H); filter in SQL, order in memory.
        var rows = await _db.Articles.Where(a => a.EvaluatedAt == null).ToListAsync(ct);
        return rows.OrderBy(a => a.IngestedAt).Take(max).ToList();
    }
}

public sealed class UserRepository : IUserRepository
{
    private readonly AlertCenterDbContext _db;
    public UserRepository(AlertCenterDbContext db) => _db = db;

    public Task<User?> GetAsync(Guid id, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => _db.Users.AnyAsync(u => u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _db.Users.AddAsync(user, ct);
}

public sealed class AlertRepository : IAlertRepository
{
    private readonly AlertCenterDbContext _db;
    public AlertRepository(AlertCenterDbContext db) => _db = db;

    public Task<Alert?> GetAsync(Guid id, CancellationToken ct = default)
        => _db.Alerts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(Alert alert, CancellationToken ct = default)
        => await _db.Alerts.AddAsync(alert, ct);
}

public sealed class AlertQuery : IAlertQuery
{
    private readonly AlertCenterDbContext _db;
    public AlertQuery(AlertCenterDbContext db) => _db = db;

    public async Task<IReadOnlyList<ActiveAlertView>> GetActiveWithOwnersAsync(CancellationToken ct = default)
    {
        var rows = await (
            from a in _db.Alerts
            join u in _db.Users on a.OwnerUserId equals u.Id
            where a.Enabled && u.Enabled              // active = alert enabled AND owner enabled
            select new { a, u }).ToListAsync(ct);

        return rows.Select(r => new ActiveAlertView(r.a, r.u)).ToList();
    }
}

public sealed class NotificationRepository : INotificationRepository
{
    private readonly AlertCenterDbContext _db;
    public NotificationRepository(AlertCenterDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid alertId, Guid articleId, CancellationToken ct = default)
        => _db.Notifications.AnyAsync(n => n.AlertId == alertId && n.ArticleId == articleId, ct);

    public Task<Notification?> GetAsync(Guid id, CancellationToken ct = default)
        => _db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task AddAsync(Notification notification, CancellationToken ct = default)
        => await _db.Notifications.AddAsync(notification, ct);
}

public sealed class OutboxRepository : IOutboxPort
{
    private readonly AlertCenterDbContext _db;
    public OutboxRepository(AlertCenterDbContext db) => _db = db;

    public async Task AddAsync(OutboxEntry entry, CancellationToken ct = default)
        => await _db.Outbox.AddAsync(entry, ct);

    public async Task<IReadOnlyList<OutboxEntry>> LeaseAsync(int batch, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        // Filter by status in SQL; apply the time/visibility window + ordering in memory
        // (SQLite can't ORDER BY / compare DateTimeOffset reliably — RF-005-H). Demo scale.
        var pending = await _db.Outbox.Where(e => e.Status == OutboxStatus.Pending).ToListAsync(ct);
        var due = pending
            .Where(e => e.AvailableAt <= now && (e.LeasedUntil is null || e.LeasedUntil < now))
            .OrderBy(e => e.AvailableAt)
            .Take(batch)
            .ToList();

        foreach (var entry in due)
            entry.Lease(now + leaseDuration);   // visibility timeout (RF-003-A)

        await _db.SaveChangesAsync(ct);          // commit the claim before sending
        return due;
    }
}

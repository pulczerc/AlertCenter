using AlertCenter.Core.Alerts;
using AlertCenter.Core.Ingestion;
using AlertCenter.Core.Notifications;
using AlertCenter.Core.Shared;
using Microsoft.EntityFrameworkCore;

namespace AlertCenter.Infrastructure.Persistence;

/// <summary>
/// EF Core model for AlertCenter. Mirrors <c>06-db-design.md</c>; the DbContext also
/// serves as the <see cref="IUnitOfWork"/> (one SaveChanges = one transaction).
/// Slice uses SQLite; the model is provider-agnostic.
/// </summary>
public sealed class AlertCenterDbContext : DbContext, IUnitOfWork
{
    public AlertCenterDbContext(DbContextOptions<AlertCenterDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxEntry> Outbox => Set<OutboxEntry>();

    Task IUnitOfWork.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Email).HasMaxLength(254);
            e.HasIndex(x => x.Email).IsUnique();                  // U1
        });

        b.Entity<Alert>(e =>
        {
            e.ToTable("alerts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.OwnerUserId);

            // Keywords as an owned child table -> unique(alert_id, normalized) (AL3, FR-4).
            e.OwnsMany(x => x.Keywords, kb =>
            {
                kb.ToTable("alert_keywords");
                kb.WithOwner().HasForeignKey("AlertId");
                kb.Property(k => k.Text).HasColumnName("keyword").HasMaxLength(Keyword.MaxLength);
                kb.Property(k => k.Normalized).HasColumnName("normalized").HasMaxLength(Keyword.MaxLength);
                kb.HasKey("AlertId", "Normalized");
            });
            e.Navigation(x => x.Keywords).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<Article>(e =>
        {
            e.ToTable("articles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Source).HasMaxLength(64);
            e.Property(x => x.SourceGuid).HasMaxLength(512);
            e.HasIndex(x => new { x.Source, x.SourceGuid }).IsUnique();   // FR-3
            e.HasIndex(x => x.EvaluatedAt);                               // matching backlog (RF-003-B)
        });

        b.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => new { x.AlertId, x.ArticleId }).IsUnique();   // FR-7 / N1
            e.HasIndex(x => x.Status);
        });

        b.Entity<OutboxEntry>(e =>
        {
            e.ToTable("outbox");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.NotificationId).IsUnique();                 // O1 (1:1)
            e.HasIndex(x => new { x.Status, x.AvailableAt });             // lease query

            // Rendered payload (RF-005-D) stored as columns on the outbox row.
            e.OwnsOne(x => x.Payload, pb =>
            {
                pb.Property(p => p.Channel).HasConversion<string>().HasColumnName("payload_channel").HasMaxLength(16);
                pb.Property(p => p.Recipient).HasColumnName("payload_recipient");
                pb.Property(p => p.Subject).HasColumnName("payload_subject");
                pb.Property(p => p.Body).HasColumnName("payload_body");
            });
            e.Navigation(x => x.Payload).IsRequired();
        });
    }
}

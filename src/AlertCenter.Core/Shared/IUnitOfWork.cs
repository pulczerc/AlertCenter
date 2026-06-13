namespace AlertCenter.Core.Shared;

/// <summary>
/// Commits a unit of work. One <see cref="SaveChangesAsync"/> = one transaction —
/// this is how the evaluate→enqueue step writes notification + outbox + watermark
/// atomically (ADR-001 §Decision.4).
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}

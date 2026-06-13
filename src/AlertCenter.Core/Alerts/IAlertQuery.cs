namespace AlertCenter.Core.Alerts;

/// <summary>An active alert paired with its owner — the read model the matcher needs
/// (keywords + channel) and the enqueuer needs (owner email for the rendered payload).</summary>
public sealed record ActiveAlertView(Alert Alert, User Owner);

/// <summary>
/// Public cross-module port (ADR-002 M-3). "Active" = <c>alert.Enabled AND owner.Enabled</c>
/// (Domain AL <c>Active</c>); the adapter applies that filter.
/// </summary>
public interface IAlertQuery
{
    Task<IReadOnlyList<ActiveAlertView>> GetActiveWithOwnersAsync(CancellationToken ct = default);
}

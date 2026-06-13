namespace AlertCenter.Core.Alerts;

/// <summary>Driven port for alert persistence (FR-4/FR-12).</summary>
public interface IAlertRepository
{
    Task<Alert?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Alert alert, CancellationToken ct = default);
}

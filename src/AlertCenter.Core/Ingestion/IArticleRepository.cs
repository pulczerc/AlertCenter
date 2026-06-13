namespace AlertCenter.Core.Ingestion;

/// <summary>Driven port for article persistence (dedup + the evaluation backlog).</summary>
public interface IArticleRepository
{
    Task<bool> ExistsAsync(string source, string sourceGuid, CancellationToken ct = default); // FR-3
    Task AddAsync(Article article, CancellationToken ct = default);

    /// <summary>Articles not yet evaluated against alerts (restartable matching, RF-003-B).</summary>
    Task<IReadOnlyList<Article>> GetUnevaluatedAsync(int max, CancellationToken ct = default);
}

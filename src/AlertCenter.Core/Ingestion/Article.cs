namespace AlertCenter.Core.Ingestion;

/// <summary>
/// A normalized news item (FR-2). Immutable after ingestion except the
/// <see cref="EvaluatedAt"/> watermark, which makes matching restartable (A3, RF-003-B).
/// Uniqueness of <c>(Source, SourceGuid)</c> is enforced at the DB (FR-3).
/// </summary>
public sealed class Article
{
    public Guid Id { get; private set; }
    public string Source { get; private set; } = default!;
    public string SourceGuid { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Summary { get; private set; } = default!;
    public string Link { get; private set; } = default!;
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset IngestedAt { get; private set; }
    public DateTimeOffset? EvaluatedAt { get; private set; }

    private Article() { } // EF

    public Article(
        Guid id, string source, string sourceGuid, string title, string? summary,
        string link, DateTimeOffset? publishedAt, DateTimeOffset ingestedAt)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("Source is required.", nameof(source));
        if (string.IsNullOrWhiteSpace(sourceGuid)) throw new ArgumentException("SourceGuid is required.", nameof(sourceGuid));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));   // A2
        if (string.IsNullOrWhiteSpace(link)) throw new ArgumentException("Link is required.", nameof(link));

        Id = id;
        Source = source;
        SourceGuid = sourceGuid;
        Title = title;
        Summary = summary ?? string.Empty;
        Link = link;
        PublishedAt = publishedAt;
        IngestedAt = ingestedAt;
    }

    /// <summary>True until evaluated against alerts (the matching backlog, RF-003-B).</summary>
    public bool IsEvaluated => EvaluatedAt is not null;

    public void MarkEvaluated(DateTimeOffset at) => EvaluatedAt = at;
}

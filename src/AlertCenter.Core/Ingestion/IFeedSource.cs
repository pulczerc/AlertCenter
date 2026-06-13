namespace AlertCenter.Core.Ingestion;

/// <summary>A raw item fetched from a feed, before normalization into an <see cref="Article"/>.</summary>
public sealed record FeedItem(
    string Source,
    string SourceGuid,
    string Title,
    string? Summary,
    string Link,
    DateTimeOffset? PublishedAt);

/// <summary>Driven port: fetches raw items from the configured feeds (FR-1/FR-2).</summary>
public interface IFeedSource
{
    Task<IReadOnlyList<FeedItem>> FetchAsync(CancellationToken ct = default);
}

using AlertCenter.Core.Ingestion;
using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Application;

/// <summary>Fetches feed items and persists the new ones, de-duplicated by (source, guid) (FR-1..3).</summary>
public sealed class PollFeedsUseCase
{
    private readonly IFeedSource _feeds;
    private readonly IArticleRepository _articles;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public PollFeedsUseCase(IFeedSource feeds, IArticleRepository articles, IUnitOfWork uow, IClock clock)
    {
        _feeds = feeds;
        _articles = articles;
        _uow = uow;
        _clock = clock;
    }

    /// <returns>(fetched, added) counts.</returns>
    public async Task<(int Fetched, int Added)> ExecuteAsync(CancellationToken ct = default)
    {
        var items = await _feeds.FetchAsync(ct);
        var added = 0;

        foreach (var item in items)
        {
            if (await _articles.ExistsAsync(item.Source, item.SourceGuid, ct))
                continue; // FR-3 dedup (DB unique is the backstop)

            var article = new Article(
                Guid.NewGuid(), item.Source, item.SourceGuid, item.Title, item.Summary,
                item.Link, item.PublishedAt, _clock.UtcNow);

            await _articles.AddAsync(article, ct);
            added++;
        }

        await _uow.SaveChangesAsync(ct);
        return (items.Count, added);
    }
}

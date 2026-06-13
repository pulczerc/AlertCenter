using AlertCenter.Core.Ingestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlertCenter.Infrastructure.Feeds;

/// <summary>Fetches each configured feed over HTTP and parses it; a failing feed is logged and skipped (R-2).</summary>
public sealed class RssFeedSource : IFeedSource
{
    private readonly HttpClient _http;
    private readonly FeedOptions _options;
    private readonly ILogger<RssFeedSource> _log;

    public RssFeedSource(HttpClient http, IOptions<FeedOptions> options, ILogger<RssFeedSource> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    public async Task<IReadOnlyList<FeedItem>> FetchAsync(CancellationToken ct = default)
    {
        var all = new List<FeedItem>();
        foreach (var feed in _options.Sources)
        {
            try
            {
                await using var stream = await _http.GetStreamAsync(feed.Url, ct);
                using var reader = new StreamReader(stream);
                all.AddRange(RssParser.Parse(feed.Source, reader));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Feed '{Source}' ({Url}) failed; skipping", feed.Source, feed.Url);
            }
        }
        return all;
    }
}

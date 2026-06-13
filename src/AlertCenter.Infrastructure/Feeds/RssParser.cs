using System.ServiceModel.Syndication;
using System.Xml;
using AlertCenter.Core.Ingestion;

namespace AlertCenter.Infrastructure.Feeds;

/// <summary>Pure RSS/Atom → <see cref="FeedItem"/> parsing (FR-2), kept separate from I/O for testability.</summary>
public static class RssParser
{
    public static IReadOnlyList<FeedItem> Parse(string source, TextReader reader)
    {
        var items = new List<FeedItem>();
        using var xml = XmlReader.Create(reader);
        var feed = SyndicationFeed.Load(xml);
        if (feed is null) return items;

        foreach (var item in feed.Items)
        {
            var link = item.Links.FirstOrDefault()?.Uri?.ToString();
            var guid = !string.IsNullOrWhiteSpace(item.Id) ? item.Id : link;
            var title = item.Title?.Text;

            if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
                continue; // skip malformed items, keep the rest (R-2)

            DateTimeOffset? published = item.PublishDate == default ? null : item.PublishDate;
            items.Add(new FeedItem(source, guid!, title!, item.Summary?.Text, link!, published));
        }

        return items;
    }
}

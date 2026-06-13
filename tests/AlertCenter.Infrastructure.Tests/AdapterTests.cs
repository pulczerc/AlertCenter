using AlertCenter.Core.Channels;
using AlertCenter.Core.Shared;
using AlertCenter.Infrastructure.Channels;
using AlertCenter.Infrastructure.Feeds;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlertCenter.Infrastructure.Tests;

public class RssParserTests
{
    private const string Rss = """
        <?xml version="1.0" encoding="utf-8"?>
        <rss version="2.0"><channel><title>Test feed</title>
          <item>
            <title>OpenAI announces merger</title>
            <link>https://example.com/1</link>
            <guid>guid-1</guid>
            <description>a big deal</description>
            <pubDate>Sat, 13 Jun 2026 09:55:00 GMT</pubDate>
          </item>
          <item>
            <title>Second story</title>
            <link>https://example.com/2</link>
            <guid>guid-2</guid>
          </item>
        </channel></rss>
        """;

    [Fact]
    public void Parses_items_into_feed_items()
    {
        var items = RssParser.Parse("reuters", new StringReader(Rss));

        Assert.Equal(2, items.Count);
        Assert.Equal("reuters", items[0].Source);
        Assert.Equal("guid-1", items[0].SourceGuid);
        Assert.Equal("OpenAI announces merger", items[0].Title);
        Assert.Equal("a big deal", items[0].Summary);
        Assert.NotNull(items[0].PublishedAt);
    }
}

public class ChannelResolverTests
{
    [Fact]
    public void Resolves_channel_by_enum()
    {
        var email = new MockEmailChannel(NullLogger<MockEmailChannel>.Instance);
        var slack = new MockSlackChannel(NullLogger<MockSlackChannel>.Instance);
        var resolver = new ChannelResolver(new INotificationChannel[] { email, slack });

        Assert.Same(email, resolver.Resolve(Channel.Email));
        Assert.Same(slack, resolver.Resolve(Channel.Slack));
    }
}

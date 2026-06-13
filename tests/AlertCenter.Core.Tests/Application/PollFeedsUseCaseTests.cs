using AlertCenter.Core.Application;
using AlertCenter.Core.Ingestion;
using AlertCenter.Core.Tests.Fakes;

namespace AlertCenter.Core.Tests.Application;

public class PollFeedsUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-13T10:00:00Z");

    private static FeedItem Item(string guid, string title)
        => new("reuters", guid, title, "summary", "https://x/" + guid, Now);

    [Fact]
    public async Task Ingests_new_items()
    {
        var articles = new InMemoryArticleRepository();
        var uow = new FakeUnitOfWork();
        var sut = new PollFeedsUseCase(new FakeFeedSource(Item("g1", "A"), Item("g2", "B")), articles, uow, new FixedClock(Now));

        var (fetched, added) = await sut.ExecuteAsync();

        Assert.Equal(2, fetched);
        Assert.Equal(2, added);
        Assert.Equal(2, articles.Items.Count);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task Skips_already_seen_items() // FR-3
    {
        var articles = new InMemoryArticleRepository();
        // pre-seed g1
        await new PollFeedsUseCase(new FakeFeedSource(Item("g1", "A")), articles, new FakeUnitOfWork(), new FixedClock(Now)).ExecuteAsync();

        var sut = new PollFeedsUseCase(new FakeFeedSource(Item("g1", "A-dup"), Item("g2", "B")), articles, new FakeUnitOfWork(), new FixedClock(Now));
        var (_, added) = await sut.ExecuteAsync();

        Assert.Equal(1, added);            // only g2 is new
        Assert.Equal(2, articles.Items.Count);
    }
}

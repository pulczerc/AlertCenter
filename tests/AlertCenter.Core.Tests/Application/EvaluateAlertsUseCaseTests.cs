using AlertCenter.Core.Application;
using AlertCenter.Core.Alerts;
using AlertCenter.Core.Notifications;
using AlertCenter.Core.Shared;
using AlertCenter.Core.Tests.Fakes;

namespace AlertCenter.Core.Tests.Application;

public class EvaluateAlertsUseCaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-13T10:00:00Z");

    private static EvaluateAlertsUseCase Sut(
        InMemoryArticleRepository articles, FakeAlertQuery alerts,
        InMemoryNotificationRepository notifications, InMemoryOutbox outbox)
        => new(articles, alerts, notifications, outbox, new FakeUnitOfWork(), new FixedClock(Now));

    [Fact]
    public async Task Creates_one_notification_and_outbox_entry_on_match() // AC-2
    {
        var owner = Build.User();
        var alert = Build.Alert(owner.Id, Channel.Email, "openai");
        var articles = new InMemoryArticleRepository();
        articles.Items.Add(Build.Article("OpenAI announces a merger"));
        var notifications = new InMemoryNotificationRepository();
        var outbox = new InMemoryOutbox();

        var enqueued = await Sut(articles, new FakeAlertQuery(new ActiveAlertView(alert, owner)), notifications, outbox).ExecuteAsync();

        Assert.Equal(1, enqueued);
        Assert.Single(notifications.Items);
        Assert.Single(outbox.Items);
        Assert.True(articles.Items[0].IsEvaluated);
    }

    [Fact]
    public async Task Does_not_duplicate_for_same_alert_and_article() // FR-7
    {
        var owner = Build.User();
        var alert = Build.Alert(owner.Id, Channel.Email, "openai");
        var article = Build.Article("OpenAI news");
        var articles = new InMemoryArticleRepository();
        articles.Items.Add(article);
        var notifications = new InMemoryNotificationRepository();
        notifications.Items.Add(new Notification(Guid.NewGuid(), alert.Id, article.Id, Channel.Email, Now)); // already exists
        var outbox = new InMemoryOutbox();

        var enqueued = await Sut(articles, new FakeAlertQuery(new ActiveAlertView(alert, owner)), notifications, outbox).ExecuteAsync();

        Assert.Equal(0, enqueued);
        Assert.Empty(outbox.Items);
    }

    [Fact]
    public async Task Marks_article_evaluated_even_when_nothing_matches() // RF-003-B
    {
        var owner = Build.User();
        var alert = Build.Alert(owner.Id, Channel.Email, "bitcoin");
        var articles = new InMemoryArticleRepository();
        articles.Items.Add(Build.Article("OpenAI news"));
        var notifications = new InMemoryNotificationRepository();

        var enqueued = await Sut(articles, new FakeAlertQuery(new ActiveAlertView(alert, owner)), notifications, new InMemoryOutbox()).ExecuteAsync();

        Assert.Equal(0, enqueued);
        Assert.True(articles.Items[0].IsEvaluated);
    }

    [Fact]
    public async Task Renders_email_payload_with_owner_address() // RF-005-D
    {
        var owner = Build.User(email: "ada@lovelace.dev");
        var alert = Build.Alert(owner.Id, Channel.Email, "openai");
        var articles = new InMemoryArticleRepository();
        articles.Items.Add(Build.Article("OpenAI launch"));
        var outbox = new InMemoryOutbox();

        await Sut(articles, new FakeAlertQuery(new ActiveAlertView(alert, owner)), new InMemoryNotificationRepository(), outbox).ExecuteAsync();

        Assert.Equal("ada@lovelace.dev", outbox.Items[0].Payload.Recipient);
        Assert.Equal(Channel.Email, outbox.Items[0].Payload.Channel);
    }
}

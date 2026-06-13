using System.Net;
using System.Net.Http.Json;
using AlertCenter.Core.Alerts;
using AlertCenter.Core.Shared;
using AlertCenter.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AlertCenter.Api.Tests;

public class VerticalSliceTests : IDisposable
{
    // Fresh API + in-memory DB per test (isolation; the evaluated-watermark makes shared state order-dependent).
    private readonly SliceWebFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    private async Task SeedUserWithAlertAsync(string keyword)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlertCenterDbContext>();
        var user = new User(Guid.NewGuid(), "Ada", "ada@example.com", DateTimeOffset.UtcNow);
        db.Users.Add(user);
        db.Alerts.Add(new Alert(Guid.NewGuid(), user.Id, new[] { Keyword.Create(keyword) }, Channel.Email, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Health_is_ok()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/v1/ops/health");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<HealthDto>();
        Assert.Equal("ok", body!.Status);
    }

    [Fact]
    public async Task Poll_then_dispatch_produces_a_sent_notification() // AC-1, AC-2, AC-3
    {
        await SeedUserWithAlertAsync("openai");
        var client = _factory.CreateClient();

        // poll -> ingest + evaluate (the fake feed item matches "openai")
        var poll = await (await client.PostAsync("/api/v1/ops/poll", null)).Content.ReadFromJsonAsync<PollDto>();
        Assert.True(poll!.Added >= 1);
        Assert.True(poll.Enqueued >= 1);

        // dispatch -> mock send
        var dispatch = await (await client.PostAsync("/api/v1/ops/dispatch", null)).Content.ReadFromJsonAsync<DispatchDto>();
        Assert.True(dispatch!.Dispatched >= 1);
        Assert.Equal(0, dispatch.Failed);

        // history shows it sent
        var list = await client.GetFromJsonAsync<NotificationsPage>("/api/v1/notifications?status=sent");
        Assert.True(list!.Total >= 1);
        Assert.All(list.Items, i => Assert.Equal("sent", i.Status));
        Assert.Contains(list.Items, i => i.Article.Title == "OpenAI announces a merger");
    }

    [Fact]
    public async Task Re_polling_is_idempotent_no_duplicate_notifications() // FR-3 / FR-7 / R-6
    {
        await SeedUserWithAlertAsync("merger");
        var client = _factory.CreateClient();

        await client.PostAsync("/api/v1/ops/poll", null);
        var second = await (await client.PostAsync("/api/v1/ops/poll", null)).Content.ReadFromJsonAsync<PollDto>();

        Assert.Equal(0, second!.Added);     // article already seen
        Assert.Equal(0, second.Enqueued);   // already evaluated -> nothing new
    }

    private sealed record HealthDto(string Status, int OutboxPending);
    private sealed record PollDto(int Added, int Enqueued);
    private sealed record DispatchDto(int Dispatched, int Failed);
    private sealed record NotificationsPage(List<NotificationItem> Items, int Page, int PageSize, int Total);
    private sealed record NotificationItem(string Status, ArticleItem Article);
    private sealed record ArticleItem(string Title, string Link, string Source);
}

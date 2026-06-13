using System.Net;
using System.Net.Http.Json;

namespace AlertCenter.Api.Tests;

public class AdminApiTests : IDisposable
{
    private readonly SliceWebFactory _factory = new();
    private readonly HttpClient _client;

    public AdminApiTests() => _client = _factory.CreateClient();
    public void Dispose() => _factory.Dispose();

    private async Task<UserDto> CreateUserAsync(string name = "Ada", string email = "ada@x.io")
    {
        var res = await _client.PostAsJsonAsync("/api/v1/users", new { name, email });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<UserDto>())!;
    }

    [Fact]
    public async Task Creates_and_lists_a_user() // FR-11 / AC-4
    {
        var user = await CreateUserAsync();
        Assert.NotEqual(Guid.Empty, user.Id);

        var page = await _client.GetFromJsonAsync<Page<UserDto>>("/api/v1/users");
        Assert.Contains(page!.Items, u => u.Email == "ada@x.io");
    }

    [Fact]
    public async Task Duplicate_email_returns_409()
    {
        await CreateUserAsync(email: "dup@x.io");
        var res = await _client.PostAsJsonAsync("/api/v1/users", new { name = "Bob", email = "dup@x.io" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Create_alert_for_unknown_user_returns_404() // RF-003-H
    {
        var res = await _client.PostAsJsonAsync("/api/v1/alerts",
            new { userId = Guid.NewGuid(), keywords = new[] { "openai" }, channel = "email" });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Create_alert_for_disabled_user_returns_422() // RF-003-H
    {
        var user = await CreateUserAsync(email: "dis@x.io");
        await _client.PatchAsJsonAsync($"/api/v1/users/{user.Id}", new { enabled = false });

        var res = await _client.PostAsJsonAsync("/api/v1/alerts",
            new { userId = user.Id, keywords = new[] { "openai" }, channel = "email" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task Creates_alert_with_owner_name() // RF-004-A
    {
        var user = await CreateUserAsync(name: "Grace", email: "grace@x.io");
        var res = await _client.PostAsJsonAsync("/api/v1/alerts",
            new { userId = user.Id, keywords = new[] { "openai", "merger" }, channel = "slack" });

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var alert = (await res.Content.ReadFromJsonAsync<AlertDto>())!;
        Assert.Equal("Grace", alert.OwnerName);
        Assert.Equal("slack", alert.Channel);
        Assert.Equal(2, alert.Keywords.Count);
    }

    [Fact]
    public async Task Invalid_channel_returns_422()
    {
        var user = await CreateUserAsync(email: "ch@x.io");
        var res = await _client.PostAsJsonAsync("/api/v1/alerts",
            new { userId = user.Id, keywords = new[] { "x" }, channel = "sms" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    [Fact]
    public async Task Whitespace_keyword_returns_422() // RF-003-C
    {
        var user = await CreateUserAsync(email: "ws@x.io");
        var res = await _client.PostAsJsonAsync("/api/v1/alerts",
            new { userId = user.Id, keywords = new[] { "interest rate" }, channel = "email" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, res.StatusCode);
    }

    private sealed record UserDto(Guid Id, string Name, string Email, bool Enabled);
    private sealed record AlertDto(Guid Id, Guid UserId, string OwnerName, List<string> Keywords, string Channel, bool Enabled);
    private sealed record Page<T>(List<T> Items, int Total);
}

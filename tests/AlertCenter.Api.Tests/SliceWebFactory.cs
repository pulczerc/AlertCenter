using AlertCenter.Core.Ingestion;
using AlertCenter.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AlertCenter.Api.Tests;

/// <summary>
/// Boots the real API in-process with: a shared in-memory SQLite DB, the background
/// timers disabled, and the RSS feed replaced by a fake (no network). The test drives
/// the slice through the /ops endpoints.
/// </summary>
public sealed class SliceWebFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SliceWebFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Scheduling:Enabled", "false"); // timers off; the test triggers ops manually
        builder.UseEnvironment("Production");              // no Swagger noise

        builder.ConfigureServices(services =>
        {
            Replace(services, typeof(DbContextOptions<AlertCenterDbContext>));
            services.AddDbContext<AlertCenterDbContext>(o => o.UseSqlite(_connection));

            Replace(services, typeof(IFeedSource));
            services.AddSingleton<IFeedSource>(new FakeFeedSource());
        });
    }

    private static void Replace(IServiceCollection services, Type serviceType)
    {
        foreach (var d in services.Where(d => d.ServiceType == serviceType).ToList())
            services.Remove(d);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }

    private sealed class FakeFeedSource : IFeedSource
    {
        public Task<IReadOnlyList<FeedItem>> FetchAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FeedItem>>(new[]
            {
                new FeedItem("reuters", "slice-guid-1", "OpenAI announces a merger", "details", "https://x/1", null)
            });
    }
}

using AlertCenter.Core.Alerts;
using AlertCenter.Core.Application;
using AlertCenter.Core.Channels;
using AlertCenter.Core.Ingestion;
using AlertCenter.Core.Notifications;
using AlertCenter.Core.Shared;
using AlertCenter.Infrastructure.Channels;
using AlertCenter.Infrastructure.Feeds;
using AlertCenter.Infrastructure.Persistence;
using AlertCenter.Infrastructure.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AlertCenter.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Wires every adapter, use case, and background timer behind the Core ports.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("AlertCenter") ?? "Data Source=alertcenter.db";
        services.AddDbContext<AlertCenterDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AlertCenterDbContext>());

        // Repositories (driven adapters)
        services.AddScoped<IArticleRepository, ArticleRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddScoped<IAlertQuery, AlertQuery>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IOutboxPort, OutboxRepository>();

        services.AddSingleton<IClock, SystemClock>();

        // Feeds
        services.Configure<FeedOptions>(config.GetSection(FeedOptions.Section));
        services.AddHttpClient<IFeedSource, RssFeedSource>();

        // Channels (mock-first, Q-5)
        services.AddSingleton<INotificationChannel, MockEmailChannel>();
        services.AddSingleton<INotificationChannel, MockSlackChannel>();
        services.AddSingleton<IChannelResolver, ChannelResolver>();

        // Application use cases
        services.AddScoped<PollFeedsUseCase>();
        services.AddScoped<EvaluateAlertsUseCase>();
        services.AddScoped<DispatchOutboxUseCase>();

        // Background timers
        services.Configure<SchedulingOptions>(config.GetSection(SchedulingOptions.Section));
        services.AddHostedService<IngestionHostedService>();
        services.AddHostedService<DispatchHostedService>();

        return services;
    }
}

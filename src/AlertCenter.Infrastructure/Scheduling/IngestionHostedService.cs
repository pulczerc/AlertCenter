using AlertCenter.Core.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlertCenter.Infrastructure.Scheduling;

/// <summary>
/// One timer that runs <c>PollFeeds</c> then <c>EvaluateAlerts</c> each tick (RF-005-C):
/// ingest new articles, then drain the un-evaluated backlog into notifications.
/// </summary>
public sealed class IngestionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly SchedulingOptions _options;
    private readonly ILogger<IngestionHostedService> _log;

    public IngestionHostedService(IServiceScopeFactory scopes, IOptions<SchedulingOptions> options, ILogger<IngestionHostedService> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;

        using var timer = new PeriodicTimer(_options.PollInterval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Ingestion tick failed"); // tolerate and keep ticking (R-2)
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var poll = scope.ServiceProvider.GetRequiredService<PollFeedsUseCase>();
        var evaluate = scope.ServiceProvider.GetRequiredService<EvaluateAlertsUseCase>();

        var (_, added) = await poll.ExecuteAsync(ct);
        var enqueued = await evaluate.ExecuteAsync(ct: ct);
        if (added > 0 || enqueued > 0)
            _log.LogInformation("Ingestion tick: +{Added} articles, +{Enqueued} notifications", added, enqueued);
    }
}

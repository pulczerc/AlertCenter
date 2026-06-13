using AlertCenter.Core.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlertCenter.Infrastructure.Scheduling;

/// <summary>Second timer that drains the outbox via <c>DispatchOutbox</c> (FR-10, AC-3).</summary>
public sealed class DispatchHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly SchedulingOptions _options;
    private readonly ILogger<DispatchHostedService> _log;

    public DispatchHostedService(IServiceScopeFactory scopes, IOptions<SchedulingOptions> options, ILogger<DispatchHostedService> log)
    {
        _scopes = scopes;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;

        using var timer = new PeriodicTimer(_options.DispatchInterval);
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var dispatch = scope.ServiceProvider.GetRequiredService<DispatchOutboxUseCase>();
                var result = await dispatch.ExecuteAsync(ct: stoppingToken);
                if (result.Sent > 0 || result.Failed > 0)
                    _log.LogInformation("Dispatch tick: {Sent} sent, {Failed} failed", result.Sent, result.Failed);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Dispatch tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

using AlertCenter.Core.Channels;
using AlertCenter.Core.Shared;
using Microsoft.Extensions.Logging;

namespace AlertCenter.Infrastructure.Channels;

/// <summary>Default Email binding (Q-5): logs the send instead of contacting SMTP.
/// A real adapter is a drop-in replacement reading creds from config (NFR-4).</summary>
public sealed class MockEmailChannel : INotificationChannel
{
    private readonly ILogger<MockEmailChannel> _log;
    public MockEmailChannel(ILogger<MockEmailChannel> log) => _log = log;

    public Channel Channel => Channel.Email;

    public Task SendAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _log.LogInformation("[MOCK EMAIL] to={Recipient} subject=\"{Subject}\"", message.Recipient, message.Subject);
        return Task.CompletedTask;
    }
}

/// <summary>Default Slack binding (Q-5): logs the send instead of posting to a webhook.</summary>
public sealed class MockSlackChannel : INotificationChannel
{
    private readonly ILogger<MockSlackChannel> _log;
    public MockSlackChannel(ILogger<MockSlackChannel> log) => _log = log;

    public Channel Channel => Channel.Slack;

    public Task SendAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _log.LogInformation("[MOCK SLACK] to={Recipient} subject=\"{Subject}\"", message.Recipient, message.Subject);
        return Task.CompletedTask;
    }
}

using AlertCenter.Core.Channels;
using AlertCenter.Core.Shared;

namespace AlertCenter.Infrastructure.Channels;

/// <summary>Resolves the registered <see cref="INotificationChannel"/> by its <see cref="Channel"/>.</summary>
public sealed class ChannelResolver : IChannelResolver
{
    private readonly IReadOnlyDictionary<Channel, INotificationChannel> _channels;

    public ChannelResolver(IEnumerable<INotificationChannel> channels)
        => _channels = channels.ToDictionary(c => c.Channel);

    public INotificationChannel Resolve(Channel channel)
        => _channels.TryGetValue(channel, out var c)
            ? c
            : throw new InvalidOperationException($"No channel registered for '{channel}'.");
}

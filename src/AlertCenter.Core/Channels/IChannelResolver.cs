using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Channels;

/// <summary>Resolves the <see cref="INotificationChannel"/> for a given <see cref="Channel"/>.</summary>
public interface IChannelResolver
{
    INotificationChannel Resolve(Channel channel);
}

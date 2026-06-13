using AlertCenter.Core.Shared;

namespace AlertCenter.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

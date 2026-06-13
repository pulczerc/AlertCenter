namespace AlertCenter.Core.Shared;

/// <summary>
/// Abstracts "now" so domain/application code is testable and never calls
/// <c>DateTime.Now</c> directly (ADR-001 ClockPort).
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

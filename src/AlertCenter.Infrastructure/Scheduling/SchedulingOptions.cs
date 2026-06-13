namespace AlertCenter.Infrastructure.Scheduling;

public sealed class SchedulingOptions
{
    public const string Section = "Scheduling";

    /// <summary>Poll+evaluate cadence (NFR-5). Sensible default to avoid hammering sources.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan DispatchInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>When false, the background timers don't run (e.g. tests drive use cases directly).</summary>
    public bool Enabled { get; set; } = true;
}

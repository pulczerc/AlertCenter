namespace AlertCenter.Infrastructure.Feeds;

public sealed class FeedOptions
{
    public const string Section = "Feeds";
    public List<FeedConfig> Sources { get; set; } = new();
}

public sealed class FeedConfig
{
    public string Source { get; set; } = string.Empty;  // e.g. "reuters"
    public string Url { get; set; } = string.Empty;
}

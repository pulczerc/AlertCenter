using AlertCenter.Core.Alerts;
using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Tests.Alerts;

public class AlertTests
{
    private static Keyword[] K(params string[] words) => words.Select(Keyword.Create).ToArray();
    private static Alert NewAlert(IEnumerable<Keyword> keywords)
        => new(Guid.NewGuid(), Guid.NewGuid(), keywords, Channel.Email, DateTimeOffset.UtcNow);

    [Fact]
    public void Requires_at_least_one_keyword()
        => Assert.Throws<ArgumentException>(() => NewAlert(Array.Empty<Keyword>()));

    [Fact]
    public void Dedups_keywords_case_insensitively()
    {
        var alert = NewAlert(K("openai", "OpenAI", "merger"));
        Assert.Equal(2, alert.Keywords.Count);
    }

    [Fact]
    public void New_alert_is_enabled()
        => Assert.True(NewAlert(K("openai")).Enabled);

    [Fact]
    public void Disable_then_enable_toggles()
    {
        var alert = NewAlert(K("openai"));
        alert.Disable();
        Assert.False(alert.Enabled);
        alert.Enable();
        Assert.True(alert.Enabled);
    }
}

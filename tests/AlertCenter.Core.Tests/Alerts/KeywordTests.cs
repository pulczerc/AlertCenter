using AlertCenter.Core.Alerts;

namespace AlertCenter.Core.Tests.Alerts;

public class KeywordTests
{
    [Fact]
    public void Normalizes_to_lowercase() => Assert.Equal("openai", Keyword.Create("OpenAI").Normalized);

    [Fact]
    public void Trims_surrounding_whitespace() => Assert.Equal("openai", Keyword.Create("  OpenAI  ").Normalized);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_blank(string text) => Assert.Throws<ArgumentException>(() => Keyword.Create(text));

    [Fact]
    public void Rejects_internal_whitespace() // RF-003-C: single token only
        => Assert.Throws<ArgumentException>(() => Keyword.Create("interest rate"));

    [Fact]
    public void Rejects_too_long()
        => Assert.Throws<ArgumentException>(() => Keyword.Create(new string('a', Keyword.MaxLength + 1)));

    [Fact]
    public void Equality_is_case_insensitive()
        => Assert.Equal(Keyword.Create("OpenAI"), Keyword.Create("openai"));
}

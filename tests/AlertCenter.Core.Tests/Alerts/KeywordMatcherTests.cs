using AlertCenter.Core.Alerts;

namespace AlertCenter.Core.Tests.Alerts;

public class KeywordMatcherTests
{
    private static IEnumerable<Keyword> Kw(params string[] words) => words.Select(Keyword.Create);

    [Fact]
    public void Matches_whole_word_in_title()
        => Assert.True(KeywordMatcher.Matches("OpenAI announces merger", null, Kw("openai")));

    [Fact]
    public void Matches_whole_word_in_summary()
        => Assert.True(KeywordMatcher.Matches("Markets today", "a major merger was announced", Kw("merger")));

    [Fact]
    public void Or_semantics_matches_when_any_keyword_hits()
        => Assert.True(KeywordMatcher.Matches("Turing test revisited", null, Kw("nobel", "turing")));

    [Fact]
    public void Does_not_match_substring_only_whole_words()
        => Assert.False(KeywordMatcher.Matches("a catfish tale", null, Kw("cat")));

    [Fact]
    public void Returns_false_when_nothing_matches()
        => Assert.False(KeywordMatcher.Matches("nothing here", "still nothing", Kw("bitcoin")));

    [Theory]
    [InlineData("OpenAI")]
    [InlineData("openai")]
    [InlineData("OPENAI")]
    public void Is_case_insensitive(string keyword)
        => Assert.True(KeywordMatcher.Matches("the openai lab", null, Kw(keyword)));

    [Fact]
    public void Treats_punctuation_as_word_boundary()
        => Assert.True(KeywordMatcher.Matches("\"OpenAI,\" said the CEO.", null, Kw("openai")));

    [Fact]
    public void Empty_text_matches_nothing()
        => Assert.False(KeywordMatcher.Matches("", "", Kw("anything")));
}

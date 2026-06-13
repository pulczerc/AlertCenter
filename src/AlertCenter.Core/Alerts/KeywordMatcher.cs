using System.Text;

namespace AlertCenter.Core.Alerts;

/// <summary>
/// The pure matching rule (FR-5), side-effect-free and fully unit-testable (AD-7).
/// Semantics: <b>OR</b> across keywords (Q-1), <b>whole-word, case-insensitive</b>
/// (Q-7), over <b>title + summary</b> (Q-2). No regex/NLP (A-3).
/// </summary>
public static class KeywordMatcher
{
    /// <summary>True if any keyword matches a whole word in the article's title or summary.</summary>
    public static bool Matches(string title, string? summary, IEnumerable<Keyword> keywords)
    {
        var tokens = Tokenize(title, summary);
        foreach (var keyword in keywords)
        {
            if (tokens.Contains(keyword.Normalized))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Lower-cases and splits text into the set of whole-word tokens
    /// (maximal runs of letters/digits). Punctuation and case are ignored.
    /// </summary>
    public static IReadOnlySet<string> Tokenize(string? title, string? summary = null)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        AppendTokens(title, tokens);
        AppendTokens(summary, tokens);
        return tokens;
    }

    private static void AppendTokens(string? text, HashSet<string> tokens)
    {
        if (string.IsNullOrEmpty(text)) return;

        var current = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0)
            tokens.Add(current.ToString());
    }
}

namespace AlertCenter.Core.Alerts;

/// <summary>
/// A single match term (FR-4). MVP rule (D-007 #1 / RF-003-C): a keyword is a
/// <b>single token</b> — no internal whitespace, ≤60 chars. Equality is by the
/// lower-cased <see cref="Normalized"/> form (case-insensitive, Q-7).
/// </summary>
public sealed class Keyword : IEquatable<Keyword>
{
    public const int MaxLength = 60;

    public string Text { get; }
    public string Normalized { get; }

    private Keyword(string text, string normalized)
    {
        Text = text;
        Normalized = normalized;
    }

    public static Keyword Create(string text)
    {
        if (text is null) throw new ArgumentException("Keyword is required.", nameof(text));
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Keyword must not be blank.", nameof(text));
        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Keyword must be at most {MaxLength} characters.", nameof(text));
        if (trimmed.Any(char.IsWhiteSpace))
            throw new ArgumentException("Keyword must be a single token (no whitespace).", nameof(text)); // RF-003-C

        return new Keyword(trimmed, trimmed.ToLowerInvariant());
    }

    public bool Equals(Keyword? other) => other is not null && Normalized == other.Normalized;
    public override bool Equals(object? obj) => Equals(obj as Keyword);
    public override int GetHashCode() => Normalized.GetHashCode(StringComparison.Ordinal);
    public override string ToString() => Text;
}

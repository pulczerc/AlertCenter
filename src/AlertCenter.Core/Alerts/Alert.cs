using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Alerts;

/// <summary>
/// A user-defined keyword rule with a target channel (FR-4, Q-3/Q-4).
/// Invariants: ≥1 keyword (AL1), keywords de-duplicated case-insensitively (AL3).
/// </summary>
public sealed class Alert
{
    private readonly List<Keyword> _keywords = new();

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Channel Channel { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<Keyword> Keywords => _keywords;

    private Alert() { } // EF

    public Alert(Guid id, Guid ownerUserId, IEnumerable<Keyword> keywords, Channel channel, DateTimeOffset createdAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        Channel = channel;
        Enabled = true;
        CreatedAt = createdAt;
        SetKeywords(keywords);
    }

    public void SetKeywords(IEnumerable<Keyword> keywords)
    {
        var deduped = keywords.Distinct().ToList(); // Keyword equality is by Normalized (AL3)
        if (deduped.Count == 0)
            throw new ArgumentException("An alert requires at least one keyword.", nameof(keywords)); // AL1

        _keywords.Clear();
        _keywords.AddRange(deduped);
    }

    public void ChangeChannel(Channel channel) => Channel = channel;
    public void Disable() => Enabled = false;
    public void Enable() => Enabled = true;
}

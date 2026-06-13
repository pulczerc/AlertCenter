using AlertCenter.Core.Alerts;

namespace AlertCenter.Api.Contracts;

/// <summary>Alert read model. Carries <c>ownerName</c> (RF-004-A) so the list view needs no client-side join.</summary>
public sealed record AlertDto(
    Guid Id,
    Guid UserId,
    string OwnerName,
    IReadOnlyList<string> Keywords,
    string Channel,
    bool Enabled,
    DateTimeOffset CreatedAt)
{
    public static AlertDto From(Alert a, string ownerName) => new(
        a.Id, a.OwnerUserId, ownerName,
        a.Keywords.Select(k => k.Text).ToList(),
        a.Channel.ToString().ToLowerInvariant(),
        a.Enabled, a.CreatedAt);
}

public sealed record CreateAlertRequest(Guid UserId, List<string>? Keywords, string? Channel);

public sealed record UpdateAlertRequest(List<string>? Keywords, string? Channel, bool? Enabled);

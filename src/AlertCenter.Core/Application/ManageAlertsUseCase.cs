using AlertCenter.Core.Alerts;
using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Application;

/// <summary>Admin alert operations (FR-4/FR-12): create, update, enable/disable.</summary>
public sealed class ManageAlertsUseCase
{
    private readonly IAlertRepository _alerts;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public ManageAlertsUseCase(IAlertRepository alerts, IUserRepository users, IUnitOfWork uow, IClock clock)
    {
        _alerts = alerts;
        _users = users;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Alert> CreateAsync(Guid userId, IEnumerable<string> keywords, Channel channel, CancellationToken ct = default)
    {
        var owner = await _users.GetAsync(userId, ct)
            ?? throw new NotFoundException($"User {userId} not found.");          // RF-003-H -> 404
        if (!owner.Enabled)
            throw new ValidationException("Cannot create an alert for a disabled user."); // RF-003-H -> 422

        var alert = new Alert(Guid.NewGuid(), userId, ToKeywords(keywords), channel, _clock.UtcNow);
        await _alerts.AddAsync(alert, ct);
        await _uow.SaveChangesAsync(ct);
        return alert;
    }

    public async Task<Alert> UpdateAsync(Guid id, IEnumerable<string>? keywords, Channel? channel, bool? enabled, CancellationToken ct = default)
    {
        var alert = await _alerts.GetAsync(id, ct) ?? throw new NotFoundException($"Alert {id} not found.");

        if (keywords is not null)
            alert.SetKeywords(ToKeywords(keywords));
        if (channel is Channel c)
            alert.ChangeChannel(c);
        if (enabled is bool e)
        {
            if (e) alert.Enable(); else alert.Disable();
        }

        await _uow.SaveChangesAsync(ct);
        return alert;
    }

    // Keyword.Create enforces single-token/≤60 (RF-003-C); surface that as a 422 semantic error.
    private static List<Keyword> ToKeywords(IEnumerable<string> keywords)
    {
        try
        {
            return keywords.Select(Keyword.Create).ToList();
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException(ex.Message);
        }
    }
}

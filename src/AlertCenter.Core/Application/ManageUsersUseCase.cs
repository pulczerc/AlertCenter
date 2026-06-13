using AlertCenter.Core.Alerts;
using AlertCenter.Core.Shared;

namespace AlertCenter.Core.Application;

/// <summary>Admin user operations (FR-11): create, fetch, rename, enable/disable.</summary>
public sealed class ManageUsersUseCase
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public ManageUsersUseCase(IUserRepository users, IUnitOfWork uow, IClock clock)
    {
        _users = users;
        _uow = uow;
        _clock = clock;
    }

    public async Task<User> CreateAsync(string name, string email, CancellationToken ct = default)
    {
        var normalizedEmail = (email ?? string.Empty).Trim();
        if (await _users.EmailExistsAsync(normalizedEmail, ct))
            throw new ConflictException($"Email '{normalizedEmail}' is already in use."); // U1 -> 409

        var user = new User(Guid.NewGuid(), name, email!, _clock.UtcNow); // ArgumentException on invalid -> 400
        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        return user;
    }

    public async Task<User> UpdateAsync(Guid id, string? name, bool? enabled, CancellationToken ct = default)
    {
        var user = await _users.GetAsync(id, ct) ?? throw new NotFoundException($"User {id} not found.");
        if (enabled is bool e)
        {
            if (e) user.Enable(); else user.Disable();
        }
        if (!string.IsNullOrWhiteSpace(name))
            user.Rename(name);

        await _uow.SaveChangesAsync(ct);
        return user;
    }
}

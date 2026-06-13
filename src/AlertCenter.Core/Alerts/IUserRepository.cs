namespace AlertCenter.Core.Alerts;

/// <summary>Driven port for user persistence (FR-11).</summary>
public interface IUserRepository
{
    Task<User?> GetAsync(Guid id, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default); // U1
    Task AddAsync(User user, CancellationToken ct = default);
}

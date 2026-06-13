using AlertCenter.Core.Alerts;

namespace AlertCenter.Api.Contracts;

public sealed record UserDto(Guid Id, string Name, string Email, bool Enabled, DateTimeOffset CreatedAt)
{
    public static UserDto From(User u) => new(u.Id, u.Name, u.Email, u.Enabled, u.CreatedAt);
}

public sealed record CreateUserRequest(string? Name, string? Email);

public sealed record UpdateUserRequest(string? Name, bool? Enabled);

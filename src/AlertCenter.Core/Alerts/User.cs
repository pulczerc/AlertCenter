namespace AlertCenter.Core.Alerts;

/// <summary>Owner of alerts and the Email-channel recipient (FR-11, Q-4).
/// Disabled rather than deleted (D-007 #6); a disabled user's alerts do not match.</summary>
public sealed class User
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public bool Enabled { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { } // EF

    public User(Guid id, string name, string email, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (!IsValidEmail(email))
            throw new ArgumentException("A valid email is required.", nameof(email)); // U1

        Id = id;
        Name = name.Trim();
        Email = email.Trim();
        Enabled = true;
        CreatedAt = createdAt;
    }

    public void Disable() => Enabled = false;
    public void Enable() => Enabled = true;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim();
    }

    // Deliberately permissive (MVP): a single '@' with non-empty local/domain parts.
    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var at = email.Trim().IndexOf('@');
        return at > 0 && at < email.Trim().Length - 1;
    }
}

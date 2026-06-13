namespace AlertCenter.Core.Shared;

/// <summary>A referenced resource (or parent) does not exist → HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>A uniqueness/state conflict (e.g. duplicate email) → HTTP 409.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>A semantically invalid request (e.g. alert for a disabled user) → HTTP 422.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

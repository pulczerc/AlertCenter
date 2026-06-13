using AlertCenter.Core.Shared;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AlertCenter.Api;

/// <summary>Maps application/domain exceptions to RFC-7807 problem responses (05 §8).
/// 5xx never leaks the exception detail (NFR-4).</summary>
public sealed class ProblemExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ProblemExceptionHandler> _log;
    public ProblemExceptionHandler(ILogger<ProblemExceptionHandler> log) => _log = log;

    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            ValidationException => (StatusCodes.Status422UnprocessableEntity, "Unprocessable entity"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (StatusCodes.Status500InternalServerError, "Server error")
        };

        if (status >= 500)
            _log.LogError(ex, "Unhandled exception");

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= 500 ? null : ex.Message  // don't leak internals
        }, ct);
        return true;
    }
}

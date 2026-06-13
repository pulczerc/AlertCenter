using AlertCenter.Api.Contracts;
using AlertCenter.Core.Application;
using AlertCenter.Core.Notifications;
using AlertCenter.Infrastructure;
using AlertCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddPolicy("spa", p => p
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Slice: create the schema on startup (EnsureCreated; migrations are stretch).
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AlertCenterDbContext>().Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("spa");

var api = app.MapGroup("/api/v1");

// ── Ops (NFR-3, demo triggers) ───────────────────────────────────────────────
// poll runs poll+evaluate so matches appear without waiting for the timer.
api.MapPost("/ops/poll", async (PollFeedsUseCase poll, EvaluateAlertsUseCase evaluate, CancellationToken ct) =>
{
    var (_, added) = await poll.ExecuteAsync(ct);
    var enqueued = await evaluate.ExecuteAsync(ct: ct);
    return Results.Ok(new { added, enqueued });
});

api.MapPost("/ops/dispatch", async (DispatchOutboxUseCase dispatch, CancellationToken ct) =>
{
    var result = await dispatch.ExecuteAsync(ct: ct);
    return Results.Ok(new { dispatched = result.Sent, failed = result.Failed });
});

api.MapGet("/ops/health", async (AlertCenterDbContext db, CancellationToken ct) =>
{
    var pending = await db.Outbox.CountAsync(e => e.Status == OutboxStatus.Pending, ct);
    return Results.Ok(new { status = "ok", outboxPending = pending });
});

// ── Notifications (FR-13) — read-only ────────────────────────────────────────
api.MapGet("/notifications", async (AlertCenterDbContext db, string? status, CancellationToken ct, int page = 1, int pageSize = 25) =>
{
    page = page <= 0 ? 1 : page;
    pageSize = pageSize is <= 0 or > 100 ? 25 : pageSize;

    var query = from n in db.Notifications
                join a in db.Articles on n.ArticleId equals a.Id
                select new { n, a };
    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<NotificationStatus>(status, ignoreCase: true, out var st))
        query = query.Where(x => x.n.Status == st);

    // order + page in memory (SQLite can't ORDER BY DateTimeOffset — RF-005-H)
    var rows = (await query.ToListAsync(ct)).OrderByDescending(x => x.n.CreatedAt).ToList();
    var items = rows.Skip((page - 1) * pageSize).Take(pageSize)
        .Select(x => NotificationDto.From(x.n, x.a)).ToList();

    return Results.Ok(new { items, page, pageSize, total = rows.Count });
});

api.MapGet("/notifications/{id:guid}", async (Guid id, AlertCenterDbContext db, CancellationToken ct) =>
{
    var row = await (from n in db.Notifications
                     join a in db.Articles on n.ArticleId equals a.Id
                     where n.Id == id
                     select new { n, a }).FirstOrDefaultAsync(ct);
    return row is null ? Results.NotFound() : Results.Ok(NotificationDto.From(row.n, row.a));
});

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }

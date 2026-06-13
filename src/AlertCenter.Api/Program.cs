using AlertCenter.Api;
using AlertCenter.Api.Contracts;
using AlertCenter.Core.Alerts;
using AlertCenter.Core.Application;
using AlertCenter.Core.Notifications;
using AlertCenter.Core.Shared;
using AlertCenter.Infrastructure;
using AlertCenter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
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

app.UseExceptionHandler();

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

// ── Users (FR-11) ────────────────────────────────────────────────────────────
api.MapGet("/users", async (AlertCenterDbContext db, CancellationToken ct, bool? enabled = null, int page = 1, int pageSize = 25) =>
{
    page = page <= 0 ? 1 : page;
    pageSize = pageSize is <= 0 or > 100 ? 25 : pageSize;
    var query = db.Users.AsQueryable();
    if (enabled is bool e) query = query.Where(u => u.Enabled == e);

    var rows = (await query.ToListAsync(ct)).OrderByDescending(u => u.CreatedAt).ToList();
    var items = rows.Skip((page - 1) * pageSize).Take(pageSize).Select(UserDto.From).ToList();
    return Results.Ok(new { items, page, pageSize, total = rows.Count });
});

api.MapPost("/users", async (CreateUserRequest req, ManageUsersUseCase users, CancellationToken ct) =>
{
    var user = await users.CreateAsync(req.Name ?? string.Empty, req.Email ?? string.Empty, ct);
    return Results.Created($"/api/v1/users/{user.Id}", UserDto.From(user));
});

api.MapGet("/users/{id:guid}", async (Guid id, AlertCenterDbContext db, CancellationToken ct) =>
{
    var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
    return u is null ? Results.NotFound() : Results.Ok(UserDto.From(u));
});

api.MapPatch("/users/{id:guid}", async (Guid id, UpdateUserRequest req, ManageUsersUseCase users, CancellationToken ct) =>
{
    var user = await users.UpdateAsync(id, req.Name, req.Enabled, ct);
    return Results.Ok(UserDto.From(user));
});

// ── Alerts (FR-4 / FR-12) ────────────────────────────────────────────────────
api.MapGet("/alerts", async (AlertCenterDbContext db, CancellationToken ct, Guid? userId = null, string? channel = null, bool? enabled = null, int page = 1, int pageSize = 25) =>
{
    page = page <= 0 ? 1 : page;
    pageSize = pageSize is <= 0 or > 100 ? 25 : pageSize;
    var query = from a in db.Alerts join u in db.Users on a.OwnerUserId equals u.Id select new { a, u };
    if (userId is Guid uid) query = query.Where(x => x.a.OwnerUserId == uid);
    if (enabled is bool e) query = query.Where(x => x.a.Enabled == e);
    if (!string.IsNullOrWhiteSpace(channel) && Enum.TryParse<Channel>(channel, true, out var ch))
        query = query.Where(x => x.a.Channel == ch);

    var rows = (await query.ToListAsync(ct)).OrderByDescending(x => x.a.CreatedAt).ToList();
    var items = rows.Skip((page - 1) * pageSize).Take(pageSize).Select(x => AlertDto.From(x.a, x.u.Name)).ToList();
    return Results.Ok(new { items, page, pageSize, total = rows.Count });
});

api.MapPost("/alerts", async (CreateAlertRequest req, ManageAlertsUseCase alerts, IUserRepository users, CancellationToken ct) =>
{
    var alert = await alerts.CreateAsync(req.UserId, req.Keywords ?? new(), ParseChannel(req.Channel), ct);
    var owner = await users.GetAsync(alert.OwnerUserId, ct);
    return Results.Created($"/api/v1/alerts/{alert.Id}", AlertDto.From(alert, owner?.Name ?? string.Empty));
});

api.MapGet("/alerts/{id:guid}", async (Guid id, AlertCenterDbContext db, CancellationToken ct) =>
{
    var row = await (from a in db.Alerts join u in db.Users on a.OwnerUserId equals u.Id
                     where a.Id == id select new { a, u }).FirstOrDefaultAsync(ct);
    return row is null ? Results.NotFound() : Results.Ok(AlertDto.From(row.a, row.u.Name));
});

api.MapPatch("/alerts/{id:guid}", async (Guid id, UpdateAlertRequest req, ManageAlertsUseCase alerts, AlertCenterDbContext db, CancellationToken ct) =>
{
    Channel? channel = req.Channel is null ? null : ParseChannel(req.Channel);
    var alert = await alerts.UpdateAsync(id, req.Keywords, channel, req.Enabled, ct);
    var owner = await db.Users.FirstOrDefaultAsync(u => u.Id == alert.OwnerUserId, ct);
    return Results.Ok(AlertDto.From(alert, owner?.Name ?? string.Empty));
});

app.Run();

static Channel ParseChannel(string? value)
    => Enum.TryParse<Channel>(value, ignoreCase: true, out var c) && Enum.IsDefined(c)
        ? c
        : throw new ValidationException($"Invalid channel '{value}'. Expected 'email' or 'slack'.");

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }

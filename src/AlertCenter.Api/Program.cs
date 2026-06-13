var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Wave 0 — minimal liveness (full Ops endpoints arrive in Wave 2).
// outboxPending is a placeholder until the outbox is wired.
app.MapGet("/api/v1/ops/health", () => Results.Ok(new { status = "ok", outboxPending = 0 }));

app.Run();

// Exposed for WebApplicationFactory-based integration tests (Stream 3).
public partial class Program { }

using AlertCenter.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AlertCenter.Infrastructure.Tests;

/// <summary>
/// A real SQLite database held in memory for the lifetime of the test (the open
/// connection keeps it alive). Fast, real SQL, no Docker — the slice's infra test bed.
/// </summary>
public sealed class SqliteFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AlertCenterDbContext> _options;

    public SqliteFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AlertCenterDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    /// <summary>A fresh context over the same database (independent change tracker).</summary>
    public AlertCenterDbContext NewContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}

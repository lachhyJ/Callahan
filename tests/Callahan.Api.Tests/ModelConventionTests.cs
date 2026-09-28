using Callahan.Api.Data;
using Callahan.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// Two model-wide rules in AppDbContext.OnModelCreating exist because their
// failure is silent (decisions: "Decimals are stored as REAL across the whole
// model", "Every timestamp is re-tagged UTC on the way out of SQLite"). These
// guard the rules themselves, including for columns added later.
public class ModelConventionTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly AppDbContext _db;

    public ModelConventionTests()
    {
        _conn.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }

    // Checked on the model, not with a LINQ comparison: EF's SQLite provider
    // compares decimals correctly in LINQ even on TEXT columns (tried - such a
    // test passes with the rule removed). The trap is in raw SQL, sqlite3
    // inspection and any query that bypasses the provider.
    [Fact]
    public void EveryDecimalIsStoredAsReal()
    {
        var textDecimals = _db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(decimal))
            .Where(p => p.GetProviderClrType() != typeof(double))
            .Select(p => $"{p.DeclaringType.DisplayName()}.{p.Name}")
            .ToList();
        Assert.Empty(textDecimals);
    }

    [Fact]
    public void EveryDateTimeHasAValueConverter()
    {
        var unconverted = _db.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(DateTime))
            .Where(p => p.GetValueConverter() is null)
            .Select(p => $"{p.DeclaringType.DisplayName()}.{p.Name}")
            .ToList();
        Assert.Empty(unconverted);
    }

    // DateTime equality ignores Kind, so this checks Kind explicitly.
    [Fact]
    public async Task ATimestampReadsBackAsUtc()
    {
        var started = new DateTime(2026, 9, 1, 1, 35, 0, DateTimeKind.Utc);
        _db.WorkoutSessions.Add(new WorkoutSession { Date = new DateOnly(2026, 9, 1), StartedAt = started });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var read = await _db.WorkoutSessions.SingleAsync();
        Assert.Equal(DateTimeKind.Utc, read.StartedAt!.Value.Kind);
        Assert.Equal(started, read.StartedAt);
    }
}

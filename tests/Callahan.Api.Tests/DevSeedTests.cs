using Callahan.Api;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

public class DevSeedTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // Finishers are HasData catalog content: nothing re-inserts them once the
    // migration has applied, so a seed that deletes them empties the endpoint
    // for good.
    [Fact]
    public async Task Seed_leaves_the_finisher_catalog_alone()
    {
        _connection.Open();
        using var db = TestData.OpenDb(_connection);
        var before = await db.Finishers.CountAsync();
        Assert.True(before > 0);

        await DevSeed.RunAsync(db, TimeProvider.System);

        Assert.Equal(before, await db.Finishers.CountAsync());
    }
}

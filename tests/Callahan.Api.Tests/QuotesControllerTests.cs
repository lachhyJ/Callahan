using Callahan.Api.Controllers;
using Callahan.Api.DTOs;
using Callahan.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Callahan.Api.Tests;

// The app-side list behind the lock-screen quotes widget, and the widget's own
// read route. The widget route's key check is its whole security (it opts out of
// the fallback policy), so every failure there has to be a 404.
public class QuotesControllerTests
{
    private const string Key = "test-widget-key-0123456789";

    private static async Task<AddQuotesResultDto> Add(Data.AppDbContext db, params string?[] texts)
    {
        var result = await new QuotesController(db).Add(new AddQuotesRequest(texts.ToList()!));
        return Assert.IsType<AddQuotesResultDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private static async Task<List<QuoteDto>> List(Data.AppDbContext db)
    {
        var result = await new QuotesController(db).GetAll();
        return Assert.IsType<List<QuoteDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
    }

    private static WidgetController Widget(Data.AppDbContext db, string? configuredKey, string? suppliedKey)
    {
        var http = new DefaultHttpContext();
        if (suppliedKey is not null) http.Request.Headers[WidgetController.KeyHeader] = suppliedKey;
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configuredKey is null
                ? []
                : new Dictionary<string, string?> { ["Widget:Key"] = configuredKey })
            .Build();
        return new WidgetController(db, config)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    [Fact]
    public void BothControllersKeepTheirAuthPosture()
    {
        Assert.NotEmpty(typeof(QuotesController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.NotEmpty(typeof(WidgetController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }

    [Fact]
    public async Task ManyLinesBecomeManyQuotes_TrimmedAndNewestFirst()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);

        var result = await Add(db, "  Bring Him with you ", "Be the one");

        Assert.Equal(2, result.Quotes.Count);
        Assert.Empty(result.Skipped);
        var list = await List(db);
        Assert.Equal(["Be the one", "Bring Him with you"], list.Select(q => q.Text));
    }

    [Fact]
    public async Task BlankTooLongAndDuplicateLinesAreSkippedWithAReason()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        await Add(db, "Keep going");

        var tooLong = new string('x', QuotesController.MaxLength + 1);
        var result = await Add(db, "", "   ", tooLong, "keep going", "Fresh one", "FRESH ONE");

        Assert.Equal(["Fresh one"], result.Quotes.Select(q => q.Text));
        Assert.Equal(
            ["blank", "blank", "too long", "duplicate", "duplicate"],
            result.Skipped.Select(s => s.Reason));
        Assert.Equal(2, (await List(db)).Count);
    }

    [Fact]
    public async Task AQuoteExactlyAtTheCapIsKept()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);

        var result = await Add(db, new string('x', QuotesController.MaxLength));

        Assert.Single(result.Quotes);
    }

    [Fact]
    public async Task AnEmptyRequestIs400()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);

        var result = await new QuotesController(db).Add(new AddQuotesRequest([]));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteHidesAQuoteAndRestoreBringsItBack()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var id = (await Add(db, "Keep going")).Quotes.Single().Id;

        Assert.IsType<NoContentResult>(await new QuotesController(db).Delete(id));
        Assert.Empty(await List(db));

        var restored = await new QuotesController(db).Restore(id);
        Assert.IsType<OkObjectResult>(restored.Result);
        Assert.Single(await List(db));
    }

    [Fact]
    public async Task DeletingOrRestoringTheWrongThingIs404()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var id = (await Add(db, "Keep going")).Quotes.Single().Id;

        Assert.IsType<NotFoundResult>(await new QuotesController(db).Delete(999));
        // Restoring a quote that was never deleted is not a thing to do.
        Assert.IsType<NotFoundResult>((await new QuotesController(db).Restore(id)).Result);
    }

    [Fact]
    public async Task AddingTextThatIsWaitingToBePurgedRestoresItInsteadOfDuplicating()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var id = (await Add(db, "Keep going")).Quotes.Single().Id;
        await new QuotesController(db).Delete(id);

        var result = await Add(db, "keep GOING");

        Assert.Equal(id, result.Quotes.Single().Id);
        Assert.Empty(result.Skipped);
        Assert.Single(await db.Quotes.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task QuotesDeletedLongerThanTheRecoveryWindowArePurgedOnRead()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var old = (await Add(db, "Old")).Quotes.Single().Id;
        var recent = (await Add(db, "Recent")).Quotes.Single().Id;
        await new QuotesController(db).Delete(old);
        await new QuotesController(db).Delete(recent);
        (await db.Quotes.IgnoreQueryFilters().SingleAsync(q => q.Id == old)).DeletedAt =
            DateTime.UtcNow.AddDays(-8);
        await db.SaveChangesAsync();

        await List(db);

        var left = await db.Quotes.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(recent, Assert.Single(left).Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-key")]
    [InlineData("test-widget-key-012345678")] // a prefix of the real key
    public async Task TheWidgetRouteIs404ForAMissingOrWrongKey(string? supplied)
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        await Add(db, "Keep going");

        var result = await Widget(db, Key, supplied).GetQuotes();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task TheWidgetRouteIs404WhenNoKeyIsConfigured()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        await Add(db, "Keep going");

        var result = await Widget(db, configuredKey: null, suppliedKey: Key).GetQuotes();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task TheWidgetGetsLiveQuotesOnly_IdAndTextOnly()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);
        var ids = (await Add(db, "One", "Two", "Three")).Quotes.Select(q => q.Id).ToList();
        await new QuotesController(db).Delete(ids[1]);

        var result = await Widget(db, Key, Key).GetQuotes();

        var dtos = Assert.IsType<List<WidgetQuoteDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(["One", "Three"], dtos.Select(q => q.Text));
        Assert.Equal(new[] { ids[0], ids[2] }, dtos.Select(q => q.Id));
    }

    // An empty list is a normal state, not "nothing to show".
    [Fact]
    public async Task AnEmptyListIsA200WithAnEmptyArray()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        using var db = TestData.OpenDb(conn);

        var result = await Widget(db, Key, Key).GetQuotes();

        var dtos = Assert.IsType<List<WidgetQuoteDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Empty(dtos);
    }
}

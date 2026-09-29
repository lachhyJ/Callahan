using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Callahan.Api.Tests;

// The deny-by-default authorisation and the /api 404 fallback only exist as
// pipeline behaviour, so they're tested through the real app rather than a
// controller. The fallback matters because the frontend treats any 401 as an
// expired session and logs out: without it an unknown /api route (a stale
// bundle calling a removed endpoint) returned 401 - that regression shipped
// once and was only caught by a post-deploy probe.
public class AuthPipelineTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"callahan-pipeline-{Guid.NewGuid():N}.db");
    private readonly List<IDisposable> _factories = [];

    private HttpClient Client(string environment, bool allowDevLogin = false) => Factory(environment, allowDevLogin).CreateClient();

    private WebApplicationFactory<Program> Factory(string environment, bool allowDevLogin = false)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseEnvironment(environment)
            .UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}")
            .UseSetting("Auth:JwtSecret", "pipeline-test-secret-that-is-long-enough-for-hs256")
            // Set here, not inherited: a dev machine supplies it from local
            // config, CI has none, and dev-login 500s without it.
            .UseSetting("Auth:Username", "pipeline-test-user")
            .UseSetting("Auth:AllowDevLogin", allowDevLogin ? "true" : "false"));
        _factories.Add(factory);
        return factory;
    }

    public void Dispose()
    {
        foreach (var f in _factories) f.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AnUnknownApiRouteIs404_NotTheLogout401()
    {
        var response = await Client("Production").GetAsync("/api/no-such-route");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ADataEndpointWithoutATokenIs401()
    {
        var response = await Client("Production").GetAsync("/api/streaks");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Every controller today also carries its own [Authorize], so the 401 test
    // above passes with or without the fallback policy. The policy exists for
    // the next controller someone forgets to annotate, so check it directly.
    [Fact]
    public void EndpointsWithoutAnAttributeRequireAnAuthenticatedUser()
    {
        var factory = Factory("Production");
        factory.CreateClient();
        var policy = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy;
        Assert.NotNull(policy);
        Assert.Contains(policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task HealthIsAnonymous()
    {
        var response = await Client("Production").GetAsync("/api/health");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Registered only when BOTH conditions hold, so a single stray setting can't
    // open an unauthenticated login in production.
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public async Task DevLoginDoesNotExistUnlessBothConditionsHold(string environment, bool allowDevLogin)
    {
        var response = await Client(environment, allowDevLogin).PostAsync("/api/auth/dev-login", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DevLoginWorksInDevelopmentWithTheFlag()
    {
        var response = await Client("Development", allowDevLogin: true).PostAsync("/api/auth/dev-login", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

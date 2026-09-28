using System.Net;
using System.Security.Cryptography;
using Callahan.Api.Data;
using Callahan.Api.Models;
using Callahan.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using VapidHelper = WebPush.VapidHelper;

namespace Callahan.Api.Tests;

// A subscription the push service reports as gone (404/410) is deleted, so it
// isn't retried on every later push; one that still works is kept.
public class PushNotificationServiceTests
{
    private sealed class FakePushService : HttpMessageHandler
    {
        public List<string> Hosts { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Hosts.Add(request.RequestUri!.Host);
            var status = request.RequestUri.Host == "gone.example" ? HttpStatusCode.Gone : HttpStatusCode.Created;
            return Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request });
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // A real P-256 public key and auth secret, so the payload encryption runs
    // exactly as it does against a browser's subscription.
    private static PushSubscription Subscription(string host)
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p = ecdh.ExportParameters(false).Q;
        return new PushSubscription
        {
            Endpoint = $"https://{host}/push/abc",
            P256dh = Base64Url([0x04, .. p.X!, .. p.Y!]),
            Auth = Base64Url(RandomNumberGenerator.GetBytes(16)),
        };
    }

    [Fact]
    public async Task ExpiredSubscriptionsAreRemoved_WorkingOnesKept()
    {
        using var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        db.PushSubscriptions.AddRange(Subscription("gone.example"), Subscription("ok.example"));
        await db.SaveChangesAsync();

        var vapid = VapidHelper.GenerateVapidKeys();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Vapid:PublicKey"] = vapid.PublicKey,
            ["Vapid:PrivateKey"] = vapid.PrivateKey,
            ["Vapid:Subject"] = "mailto:test@example.com",
        }).Build();
        var handler = new FakePushService();
        var service = new PushNotificationService(config, NullLogger<PushNotificationService>.Instance, new Factory(handler), db);

        await service.SendToAllAsync(await db.PushSubscriptions.ToListAsync(), "Rest over", "Next set.");

        Assert.Equal(["gone.example", "ok.example"], handler.Hosts.Order());
        Assert.Equal("https://ok.example/push/abc", Assert.Single(db.PushSubscriptions).Endpoint);
    }
}

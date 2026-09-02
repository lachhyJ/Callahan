using System.Text;
using System.Threading.RateLimiting;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<PushNotificationService>();
builder.Services.AddScoped<WeeklyConsistencyService>();
builder.Services.AddScoped<MonthlyReportBuilder>();
builder.Services.AddHttpClient<TaperConsultService>(client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});
// Proxies the "Sync Garmin" button to the garmin-sync-trigger container. A
// normal daily pull is seconds; the generous timeout only matters on a first
// run with no cached Garmin/Callahan tokens.
builder.Services.AddHttpClient<GarminSyncClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddHostedService<TaperReminderService>();

var jwtSecret = builder.Configuration["Auth:JwtSecret"]
    ?? throw new InvalidOperationException("Auth:JwtSecret is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// Deny by default. Every data controller already carries [Authorize], but that
// made protection a convention: a new controller added without the attribute
// would have been silently public. The fallback policy inverts that, so opting
// *out* now takes an explicit [AllowAnonymous] (Auth and Health, both of which
// have to be reachable before a token exists).
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseHttpsRedirection();

app.UseCors("Default");

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Dev-only login bypass for tooling (e.g. Claude Code browser checks) that needs to
// authenticate without knowing the real password. Two independent gates, both
// required: the route is only registered at all when IsDevelopment() (false for the
// NAS prod deploy, which never sets ASPNETCORE_ENVIRONMENT), and even then only when
// Auth:AllowDevLogin is explicitly true (set in docker-compose.yml's local-dev
// service, absent from backend.prod.env). A single misconfigured env var can't
// expose this — it takes both, in two different files, neither of which the NAS
// deploy touches.
if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Auth:AllowDevLogin"))
{
    app.MapPost("/api/auth/dev-login", (IConfiguration config, TokenService tokenService) =>
    {
        var username = config["Auth:Username"];
        if (username is null)
        {
            return Results.StatusCode(500);
        }

        return Results.Ok(new LoginResponse(tokenService.GenerateToken(username)));
    }).AllowAnonymous();
}

app.Run();

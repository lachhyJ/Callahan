using System.Text;
using Callahan.Api.Data;
using Callahan.Api.DTOs;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

builder.Services.AddAuthorization();

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
    });
}

app.Run();

using System.Security.Cryptography;
using System.Text;
using Callahan.Api.DTOs;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Callahan.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly TokenService _tokenService;

    public AuthController(IConfiguration config, TokenService tokenService)
    {
        _config = config;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public IActionResult Login(LoginRequest request)
    {
        var expectedUsername = _config["Auth:Username"];
        var expectedHash = _config["Auth:PasswordHash"];

        if (expectedUsername is null || expectedHash is null)
        {
            return StatusCode(500, new { error = "Auth is not configured on the server." });
        }

        // Both checks always run: short-circuiting on the username returned
        // instantly for a wrong one and only paid the bcrypt cost for a right
        // one, which tells an unauthenticated caller when it has guessed the
        // username. Single-user app, so the payoff is small — but so is the fix.
        var usernameMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(request.Username ?? string.Empty),
            Encoding.UTF8.GetBytes(expectedUsername));
        var passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password ?? string.Empty, expectedHash);

        if (!usernameMatches || !passwordMatches)
        {
            return Unauthorized(new { error = "Invalid username or password." });
        }

        // Mint from the configured username, not the request's copy: past the
        // check they are equal, and this one is non-null by construction.
        var token = _tokenService.GenerateToken(expectedUsername);
        return Ok(new LoginResponse(token));
    }
}

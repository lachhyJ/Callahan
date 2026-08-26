using Callahan.Api.DTOs;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Callahan.Api.Controllers;

[ApiController]
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

        if (request.Username != expectedUsername || !BCrypt.Net.BCrypt.Verify(request.Password, expectedHash))
        {
            return Unauthorized(new { error = "Invalid username or password." });
        }

        var token = _tokenService.GenerateToken(request.Username);
        return Ok(new LoginResponse(token));
    }
}

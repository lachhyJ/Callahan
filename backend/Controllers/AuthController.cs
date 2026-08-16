using Callahan.Api.DTOs;
using Callahan.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Callahan.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly TokenService _tokenService;
    private readonly IWebHostEnvironment _env;

    public AuthController(IConfiguration config, TokenService tokenService, IWebHostEnvironment env)
    {
        _config = config;
        _tokenService = tokenService;
        _env = env;
    }

    [HttpPost("login")]
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

    // Dev-only bypass so tooling (e.g. Claude Code browser checks) can authenticate
    // without knowing the real password. 404s outside Development so it doesn't
    // exist as far as the NAS prod deploy is concerned.
    [HttpPost("dev-login")]
    public IActionResult DevLogin()
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        var expectedUsername = _config["Auth:Username"];
        if (expectedUsername is null)
        {
            return StatusCode(500, new { error = "Auth is not configured on the server." });
        }

        var token = _tokenService.GenerateToken(expectedUsername);
        return Ok(new LoginResponse(token));
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelAggregator.Api.DTOs;
using ModelAggregator.Api.Services;

namespace ModelAggregator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AuthService authService) : ControllerBase
{
    /// <summary>
    /// Authenticate with a Google ID token. Returns a JWT + user profile.
    /// Creates the user on first login.
    /// </summary>
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            return BadRequest("ID token is required");

        var payload = await authService.ValidateGoogleTokenAsync(request.IdToken);
        if (payload == null)
            return Unauthorized("Invalid Google token");

        var response = await authService.LoginOrRegisterAsync(payload);
        return Ok(response);
    }

    /// <summary>
    /// Registers a new user with an email and password.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Email and password are required.");
            
        try
        {
            var response = await authService.RegisterAsync(request.Email, request.Password, request.DisplayName ?? request.Email);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Logs in a user with an email and password.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Email and password are required.");

        try
        {
            var response = await authService.LoginAsync(request.Email, request.Password);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    /// <summary>
    /// Get the currently authenticated user's profile.
    /// Used by the frontend to restore a session from a stored JWT.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var profile = await authService.GetUserProfileAsync(userId);
        if (profile == null)
            return NotFound("User not found");

        return Ok(profile);
    }
}

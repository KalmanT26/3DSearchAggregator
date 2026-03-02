using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ModelAggregator.Api.Data;
using ModelAggregator.Api.Data.Entities;
using ModelAggregator.Api.DTOs;

namespace ModelAggregator.Api.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;
    private readonly Microsoft.AspNetCore.Identity.IPasswordHasher<User> _passwordHasher;

    public AuthService(
        AppDbContext db,
        IConfiguration config,
        ILogger<AuthService> logger,
        Microsoft.AspNetCore.Identity.IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// Validates a Google ID token and returns the payload if valid.
    /// </summary>
    public async Task<GoogleJsonWebSignature.Payload?> ValidateGoogleTokenAsync(string idToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Google:ClientId"]! }
            };
            return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning("Invalid Google token: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Registers a new user with an email and password.
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(string email, string password, string displayName)
    {
        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower());
        if (existingUser != null)
        {
            // SECURE: We cannot merge during registration without email verification.
            // If the account exists (even if created via Google), registration must fail.
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLower(),
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreateResponse(user);
    }

    /// <summary>
    /// Logs in a user with an email and password.
    /// </summary>
    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower());

        if (user == null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (string.IsNullOrEmpty(user.PasswordHash))
            throw new UnauthorizedAccessException("This account was created with Google. Please sign in with Google.");

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (result == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Invalid email or password.");

        return CreateResponse(user);
    }

    /// <summary>
    /// Finds or creates a user based on the Google payload, handling secure identity merging.
    /// </summary>
    public async Task<AuthResponse> LoginOrRegisterAsync(GoogleJsonWebSignature.Payload payload)
    {
        var email = payload.Email.ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                GoogleId = payload.Subject,
                Email = email,
                DisplayName = payload.Name ?? payload.Email,
                AvatarUrl = payload.Picture,
                CreatedAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            _logger.LogInformation("New user registered via Google: {Email}", user.Email);
        }
        else
        {
            // SECURE IDENTITY MERGE: Google validated the email.
            // If the user previously registered with a password, we can safely link their Google ID now.
            if (user.GoogleId == null)
            {
                user.GoogleId = payload.Subject;
                _logger.LogInformation("Account merged: Google ID linked to existing email {Email}", user.Email);
            }
            
            // Update profile info from Google on each login
            user.DisplayName = payload.Name ?? user.DisplayName;
            user.AvatarUrl = payload.Picture ?? user.AvatarUrl;
        }

        await _db.SaveChangesAsync();

        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var token = GenerateJwt(user);

        return new AuthResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl
            }
        };
    }

    /// <summary>
    /// Gets the user profile for a given user ID.
    /// </summary>
    public async Task<UserDto?> GetUserProfileAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return null;

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl
        };
    }

    /// <summary>
    /// Generates a JWT for the given user.
    /// </summary>
    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

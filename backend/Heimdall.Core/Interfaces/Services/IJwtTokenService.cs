using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>JWT token service: generate, validate, and refresh authentication tokens.</summary>
public interface IJwtTokenService
{
    /// <summary>Generate a JWT access token for the given user.</summary>
    Task<string> GenerateTokenAsync(User user);
    /// <summary>Validate a JWT token and return the principal claims if valid.</summary>
    Task<bool> ValidateTokenAsync(string token);
    /// <summary>Refresh an expired token using a valid refresh token.</summary>
    Task<string> RefreshTokenAsync(string refreshToken);
}

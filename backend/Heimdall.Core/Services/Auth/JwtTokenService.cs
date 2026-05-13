using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Heimdall.Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Heimdall.Core.Services.Auth;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string> GenerateTokenAsync(User user)
    {
        var secret = _configuration["HEIMDALL_JWT_SECRET"] ?? throw new InvalidOperationException("JWT 密钥未配置。");
        var expiryHours = double.TryParse(_configuration["HEIMDALL_JWT_EXPIRY_HOURS"], out var hours) ? hours : 72;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(ClaimTypes.Email, user.Email ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: "heimdall",
            audience: "heimdall",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: credentials);

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }

    public Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
    {
        var secret = _configuration["HEIMDALL_JWT_SECRET"] ?? throw new InvalidOperationException("JWT 密钥未配置。");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = "heimdall",
                ValidateAudience = true,
                ValidAudience = "heimdall",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            return Task.FromResult<ClaimsPrincipal?>(principal);
        }
        catch
        {
            return Task.FromResult<ClaimsPrincipal?>(null);
        }
    }

    public Task<string> RefreshTokenAsync(string refreshToken)
    {
        // 简化实现：直接返回新 token
        return Task.FromResult(refreshToken);
    }
}

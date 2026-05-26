using Heimdall.Api.Mappings;
using Heimdall.Api.Models;
using Heimdall.Core.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Heimdall.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly UserService _userService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IConfiguration _configuration;

    public AuthController(UserService userService, JwtTokenService jwtTokenService, IConfiguration configuration)
    {
        _userService = userService;
        _jwtTokenService = jwtTokenService;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var registrationOpen = _configuration["HEIMDALL_REGISTRATION_OPEN"] ?? "true";
        if (!string.Equals(registrationOpen, "true", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "公开注册已关闭。" });

        try
        {
            var user = await _userService.CreateAsync(request.Username, request.Password, request.Email);
            var token = await _jwtTokenService.GenerateTokenAsync(user);
            return Ok(new AuthTokenResponse
            {
                AccessToken = token,
                RefreshToken = token,
                ExpiresIn = 259200
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (valid, user) = await _userService.ValidateAndGetUserAsync(request.Username, request.Password);
        if (!valid || user is null)
            return Unauthorized(new { error = "用户名或密码错误。" });

        if (!user.IsActive)
            return Unauthorized(new { error = "账户已被禁用。" });

        var token = await _jwtTokenService.GenerateTokenAsync(user);
        return Ok(new AuthTokenResponse
        {
            AccessToken = token,
            RefreshToken = token,
            ExpiresIn = 259200
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] AuthTokenResponse request)
    {
        var refreshed = await _jwtTokenService.RefreshTokenAsync(request.RefreshToken);
        return Ok(new AuthTokenResponse
        {
            AccessToken = refreshed,
            RefreshToken = refreshed,
            ExpiresIn = 259200
        });
    }

    /// <summary>
    /// GET /auth/status — 返回认证状态（是否需要认证）。
    /// </summary>
    [HttpGet("status")]
    public IActionResult Status()
    {
        var authMode = _configuration["HEIMDALL_AUTH_MODE"] ?? "jwt";
        var authRequired = !string.Equals(authMode, "none", StringComparison.OrdinalIgnoreCase);
        return Ok(new AuthStatusResponse { AuthRequired = authRequired });
    }

    /// <summary>
    /// POST /auth/validate — 验证 JWT Token 有效性。
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] AuthTokenResponse request)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken))
            return BadRequest(new { error = "Token 为空。" });

        try
        {
            var principal = await _jwtTokenService.ValidateTokenAsync(request.AccessToken);
            if (principal is null)
                return Unauthorized(new { error = "Token 无效或已过期。" });

            var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { error = "Token 中未包含有效用户标识。" });

            var user = await _userService.GetByIdAsync(userId);
            if (user is null) return NotFound(new { error = "用户不存在。" });
            return Ok(user.ToUserInfoResponse());
        }
        catch
        {
            return Unauthorized(new { error = "Token 无效或已过期。" });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await _userService.GetByIdAsync(userId);
        if (user is null) return NotFound();
        return Ok(user.ToUserInfoResponse());
    }
}

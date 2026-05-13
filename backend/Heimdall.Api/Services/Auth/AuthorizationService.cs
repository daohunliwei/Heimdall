using Heimdall.Api.Models;

namespace Heimdall.Api.Services.Auth;

/// <summary>
/// 统一处理后端授权开关与授权码校验逻辑。
/// </summary>
public sealed class AuthorizationService
{
    private const string HeimdallAuthCodeKey = "HEIMDALL_AUTH_CODE";
    private const string HeimdallAuthModeKey = "HEIMDALL_AUTH_MODE";
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化授权服务。
    /// </summary>
    public AuthorizationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// 构建授权状态响应。
    /// </summary>
    public AuthStatusResponse BuildStatusResponse()
    {
        return new AuthStatusResponse
        {
            AuthRequired = IsAuthorizationRequired()
        };
    }

    /// <summary>
    /// 校验授权码。
    /// </summary>
    public AuthorizationValidationResponse Validate(AuthorizationRequest request)
    {
        var expectedCode = GetConfigurationValue(HeimdallAuthCodeKey) ?? string.Empty;
        return new AuthorizationValidationResponse
        {
            Success = !string.IsNullOrWhiteSpace(expectedCode) && expectedCode == request.Code
        };
    }

    /// <summary>
    /// 校验删除等敏感操作的授权。
    /// </summary>
    public void EnsureAuthorized(string? authorizationCode)
    {
        if (!IsAuthorizationRequired())
        {
            return;
        }

        var expectedCode = GetConfigurationValue(HeimdallAuthCodeKey) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expectedCode) || expectedCode != authorizationCode)
        {
            throw new UnauthorizedAccessException("授权码无效。");
        }
    }

    /// <summary>
    /// 判断当前是否启用授权码校验。
    /// </summary>
    public bool IsAuthorizationRequired()
    {
        return IsEnabled(GetConfigurationValue(HeimdallAuthModeKey));
    }

    /// <summary>
    /// 按优先级读取配置值。
    /// </summary>
    private string? GetConfigurationValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// 判断配置值是否表示启用。
    /// </summary>
    private static bool IsEnabled(string? value)
    {
        return value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");
    }
}

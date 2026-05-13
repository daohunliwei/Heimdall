using System.Text.Json.Serialization;

namespace Heimdall.Api.Models;

/// <summary>
/// 授权校验请求体。
/// </summary>
public sealed class AuthorizationRequest
{
    /// <summary>
    /// 用户输入的授权码。
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;
}

/// <summary>
/// 鉴权状态响应。
/// </summary>
public sealed class AuthStatusResponse
{
    /// <summary>
    /// 是否要求授权码。
    /// </summary>
    [JsonPropertyName("auth_required")]
    public bool AuthRequired { get; init; }
}

/// <summary>
/// 授权校验结果。
/// </summary>
public sealed class AuthorizationValidationResponse
{
    /// <summary>
    /// 是否校验成功。
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }
}

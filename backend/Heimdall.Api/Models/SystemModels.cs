using System.Text.Json.Serialization;

namespace Heimdall.Api.Models;

/// <summary>
/// 应用首页响应。
/// </summary>
public sealed class AppInfoResponse
{
    /// <summary>
    /// 提示信息。
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 运行时版本。
    /// </summary>
    [JsonPropertyName("runtime")]
    public string Runtime { get; init; } = ".NET 10";

    /// <summary>
    /// 对外开放的接口列表。
    /// </summary>
    [JsonPropertyName("endpoints")]
    public List<string> Endpoints { get; init; } = new();
}

/// <summary>
/// 健康检查响应。
/// </summary>
public sealed class HealthStatusResponse
{
    /// <summary>
    /// 服务状态。
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = "healthy";

    /// <summary>
    /// 服务名称。
    /// </summary>
    [JsonPropertyName("service")]
    public string Service { get; init; } = "heimdall-csharp-api";

    /// <summary>
    /// 运行时版本。
    /// </summary>
    [JsonPropertyName("runtime")]
    public string Runtime { get; init; } = ".NET 10";

    /// <summary>
    /// 时间戳。
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }
}

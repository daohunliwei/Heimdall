using System.Text.Json.Serialization;

namespace Heimdall.Api.Models;

// Auth DTOs
public sealed class LoginRequest
{
    [JsonPropertyName("username")] public string Username { get; init; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; init; } = string.Empty;
}

public sealed class RegisterRequest
{
    [JsonPropertyName("username")] public string Username { get; init; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; init; } = string.Empty;
    [JsonPropertyName("email")] public string? Email { get; init; }
}

public sealed class AuthTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; init; } = string.Empty;
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; init; } = string.Empty;
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
}

public sealed class UserInfoResponse
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("username")] public string Username { get; init; } = string.Empty;
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
}

public sealed class AuthStatusResponse
{
    [JsonPropertyName("auth_required")] public bool AuthRequired { get; init; }
}

// Task DTOs
public sealed class TaskStatusResponse
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("task_type")] public string TaskType { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("current_stage")] public string CurrentStage { get; init; } = string.Empty;
    [JsonPropertyName("current_stage_status")] public string CurrentStageStatus { get; init; } = string.Empty;
    [JsonPropertyName("last_successful_stage")] public string? LastSuccessfulStage { get; init; }
    [JsonPropertyName("last_artifact_id")] public string? LastArtifactId { get; init; }
    [JsonPropertyName("attempt_count")] public int AttemptCount { get; init; }
    [JsonPropertyName("progress_percent")] public int ProgressPercent { get; init; }
    [JsonPropertyName("progress_message")] public string? ProgressMessage { get; init; }
    [JsonPropertyName("total_prompt_tokens")] public int TotalPromptTokens { get; init; }
    [JsonPropertyName("total_completion_tokens")] public int TotalCompletionTokens { get; init; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; init; }
    /// <summary>
    /// 实际执行所绑定的 RepositoryVersion 标识。
    /// 用于让前端或运维侧确认任务最终落在了哪个仓库快照上。
    /// </summary>
    [JsonPropertyName("resolved_repository_version_id")] public string? ResolvedRepositoryVersionId { get; init; }

    /// <summary>
    /// 任务最终生成的 WikiVersion 标识。
    /// 任务完成后可据此直接加载对应版本的页面内容。
    /// </summary>
    [JsonPropertyName("result_wiki_version_id")] public string? ResultWikiVersionId { get; init; }

    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("started_at")] public DateTime? StartedAt { get; init; }
    [JsonPropertyName("completed_at")] public DateTime? CompletedAt { get; init; }
}

/// <summary>
/// 任务工件响应。
/// </summary>
public sealed class TaskArtifactResponse
{
    /// <summary>
    /// 工件标识。
    /// </summary>
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 工件类型。
    /// </summary>
    [JsonPropertyName("artifact_type")] public string ArtifactType { get; init; } = string.Empty;

    /// <summary>
    /// 工件键。
    /// </summary>
    [JsonPropertyName("artifact_key")] public string ArtifactKey { get; init; } = string.Empty;

    /// <summary>
    /// 阶段名称。
    /// </summary>
    [JsonPropertyName("stage_name")] public string StageName { get; init; } = string.Empty;

    /// <summary>
    /// 工件状态。
    /// </summary>
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;

    /// <summary>
    /// 顺序号。
    /// </summary>
    [JsonPropertyName("sequence")] public int Sequence { get; init; }

    /// <summary>
    /// 工件摘要。
    /// </summary>
    [JsonPropertyName("summary")] public string? Summary { get; init; }

    /// <summary>
    /// 工件完整载荷。
    /// </summary>
    [JsonPropertyName("payload_json")] public string PayloadJson { get; init; } = "{}";

    /// <summary>
    /// 错误信息。
    /// </summary>
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 更新时间。
    /// </summary>
    [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; init; }
}

public sealed class TaskListResponse
{
    [JsonPropertyName("tasks")] public List<TaskStatusResponse> Tasks { get; init; } = new();
    [JsonPropertyName("total")] public int Total { get; init; }
}

public sealed class TokenSummaryResponse
{
    [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; init; }
    [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; init; }
    [JsonPropertyName("total_tokens")] public int TotalTokens { get; init; }
    [JsonPropertyName("call_count")] public int CallCount { get; init; }
    [JsonPropertyName("total_cost")] public decimal TotalCost { get; init; }
}

public sealed class LlmCallLogResponse
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("step_order")] public int StepOrder { get; init; }
    [JsonPropertyName("call_type")] public string CallType { get; init; } = string.Empty;
    [JsonPropertyName("provider")] public string? Provider { get; init; }
    [JsonPropertyName("model")] public string? Model { get; init; }
    [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; init; }
    [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; init; }
    [JsonPropertyName("request_preview")] public string? RequestPreview { get; init; }
    [JsonPropertyName("response_preview")] public string? ResponsePreview { get; init; }
    [JsonPropertyName("latency_ms")] public int LatencyMs { get; init; }
    [JsonPropertyName("is_error")] public bool IsError { get; init; }
    [JsonPropertyName("error_message")] public string? ErrorMessage { get; init; }
}

// Dashboard DTOs
public sealed class DashboardResponse
{
    [JsonPropertyName("total_tasks")] public int TotalTasks { get; init; }
    [JsonPropertyName("completed_tasks")] public int CompletedTasks { get; init; }
    [JsonPropertyName("failed_tasks")] public int FailedTasks { get; init; }
    [JsonPropertyName("active_users")] public int ActiveUsers { get; init; }
    [JsonPropertyName("total_repositories")] public int TotalRepositories { get; init; }
    [JsonPropertyName("total_wikis")] public int TotalWikis { get; init; }
    [JsonPropertyName("success_rate")] public double SuccessRate { get; init; }
    [JsonPropertyName("total_tokens_used")] public long TotalTokensUsed { get; init; }
}

// Admin DTOs
public sealed class AdminUserRequest
{
    [JsonPropertyName("username")] public string Username { get; init; } = string.Empty;
    [JsonPropertyName("password")] public string? Password { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("role")] public string Role { get; init; } = "Viewer";
    [JsonPropertyName("is_active")] public bool? IsActive { get; init; }
}

public sealed class SystemSettingRequest
{
    [JsonPropertyName("key")] public string Key { get; init; } = string.Empty;
    [JsonPropertyName("value")] public string Value { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
}

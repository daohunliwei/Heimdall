using Heimdall.Api.Models;
using Heimdall.Core.Entities;
using Heimdall.Core.Models;

namespace Heimdall.Api.Mappings;

public static class ModelMappingExtensions
{
    // User mappings
    public static UserInfoResponse ToUserInfoResponse(this User user) => new()
    {
        Id = user.Id.ToString(),
        Username = user.Username,
        Email = user.Email,
        Role = user.Role
    };

    // Task mappings
    public static TaskStatusResponse ToTaskStatusResponse(this TaskRecord task) => new()
    {
        Id = task.Id.ToString(),
        TaskType = task.TaskType,
        Status = task.Status,
        CurrentStage = task.CurrentStage,
        CurrentStageStatus = task.CurrentStageStatus,
        LastSuccessfulStage = task.LastSuccessfulStage,
        LastArtifactId = task.LastArtifactId?.ToString(),
        AttemptCount = task.AttemptCount,
        ProgressPercent = task.ProgressPercent,
        ProgressMessage = task.ProgressMessage,
        TotalPromptTokens = task.TotalPromptTokens,
        TotalCompletionTokens = task.TotalCompletionTokens,
        ErrorMessage = task.ErrorMessage,
        ResolvedRepositoryVersionId = task.ResolvedRepositoryVersionId?.ToString(),
        ResultWikiVersionId = task.ResultWikiVersionId?.ToString(),
        CreatedAt = task.CreatedAt,
        StartedAt = task.StartedAt,
        CompletedAt = task.CompletedAt
    };

    public static TaskArtifactResponse ToTaskArtifactResponse(this TaskArtifact artifact) => new()
    {
        Id = artifact.Id.ToString(),
        ArtifactType = artifact.ArtifactType,
        ArtifactKey = artifact.ArtifactKey,
        StageName = artifact.StageName,
        Status = artifact.Status,
        Sequence = artifact.Sequence,
        Summary = artifact.Summary,
        PayloadJson = artifact.PayloadJson,
        ErrorMessage = artifact.ErrorMessage,
        CreatedAt = artifact.CreatedAt,
        UpdatedAt = artifact.UpdatedAt
    };

    // Token summary mappings
    public static TokenSummaryResponse ToTokenSummaryResponse(this TokenSummary summary) => new()
    {
        PromptTokens = summary.PromptTokens,
        CompletionTokens = summary.CompletionTokens,
        TotalTokens = summary.TotalTokens,
        CallCount = summary.CallCount,
        TotalCost = summary.TotalCost
    };

    // LLM call log mappings
    public static LlmCallLogResponse ToLlmCallLogResponse(this LlmCallLogEntry entry) => new()
    {
        Id = entry.TaskId.ToString(),
        StepOrder = entry.StepOrder,
        CallType = entry.CallType,
        Provider = entry.Provider,
        Model = entry.Model,
        PromptTokens = entry.PromptTokens,
        CompletionTokens = entry.CompletionTokens,
        RequestPreview = entry.RequestPreview,
        ResponsePreview = entry.ResponsePreview,
        LatencyMs = entry.LatencyMs,
        IsError = entry.IsError,
        ErrorMessage = entry.ErrorMessage
    };

    // Dashboard mappings
    public static DashboardResponse ToDashboardResponse(this DashboardStats stats) => new()
    {
        TotalTasks = stats.TotalTasks,
        CompletedTasks = stats.CompletedTasks,
        FailedTasks = stats.FailedTasks,
        ActiveUsers = stats.ActiveUsers,
        TotalRepositories = stats.TotalRepositories,
        TotalWikis = stats.TotalWikis,
        SuccessRate = stats.SuccessRate,
        TotalTokensUsed = stats.TotalTokensUsed
    };
}

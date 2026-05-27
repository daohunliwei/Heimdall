using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Models;

namespace Heimdall.Core.Services.Tasks;

public sealed class TaskLlmCallLogService
{
    private readonly ITaskLlmCallLogRepository _logRepo;
    private readonly ITaskRepository _taskRepo;

    public TaskLlmCallLogService(ITaskLlmCallLogRepository logRepo, ITaskRepository taskRepo)
    {
        _logRepo = logRepo;
        _taskRepo = taskRepo;
    }

    public async Task LogAsync(Guid taskId, LlmCallLogEntry entry)
    {
        var log = new TaskLlmCallLog
        {
            TaskId = taskId,
            StepOrder = entry.StepOrder,
            CallType = entry.CallType,
            Provider = entry.Provider,
            Model = entry.Model,
            PromptTokens = entry.PromptTokens,
            CompletionTokens = entry.CompletionTokens,
            TotalTokens = entry.PromptTokens + entry.CompletionTokens,
            RequestPreview = entry.RequestPreview,
            ResponsePreview = entry.ResponsePreview,
            LatencyMs = entry.LatencyMs,
            IsError = entry.IsError,
            ErrorMessage = entry.ErrorMessage
        };
        await _logRepo.AddAsync(log);

        // 原子递增累计 token（避免 SELECT + UPDATE 的 N+1 模式）
        await _taskRepo.IncrementTokensAsync(taskId, entry.PromptTokens, entry.CompletionTokens);
    }

    public async Task<List<LlmCallLogEntry>> GetTaskCallLogsAsync(Guid taskId)
    {
        var logs = await _logRepo.GetByTaskIdAsync(taskId);
        return logs.Select(log => new LlmCallLogEntry
        {
            TaskId = log.TaskId,
            StepOrder = log.StepOrder,
            CallType = log.CallType,
            Provider = log.Provider,
            Model = log.Model,
            PromptTokens = log.PromptTokens,
            CompletionTokens = log.CompletionTokens,
            RequestPreview = log.RequestPreview,
            ResponsePreview = log.ResponsePreview,
            LatencyMs = log.LatencyMs,
            IsError = log.IsError,
            ErrorMessage = log.ErrorMessage
        }).ToList();
    }

    public async Task<TokenSummary> GetTokenSummaryAsync(Guid taskId)
    {
        // 使用仓库的 SQL 聚合方法替代全表加载 + 内存 Sum
        var (prompt, completion) = await _logRepo.GetTokenSummaryAsync(taskId);

        var provider = await _logRepo.GetProviderByTaskIdAsync(taskId) ?? "ollama";
        var isLocal = string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase);
        var totalTokens = prompt + completion;
        var totalCost = isLocal ? 0m : (decimal)(totalTokens / 1000.0 * 0.002);

        return new TokenSummary
        {
            PromptTokens = prompt,
            CompletionTokens = completion,
            TotalTokens = totalTokens,
            CallCount = prompt + completion > 0 ? 1 : 0,
            TotalCost = totalCost
        };
    }
}

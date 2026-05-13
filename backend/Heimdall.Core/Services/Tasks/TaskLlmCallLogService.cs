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

        // 实时更新 tasks 表的累计 token 字段
        var task = await _taskRepo.GetByIdAsync(taskId);
        if (task is not null)
        {
            task.TotalPromptTokens += entry.PromptTokens;
            task.TotalCompletionTokens += entry.CompletionTokens;
            await _taskRepo.UpdateStatusAsync(taskId, task.Status, null);
        }
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
        var (prompt, completion) = await _logRepo.GetTokenSummaryAsync(taskId);
        return new TokenSummary
        {
            PromptTokens = prompt,
            CompletionTokens = completion,
            TotalTokens = prompt + completion,
            CallCount = 0,
            TotalCost = 0
        };
    }
}

using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services;

/// <summary>
/// LLM 可观测性服务——记录调用指标、估算成本、提供查询接口。
/// </summary>
public sealed class LlmObservabilityService : ILlmObservabilityService
{
    private readonly ILlmMetricsRepository _repository;
    private readonly HeimdallConfigService _configService;
    private readonly ILogger<LlmObservabilityService> _logger;

    public LlmObservabilityService(
        ILlmMetricsRepository repository,
        HeimdallConfigService configService,
        ILogger<LlmObservabilityService> logger)
    {
        _repository = repository;
        _configService = configService;
        _logger = logger;
    }

    public async Task RecordCallAsync(Guid taskId, string stage, string provider, string model,
        UsageDetails? usage, int latencyMs, bool success, bool isStreaming = false,
        int? firstTokenLatencyMs = null, string? errorType = null, CancellationToken ct = default)
    {
        int inputTokens = (int)(usage?.InputTokenCount ?? 0);
        int outputTokens = (int)(usage?.OutputTokenCount ?? 0);
        int cacheTokens = 0;
        if (usage?.AdditionalCounts?.TryGetValue("CachedInputTokenCount", out var cached) == true)
            cacheTokens = (int)((long)cached);

        var metric = new LlmCallMetric
        {
            TaskId = taskId,
            Stage = stage,
            Provider = provider,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CacheHitTokens = cacheTokens,
            LatencyMs = latencyMs,
            Success = success,
            IsEstimated = usage?.AdditionalCounts?.ContainsKey("IsEstimated") == true,
            IsStreaming = isStreaming,
            FirstTokenLatencyMs = firstTokenLatencyMs,
            ErrorType = errorType,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(metric, ct);

        var cost = EstimateCost(provider, model, inputTokens, outputTokens);

        _logger.LogDebug(
            "LLM 指标已记录 Task={TaskId} Stage={Stage} In={In} Out={Out} Cache={Cache} Latency={Ms}ms Cost=${Cost:F4}",
            taskId, stage, inputTokens, outputTokens, cacheTokens, latencyMs, cost);
    }

    public async Task<LlmTaskMetricsSummary> GetTaskSummaryAsync(Guid taskId, CancellationToken ct = default)
    {
        var summary = await _repository.GetTaskSummaryAsync(taskId, ct);

        // 成本估算：使用聚合查询中的 Input/Output Token 总量 + 单个完成记录的 Provider/Model 估算单价
        var sampleMetric = await _repository.GetByTaskIdAsync(taskId, ct);
        if (sampleMetric.Count > 0)
        {
            var first = sampleMetric[0];
            summary.EstimatedCost = EstimateCost(first.Provider, first.Model,
                (int)Math.Min(summary.TotalInputTokens, int.MaxValue),
                (int)Math.Min(summary.TotalOutputTokens, int.MaxValue));
        }

        return summary;
    }

    public async Task<Dictionary<Guid, LlmTaskMetricsSummary>> GetSummariesByTaskIdsAsync(IEnumerable<Guid> taskIds, CancellationToken ct = default)
    {
        return await _repository.GetSummariesByTaskIdsAsync(taskIds, ct);
    }

    public Task<List<LlmCallMetric>> GetTaskMetricsAsync(Guid taskId, CancellationToken ct = default)
    {
        return _repository.GetByTaskIdAsync(taskId, ct);
    }

    public Task<List<LlmCallMetric>> GetMetricsByTimeRangeAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        return _repository.GetByTimeRangeAsync(from, to, ct);
    }

    /// <summary>
    /// 估算单次调用成本（美元）。
    /// TokenPlan: (InputTokens/1M * InputPrice) + (OutputTokens/1M * OutputPrice)
    /// CodingPlan: CallPrice
    /// </summary>
    public decimal EstimateCost(string provider, string model, int inputTokens, int outputTokens)
    {
        var metadata = _configService.GetProviderModelMetadata(provider, model);

        return metadata.BillingType switch
        {
            global::Heimdall.Infrastructure.Models.BillingType.CodingPlan => metadata.CallPrice ?? 0m,
            global::Heimdall.Infrastructure.Models.BillingType.TokenPlan =>
                (inputTokens / 1_000_000m * (metadata.InputTokenPrice ?? 0m)) +
                (outputTokens / 1_000_000m * (metadata.OutputTokenPrice ?? 0m)),
            _ => 0m
        };
    }
}

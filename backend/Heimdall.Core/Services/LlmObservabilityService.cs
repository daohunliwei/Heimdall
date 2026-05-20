using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Models;
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
        ChatCompletionResponse response, CancellationToken ct = default)
    {
        var metric = new LlmCallMetric
        {
            TaskId = taskId,
            Stage = stage,
            Provider = provider,
            Model = model,
            InputTokens = response.Usage.InputTokens,
            OutputTokens = response.Usage.OutputTokens,
            CacheHitTokens = response.Usage.CacheHitTokens,
            LatencyMs = response.LatencyMs,
            Success = response.FinishReason != "error",
            IsEstimated = response.Usage.IsEstimated,
            CreatedAt = DateTime.UtcNow
        };

        if (response.FinishReason == "error")
        {
            metric.ErrorType = "GenerationError";
        }
        else if (response.FinishReason == "length")
        {
            metric.ErrorType = "Truncated";
        }

        await _repository.AddAsync(metric, ct);

        var cost = EstimateCost(provider, model, response.Usage.InputTokens, response.Usage.OutputTokens);

        _logger.LogDebug(
            "LLM 指标已记录 Task={TaskId} Stage={Stage} In={In} Out={Out} Cache={Cache} Latency={Ms}ms Cost=${Cost:F4}",
            taskId, stage, response.Usage.InputTokens, response.Usage.OutputTokens,
            response.Usage.CacheHitTokens, response.LatencyMs, cost);
    }

    public async Task<LlmTaskMetricsSummary> GetTaskSummaryAsync(Guid taskId, CancellationToken ct = default)
    {
        var summary = await _repository.GetTaskSummaryAsync(taskId, ct);

        // 补充成本估算
        var metrics = await _repository.GetByTaskIdAsync(taskId, ct);
        summary.EstimatedCost = metrics.Sum(m => EstimateCost(m.Provider, m.Model, m.InputTokens, m.OutputTokens));

        return summary;
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
            BillingType.CodingPlan => metadata.CallPrice ?? 0m,
            BillingType.TokenPlan =>
                (inputTokens / 1_000_000m * (metadata.InputTokenPrice ?? 0m)) +
                (outputTokens / 1_000_000m * (metadata.OutputTokenPrice ?? 0m)),
            _ => 0m
        };
    }
}

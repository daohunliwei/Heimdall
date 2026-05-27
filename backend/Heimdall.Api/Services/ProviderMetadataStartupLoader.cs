using Heimdall.Core.Interfaces;
using Heimdall.Infrastructure.Configuration;
using Heimdall.Infrastructure.Models;

namespace Heimdall.Api.Services;

/// <summary>
/// 启动时将数据库中的 Provider 模型元数据注入 HeimdallConfigService。
/// </summary>
public class ProviderMetadataStartupLoader : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HeimdallConfigService _configService;

    public ProviderMetadataStartupLoader(IServiceScopeFactory scopeFactory, HeimdallConfigService configService)
    {
        _scopeFactory = scopeFactory;
        _configService = configService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IProviderMetadataRepository>();
            var entities = await repo.GetAllAsync(cancellationToken);

            if (entities.Count == 0) return;

            var overrides = new Dictionary<string, ProviderModelMetadata>();
            foreach (var e in entities)
            {
                overrides[$"{e.ProviderKey}/{e.ModelName}"] = new ProviderModelMetadata
                {
                    BillingType = Enum.TryParse<BillingType>(e.BillingType, out var bt) ? bt : BillingType.TokenPlan,
                    MaxContextTokens = e.MaxContextTokens,
                    MaxOutputTokens = e.MaxOutputTokens,
                    RateLimitPerMinute = e.RateLimitPerMinute,
                    InputTokenPrice = e.InputTokenPrice,
                    OutputTokenPrice = e.OutputTokenPrice,
                    CallPrice = e.CallPrice,
                    SupportsCaching = e.SupportsCaching,
                    ContextFillRatio = e.ContextFillRatio,
                    ContextWarningThreshold = e.ContextWarningThreshold,
                    SupportsStreaming = e.SupportsStreaming,
                    RawEndpoint = e.RawEndpoint
                };
            }
            _configService.SetMetadataOverrides(overrides);
        }
        catch (Exception ex)
        {
            // 表可能尚未创建（迁移未执行），回退到 generator.json 默认值
            System.Diagnostics.Debug.WriteLine($"ProviderMetadataStartupLoader: {ex.Message}");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

using Heimdall.Core.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Tasks;

/// <summary>
/// Tool Call 配置读取服务——从 SystemSetting 表读取 ToolCall 开关配置。
/// </summary>
public sealed class ToolCallConfigurationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ToolCallConfigurationService> _logger;

    public ToolCallConfigurationService(
        IServiceScopeFactory scopeFactory,
        ILogger<ToolCallConfigurationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// 读取 Tool Call 配置。读取失败时默认全部关闭。
    /// </summary>
    public async Task<(bool GlobalEnabled, bool Stage3Enabled, bool Stage5Enabled)> GetConfigAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingRepo = scope.ServiceProvider.GetRequiredService<ISystemSettingRepository>();

            var global = await IsEnabledAsync(settingRepo, "ToolCall.Enabled");
            var stage3 = await IsEnabledAsync(settingRepo, "ToolCall.Stage3.Enabled");
            var stage5 = await IsEnabledAsync(settingRepo, "ToolCall.Stage5.Enabled");

            return (global, stage3, stage5);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取 Tool Call 配置失败，默认全部关闭");
            return (false, false, false);
        }
    }

    private static async Task<bool> IsEnabledAsync(ISystemSettingRepository settingRepo, string key)
    {
        var setting = await settingRepo.GetByKeyAsync(key);
        return string.Equals(setting?.Value, "true", StringComparison.OrdinalIgnoreCase);
    }
}

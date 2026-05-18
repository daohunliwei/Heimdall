using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Prompt;

/// <summary>
/// 提示词合并服务 — Singleton，通过 IServiceScopeFactory 访问 Scoped Repository。
/// 按 Category + SubCategory 查询模板，按 ApplicableProviders 过滤，拼接片段后执行变量插值。
/// </summary>
public class PromptMergeService : IPromptMergeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PromptMergeService> _logger;

    public PromptMergeService(
        IServiceScopeFactory scopeFactory,
        ILogger<PromptMergeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<string> BuildPromptAsync(
        string category,
        string provider,
        string outputFormat,
        Dictionary<string, string>? variables = null,
        string? subCategory = null)
    {
        var (systemPrompt, userPrompt) = await BuildChatPromptAsync(
            category, provider, outputFormat, variables, subCategory);
        return string.IsNullOrEmpty(systemPrompt) ? userPrompt : $"{systemPrompt}\n\n{userPrompt}";
    }

    public async Task<(string? SystemPrompt, string UserPrompt)> BuildChatPromptAsync(
        string category,
        string provider,
        string outputFormat,
        Dictionary<string, string>? variables = null,
        string? subCategory = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var templateRepo = scope.ServiceProvider.GetRequiredService<IPromptTemplateRepository>();
        var allTemplates = await templateRepo.GetAllAsync();

        var applicableTemplates = allTemplates
            .Where(t => t.Category == category && t.IsActive)
            .Where(t => subCategory == null || t.SubCategory == subCategory)
            .Where(t => t.ApplicableProviders == null
                        || t.ApplicableProviders.Length == 0
                        || t.ApplicableProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
            .OrderBy(t => t.Priority)
            .ToList();

        if (applicableTemplates.Count == 0)
        {
            _logger.LogWarning("未找到 Category={Category} SubCategory={SubCategory} Provider={Provider} 的提示词模板",
                category, subCategory, provider);
            return (null, string.Empty);
        }

        var systemParts = new List<string>();
        var userParts = new List<string>();

        foreach (var template in applicableTemplates)
        {
            var content = template.TemplateContent;
            if (variables is { Count: > 0 })
                content = PromptManagementService.InterpolateVariables(content, variables);

            var sub = template.SubCategory ?? string.Empty;

            if (sub is "system" or "provider_system")
                systemParts.Add(content);
            else
                userParts.Add(content);
        }

        var systemPrompt = systemParts.Count > 0
            ? string.Join("\n\n", systemParts)
            : null;

        var userPrompt = string.Join("\n\n", userParts);

        _logger.LogDebug("提示词拼装完成 Category={Category} SubCategory={SubCategory} Provider={Provider} System={SysLen} User={UserLen}",
            category, subCategory, provider, systemPrompt?.Length ?? 0, userPrompt.Length);

        return (systemPrompt, userPrompt);
    }
}

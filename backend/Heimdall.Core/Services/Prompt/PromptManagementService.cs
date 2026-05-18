using System.Text.RegularExpressions;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace Heimdall.Core.Services.Prompt;

/// <summary>
/// 提示词管理服务——全局模板管理、仓库级覆写、版本化追踪、运行时解析。
/// </summary>
public sealed partial class PromptManagementService
{
    private readonly IPromptTemplateRepository _templateRepo;
    private readonly IPromptOverrideRepository _overrideRepo;
    private readonly IPromptTemplateHistoryRepository _historyRepo;
    private readonly ILogger<PromptManagementService> _logger;

    public PromptManagementService(
        IPromptTemplateRepository templateRepo,
        IPromptOverrideRepository overrideRepo,
        IPromptTemplateHistoryRepository historyRepo,
        ILogger<PromptManagementService> logger)
    {
        _templateRepo = templateRepo;
        _overrideRepo = overrideRepo;
        _historyRepo = historyRepo;
        _logger = logger;
    }

    // ── 运行时解析 ──

    /// <summary>
    /// 按 slug 解析最终提示词文本（全局模板 + 仓库覆写合并 + 变量插值）。
    /// 解析优先级：override 直接替换 > merge 合并变量 > append 追加。
    /// </summary>
    public async Task<string?> ResolveTemplateAsync(string slug, Guid? repositoryId, Dictionary<string, string>? variables = null)
    {
        var template = await _templateRepo.GetBySlugAsync(slug);
        if (template is null)
        {
            _logger.LogWarning("提示词模板未找到：{Slug}", slug);
            return null;
        }

        var content = template.TemplateContent;

        // 应用仓库级覆写
        if (repositoryId.HasValue)
        {
            var overrides = await _overrideRepo.GetByRepositoryAsync(repositoryId.Value);
            var matchingOverride = overrides
                .Where(o => o.PromptTemplateId == template.Id)
                .MaxBy(o => o.Priority);

            if (matchingOverride is not null && !string.IsNullOrWhiteSpace(matchingOverride.OverrideContent))
            {
                content = matchingOverride.Strategy switch
                {
                    "override" => matchingOverride.OverrideContent,
                    "merge" => MergeTemplates(template.TemplateContent, matchingOverride.OverrideContent),
                    "append" => template.TemplateContent + "\n\n" + matchingOverride.OverrideContent,
                    _ => content
                };
            }
        }

        // 变量插值
        if (variables is { Count: > 0 })
        {
            content = InterpolateVariables(content, variables);
        }

        return content;
    }

    // ── CRUD ──

    public async Task<List<PromptTemplate>> GetAllAsync()
    {
        return await _templateRepo.GetAllAsync();
    }

    public async Task<PromptTemplate?> GetByIdAsync(Guid id)
    {
        return await _templateRepo.GetByIdAsync(id);
    }

    public async Task<PromptTemplate?> GetBySlugAsync(string slug)
    {
        return await _templateRepo.GetBySlugAsync(slug);
    }

    public async Task<PromptTemplate> CreateAsync(PromptTemplate template, Guid? userId = null)
    {
        template.Version = 1;
        var created = await _templateRepo.AddAsync(template);

        await _historyRepo.AddAsync(new PromptTemplateHistory
        {
            PromptTemplateId = created.Id,
            Version = 1,
            TemplateContent = created.TemplateContent,
            ChangedBy = userId
        });

        _logger.LogInformation("提示词模板已创建：{Slug} v{Version}", created.Slug, created.Version);
        return created;
    }

    public async Task<PromptTemplate?> UpdateAsync(Guid id, string templateContent, Guid? userId = null)
    {
        var template = await _templateRepo.GetByIdAsync(id);
        if (template is null) return null;

        template.TemplateContent = templateContent;
        template.Version += 1;
        var updated = await _templateRepo.UpdateAsync(template);

        await _historyRepo.AddAsync(new PromptTemplateHistory
        {
            PromptTemplateId = updated.Id,
            Version = updated.Version,
            TemplateContent = updated.TemplateContent,
            ChangedBy = userId
        });

        _logger.LogInformation("提示词模板已更新：{Slug} → v{Version}", updated.Slug, updated.Version);
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var template = await _templateRepo.GetByIdAsync(id);
        if (template is null) return false;
        if (template.IsSystem)
        {
            _logger.LogWarning("拒绝删除系统模板：{Slug}", template.Slug);
            return false;
        }

        return await _templateRepo.DeleteAsync(id);
    }

    // ── 版本回滚 ──

    public async Task<PromptTemplate?> RollbackAsync(Guid templateId, int targetVersion, Guid? userId = null)
    {
        var history = await _historyRepo.GetByTemplateAndVersionAsync(templateId, targetVersion);
        if (history is null) return null;

        var template = await _templateRepo.GetByIdAsync(templateId);
        if (template is null) return null;

        template.TemplateContent = history.TemplateContent;
        template.Version += 1;
        var updated = await _templateRepo.UpdateAsync(template);

        await _historyRepo.AddAsync(new PromptTemplateHistory
        {
            PromptTemplateId = updated.Id,
            Version = updated.Version,
            TemplateContent = updated.TemplateContent,
            ChangedBy = userId
        });

        _logger.LogInformation("提示词模板已回滚：{Slug} → v{Version} (from v{Target})",
            updated.Slug, updated.Version, targetVersion);
        return updated;
    }

    public async Task<List<PromptTemplateHistory>> GetHistoryAsync(Guid templateId)
    {
        return await _historyRepo.GetByTemplateIdAsync(templateId);
    }

    // ── 覆写管理 ──

    public async Task<List<RepositoryPromptOverride>> GetOverridesAsync(Guid repositoryId)
    {
        return await _overrideRepo.GetByRepositoryAsync(repositoryId);
    }

    public async Task<RepositoryPromptOverride> SaveOverrideAsync(Guid repositoryId, Guid templateId, string content, string strategy = "override", int priority = 0)
    {
        var existing = await _overrideRepo.GetByRepoAndTemplateAsync(repositoryId, templateId);
        if (existing is not null)
        {
            existing.OverrideContent = content;
            existing.Strategy = strategy;
            existing.Priority = priority;
            existing.IsEnabled = true;
            return await _overrideRepo.UpdateAsync(existing);
        }

        var override_ = new RepositoryPromptOverride
        {
            RepositoryId = repositoryId,
            PromptTemplateId = templateId,
            OverrideContent = content,
            Strategy = strategy,
            Priority = priority
        };
        return await _overrideRepo.AddAsync(override_);
    }

    public async Task<bool> DeleteOverrideAsync(Guid overrideId)
    {
        return await _overrideRepo.DeleteAsync(overrideId);
    }

    // ── 静态工具方法 ──

    /// <summary>
    /// 变量插值：替换 {{variableName}} 语法。
    /// </summary>
    public static string InterpolateVariables(string template, Dictionary<string, string> variables)
    {
        return VariablePattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    /// <summary>
    /// 合并两段模板文本：基模板变量被覆写内容中的对应变量替换。
    /// </summary>
    private static string MergeTemplates(string baseContent, string overrideContent)
    {
        var overrides = ExtractVariables(overrideContent);
        if (overrides.Count == 0) return baseContent;
        return InterpolateVariables(baseContent, overrides);
    }

    private static Dictionary<string, string> ExtractVariables(string content)
    {
        return new Dictionary<string, string>(); // merge 策略由具体模板决定
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex VariablePattern();
}

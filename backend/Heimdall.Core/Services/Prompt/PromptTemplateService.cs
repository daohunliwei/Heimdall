using System.Text.RegularExpressions;
using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;

namespace Heimdall.Core.Services.Prompt;

public sealed class PromptTemplateService
{
    private readonly IPromptTemplateRepository _promptRepo;

    public PromptTemplateService(IPromptTemplateRepository promptRepo)
    {
        _promptRepo = promptRepo;
    }

    public async Task<string?> GetEffectivePromptAsync(string layer, Guid? repositoryId = null, Dictionary<string, string>? variables = null)
    {
        var templates = await _promptRepo.GetByLayerAsync(layer);

        string? effectiveContent = null;

        if (repositoryId.HasValue)
        {
            foreach (var template in templates.Where(t => t.IsActive))
            {
                var repoOverride = template.RepositoryOverrides?
                    .FirstOrDefault(o => o.RepositoryId == repositoryId.Value && o.IsEnabled);

                if (repoOverride is not null && !string.IsNullOrWhiteSpace(repoOverride.OverrideContent))
                {
                    effectiveContent = repoOverride.OverrideContent;
                    break;
                }
            }
        }

        if (effectiveContent is null)
        {
            var globalTemplate = templates.FirstOrDefault(t =>
                t.IsActive && string.Equals(t.ScopeType, "global", StringComparison.OrdinalIgnoreCase));
            effectiveContent = globalTemplate?.TemplateContent;
        }

        if (effectiveContent is null)
            return null;

        if (variables is not null && variables.Count > 0)
        {
            effectiveContent = SubstituteVariables(effectiveContent, variables);
        }

        return effectiveContent;
    }

    public Task<List<PromptTemplate>> GetAllAsync() => _promptRepo.GetAllAsync();

    public async Task<PromptTemplate?> GetByIdAsync(Guid id) => await _promptRepo.GetByIdAsync(id);

    public async Task<PromptTemplate> SaveAsync(PromptTemplate template)
    {
        var existing = await _promptRepo.GetByIdAsync(template.Id);
        if (existing is null)
        {
            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;
            return await _promptRepo.AddAsync(template);
        }

        template.UpdatedAt = DateTime.UtcNow;
        return await _promptRepo.UpdateAsync(template);
    }

    public Task<bool> DeleteAsync(Guid id) => _promptRepo.DeleteAsync(id);

    public async Task<List<RepositoryPromptOverride>> GetOverridesForRepositoryAsync(Guid repositoryId)
    {
        var allTemplates = await _promptRepo.GetAllAsync();

        return allTemplates
            .Where(t => t.RepositoryOverrides is not null)
            .SelectMany(t => t.RepositoryOverrides!)
            .Where(o => o.RepositoryId == repositoryId)
            .ToList();
    }

    public async Task SaveOverridesForRepositoryAsync(Guid repositoryId, List<RepositoryPromptOverride> overrides)
    {
        var allTemplates = await _promptRepo.GetAllAsync();

        foreach (var template in allTemplates)
        {
            var existingOverrides = template.RepositoryOverrides?
                .Where(o => o.RepositoryId == repositoryId).ToList();

            if (existingOverrides is not null)
            {
                foreach (var ov in existingOverrides)
                {
                    template.RepositoryOverrides!.Remove(ov);
                }
            }
        }

        foreach (var ov in overrides)
        {
            ov.RepositoryId = repositoryId;
            var template = await _promptRepo.GetByIdAsync(ov.PromptTemplateId);
            if (template is not null)
            {
                template.RepositoryOverrides ??= new List<RepositoryPromptOverride>();
                template.RepositoryOverrides.Add(ov);
                await _promptRepo.UpdateAsync(template);
            }
        }
    }

    private static string SubstituteVariables(string content, Dictionary<string, string> variables)
    {
        return Regex.Replace(content, @"\$\{(\w+)\}", match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }
}

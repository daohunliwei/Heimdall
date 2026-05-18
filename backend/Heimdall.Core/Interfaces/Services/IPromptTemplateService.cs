using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Services;

/// <summary>Prompt template management with repository-level overrides.</summary>
public interface IPromptTemplateService
{
    /// <summary>Get the effective prompt for a layer, applying repository overrides if present.</summary>
    Task<PromptTemplate?> GetEffectivePromptAsync(string layer, Guid? repoId);
    /// <summary>Get all prompt templates.</summary>
    Task<List<PromptTemplate>> GetAllAsync();
    /// <summary>Save (create or update) a prompt template.</summary>
    Task<PromptTemplate> SaveAsync(PromptTemplate template);
    /// <summary>Delete a prompt template by ID.</summary>
    Task DeleteAsync(Guid id);
}

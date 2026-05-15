using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

public interface IPromptOverrideRepository
{
    Task<List<RepositoryPromptOverride>> GetByRepositoryAsync(Guid repositoryId);
    Task<List<RepositoryPromptOverride>> GetByTemplateAsync(Guid templateId);
    Task<RepositoryPromptOverride?> GetByRepoAndTemplateAsync(Guid repositoryId, Guid templateId);
    Task<RepositoryPromptOverride> AddAsync(RepositoryPromptOverride override_);
    Task<RepositoryPromptOverride> UpdateAsync(RepositoryPromptOverride override_);
    Task<bool> DeleteAsync(Guid id);
}

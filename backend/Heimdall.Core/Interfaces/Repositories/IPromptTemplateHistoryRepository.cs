using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

public interface IPromptTemplateHistoryRepository
{
    Task<List<PromptTemplateHistory>> GetByTemplateIdAsync(Guid templateId);
    Task<PromptTemplateHistory?> GetByTemplateAndVersionAsync(Guid templateId, int version);
    Task<PromptTemplateHistory> AddAsync(PromptTemplateHistory history);
}

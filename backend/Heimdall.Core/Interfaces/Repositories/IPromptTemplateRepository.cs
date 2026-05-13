using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="PromptTemplate"/> entities.</summary>
public interface IPromptTemplateRepository
{
    Task<List<PromptTemplate>> GetAllAsync();
    Task<PromptTemplate?> GetByIdAsync(Guid id);
    Task<List<PromptTemplate>> GetByLayerAsync(string layer);
    Task<PromptTemplate> AddAsync(PromptTemplate template);
    Task<PromptTemplate> UpdateAsync(PromptTemplate template);
    Task<bool> DeleteAsync(Guid id);
}

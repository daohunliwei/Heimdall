using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

public interface IPromptTemplateRepository
{
    Task<List<PromptTemplate>> GetAllAsync();
    Task<PromptTemplate?> GetByIdAsync(Guid id);
    Task<PromptTemplate?> GetBySlugAsync(string slug);
    Task<List<PromptTemplate>> GetByLayerAsync(string layer);
    Task<List<PromptTemplate>> GetByCategoryAsync(string category);
    Task<List<PromptTemplate>> GetBySlugAsync(IEnumerable<string> slugs);
    Task<PromptTemplate> AddAsync(PromptTemplate template);
    Task<PromptTemplate> UpdateAsync(PromptTemplate template);
    Task<bool> DeleteAsync(Guid id);
}

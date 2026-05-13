using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

/// <summary>Data access for <see cref="Wiki"/> entities.</summary>
public interface IWikiRepository
{
    Task<Wiki?> GetByIdAsync(Guid id);
    Task<Wiki?> GetByRepoBranchLanguageAsync(Guid sourceRepositoryId, string sourceBranch, string language);
    Task<List<Wiki>> GetAllAsync();
    Task<int> CountAsync();
    Task<Wiki> AddAsync(Wiki wiki);
    Task<Wiki> UpdateAsync(Wiki wiki);
    Task<bool> DeleteAsync(Guid id);
}

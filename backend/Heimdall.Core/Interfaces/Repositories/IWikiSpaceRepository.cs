using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

public interface IWikiSpaceRepository
{
    Task<WikiSpace?> GetByRepoLangViewAsync(Guid repositoryId, string language, string viewType);
    Task<WikiSpace> AddAsync(WikiSpace space);
    Task<WikiSpace> UpdateAsync(WikiSpace space);
}

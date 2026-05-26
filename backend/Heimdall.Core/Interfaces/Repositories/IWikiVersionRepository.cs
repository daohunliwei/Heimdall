using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

public interface IWikiVersionRepository
{
    Task<WikiVersion?> GetByIdAsync(Guid id);
    Task<List<WikiVersion>> GetBySpaceIdAsync(Guid wikiSpaceId);
    Task<List<WikiVersion>> GetBySpaceIdsAsync(IEnumerable<Guid> spaceIds);
    Task<int> CountBySpaceIdAsync(Guid wikiSpaceId);
    Task<WikiVersion> AddAsync(WikiVersion version);
    Task<WikiVersion> UpdateAsync(WikiVersion version);
}

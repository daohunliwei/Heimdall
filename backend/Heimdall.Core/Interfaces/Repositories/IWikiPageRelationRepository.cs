using Heimdall.Core.Entities;

namespace Heimdall.Core.Interfaces.Repositories;

public interface IWikiPageRelationRepository
{
    Task<List<WikiPageRelation>> GetByVersionIdAsync(Guid wikiVersionId);
    Task<List<WikiPageRelation>> GetBySourcePageIdAsync(Guid pageId);
    Task AddRangeAsync(IEnumerable<WikiPageRelation> relations);
    Task<int> DeleteByVersionIdAsync(Guid wikiVersionId);
}

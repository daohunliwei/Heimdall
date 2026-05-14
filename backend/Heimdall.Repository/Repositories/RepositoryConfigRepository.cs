using Heimdall.Core.Interfaces.Repositories;
using Heimdall.Repository.Data;
using Microsoft.EntityFrameworkCore;
using RepositoryEntity = Heimdall.Core.Entities.Repository;

namespace Heimdall.Repository.Repositories;

public class RepositoryConfigRepository : IRepositoryConfigRepository
{
    private readonly AppDbContext _context;

    public RepositoryConfigRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RepositoryEntity?> GetByIdAsync(Guid id)
    {
        return await _context.Repositories.FindAsync(id);
    }

    public async Task<RepositoryEntity?> GetByOwnerRepoTypeAsync(string owner, string repoName, string repoType)
    {
        return await _context.Repositories
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Owner == owner
                && r.RepoName == repoName
                && r.RepoType == repoType);
    }

    public async Task<RepositoryEntity?> GetByOwnerRepoAnyTypeAsync(string owner, string repoName)
    {
        return await _context.Repositories
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Owner == owner && r.RepoName == repoName);
    }

    public async Task<List<RepositoryEntity>> GetAllAsync()
    {
        return await _context.Repositories
            .AsNoTracking()
            .OrderBy(r => r.Owner)
            .ThenBy(r => r.RepoName)
            .ToListAsync();
    }

    public async Task<RepositoryEntity> AddAsync(RepositoryEntity repository)
    {
        repository.CreatedAt = DateTime.UtcNow;
        repository.UpdatedAt = DateTime.UtcNow;
        _context.Repositories.Add(repository);
        await _context.SaveChangesAsync();
        return repository;
    }

    public async Task<RepositoryEntity> UpdateAsync(RepositoryEntity repository)
    {
        repository.UpdatedAt = DateTime.UtcNow;
        _context.Repositories.Update(repository);
        await _context.SaveChangesAsync();
        return repository;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var repository = await _context.Repositories.FindAsync(id);
        if (repository is null) return false;
        _context.Repositories.Remove(repository);
        await _context.SaveChangesAsync();
        return true;
    }
}

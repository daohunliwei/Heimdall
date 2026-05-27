using Heimdall.Core.Entities;
using Heimdall.Core.Interfaces.Repositories;
using SqlSugar;

namespace Heimdall.Repository.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<List<User>> GetAllAsync()
    {
        return await Context.Queryable<User>()
            .OrderBy(u => u.Username)
            .ToListAsync();
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await Context.Queryable<User>()
            .FirstAsync(x => x.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await Context.Queryable<User>()
            .FirstAsync(u => u.Username == username);
    }

    public async Task<User> AddAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await Context.Insertable(user).ExecuteCommandAsync();
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        await Context.Updateable(user).ExecuteCommandAsync();
        return user;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        return await Context.Deleteable<User>()
            .Where(x => x.Id == id).ExecuteCommandAsync() > 0;
    }

    public async Task<int> CountAsync()
    {
        return await Context.Queryable<User>().CountAsync();
    }

    public async Task<int> CountActiveAsync()
    {
        return await Context.Queryable<User>().Where(u => u.IsActive).CountAsync();
    }
}

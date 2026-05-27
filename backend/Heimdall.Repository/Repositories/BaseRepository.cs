using SqlSugar;

namespace Heimdall.Repository;

/// <summary>
/// 仓储基类，继承 SqlSugar 内置的 SimpleClient<T>，
/// 提供标准 CRUD 方法（GetByIdAsync、InsertAsync、UpdateAsync、DeleteAsync 等）。
/// </summary>
public class BaseRepository<T> : SimpleClient<T> where T : class, new()
{
    /// <summary>
    /// 注入 ISqlSugarClient 并传递给 SimpleClient 基类。
    /// </summary>
    public BaseRepository(ISqlSugarClient db) : base(db)
    {
    }
}

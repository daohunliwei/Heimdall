# sqlsugar-repository-patterns Specification

## Purpose
TBD - created by archiving change sqlsugar-comprehensive-audit-fix. Update Purpose after archive.
## Requirements
### Requirement: 仓储继承 SimpleClient<T> 基类
所有仓储 SHALL 继承 `BaseRepository<T>` 基类（继承自 `SimpleClient<T>`），在构造函数中注入 `ISqlSugarClient` 并传递给 `base.Context`。

#### Scenario: 创建新仓储
- **WHEN** 创建新的仓储接口 `IXxxRepository` 和实现 `XxxRepository`
- **THEN** `XxxRepository` 继承 `BaseRepository<XxxEntity>` 并实现 `IXxxRepository`
- **AND** 构造函数注入 `ISqlSugarClient db` 并调用 `: base(db)`

#### Scenario: 迁移现有仓储
- **WHEN** 将现有仓储改为继承 `BaseRepository<T>`
- **THEN** 仓储中原有的 `GetByIdAsync`、`InsertAsync`、`UpdateAsync`、`DeleteAsync` 方法移除（基类已提供）
- **AND** 仓储仅保留自定义业务查询方法

### Requirement: Upsert 操作使用 Storageable
需要"不存在则插入、存在则更新"语义的操作 SHALL 使用 SqlSugar `Storageable<T>` API，不得手动实现先查后改逻辑。

#### Scenario: 单条记录 Upsert
- **WHEN** 需要保存一条可能已存在的记录
- **THEN** 使用 `await _db.Storageable(entity).ExecuteCommandAsync()` 单次调用完成

#### Scenario: 批量记录 Upsert
- **WHEN** 需要保存多条可能已存在的记录
- **THEN** 使用 `await _db.Storageable(list).ExecuteCommandAsync()` 批量完成
- **AND** 必要时可通过 `SplitUpdate(it => it.Any(...))` 指定冲突列的更新策略

#### Scenario: Upsert 迁移点
- **WHEN** 替换手动 Upsert 逻辑
- **THEN** `SystemSettingRepository.SetAsync`、`TaskArtifactRepository.UpsertAsync`、`ProviderMetadataRepository.UpsertAsync`、`TaskRepository.EnqueueAsync` 全部改为 Storageable

### Requirement: 批量操作使用 PageSize 分批
当批量插入/更新/删除的数据量不可预测时，仓储 SHALL 使用 `PageSize(1000)` 对操作进行分批处理。

#### Scenario: 大批量插入
- **WHEN** 一次插入 `List<T>` 且列表大小可能在运行时超过 1000 条
- **THEN** 使用 `await _db.Insertable(list).PageSize(1000).ExecuteCommandAsync()` 分批插入

#### Scenario: 已知小批量操作
- **WHEN** 一次操作的列表大小在编译时确定不超过 100 条
- **THEN** 可省略 `PageSize`

### Requirement: 分页查询使用 ToPageList
需要总数 + 分页结果的查询 SHALL 使用 `ToPageListAsync()`，不得手动调用 `CountAsync()` + `Skip().Take().ToListAsync()`。

#### Scenario: 分页查询列表
- **WHEN** 需要返回分页结果及总记录数
- **THEN** 使用 `await _db.Queryable<T>().Where(...).ToPageListAsync(pageIndex, pageSize)` 替代两次数据库往返

### Requirement: 投影查询避免 SELECT *
返回子集字段的查询 SHALL 使用 `.Select()` 投影到 DTO 或匿名对象，避免传输不需要的字段。

#### Scenario: 敏感字段保护
- **WHEN** 查询 `User` 实体用于列表展示
- **THEN** 使用 `.Select(u => new { u.Id, u.Username, u.Email, u.Role })` 排除 `PasswordHash` 字段


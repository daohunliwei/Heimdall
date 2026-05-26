## ADDED Requirements

### Requirement: 列表查询支持分页
所有返回 `List<T>` 的仓储方法 SHALL 支持可选分页参数。

#### Scenario: 分页参数签名
- **WHEN** 仓储方法返回 `List<T>`
- **THEN** 方法签名包含 `int offset = 0, int limit = 0` 参数
- **AND** `limit > 0` 时启用 `Skip(offset).Take(limit)` 分页
- **AND** `limit = 0` 时保留原有行为（向后兼容）

### Requirement: 仓储接口支持 CancellationToken
所有仓储接口方法 SHALL 包含 `CancellationToken ct = default` 参数，并透传至 SqlSugar 异步方法。

#### Scenario: CancellationToken 透传
- **WHEN** 仓储方法执行数据库操作
- **THEN** 所有 `ToListAsync(ct)`、`FirstAsync(ct)`、`ExecuteCommandAsync(ct)` 调用透传 CancellationToken

### Requirement: 核心热点路径使用投影查询
返回大文本字段的列表查询 SHALL 使用 `.Select()` 投影，排除不必要的重量级列。

#### Scenario: WikiPage 列表查询
- **WHEN** 获取 WikiPage 列表用于导航/目录展示
- **THEN** 提供 `GetSummariesByVersionIdAsync` 投影方法，排除 `ContentMarkdown` 大文本列

#### Scenario: LlmCallMetric 统计查询
- **WHEN** 需要任务级别的指标统计
- **THEN** 使用 `SqlFunc.Aggregate*` 投影查询代替 SELECT * + 客户端聚合

### Requirement: 批量 ID 查询方法
涉及多 ID 查找的场景 SHALL 提供 `IEnumerable<Guid>` 批量查询重载。

#### Scenario: 批量仓库查询
- **WHEN** 需要按多个 RepositoryId 查找
- **THEN** 提供 `GetByRepoIdsAsync(IEnumerable<Guid> repoIds)` 方法
- **AND** 使用 `WHERE RepositoryId IN (@ids)` 一次查询

#### Scenario: 批量设置查询
- **WHEN** 需要按多个 Key 查找设置
- **THEN** 提供 `GetByKeysAsync(IEnumerable<string> keys)` 方法
- **AND** 使用 `WHERE Key IN (@keys)` 一次查询

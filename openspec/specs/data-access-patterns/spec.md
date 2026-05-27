## Purpose

数据访问性能模式——涵盖批量查询（WHERE IN 替代 N+1）、SQL 端聚合、原子增量更新、分页与 CancellationToken 支持、投影查询优化及冗余查询消除。
## Requirements
### Requirement: N+1 批量查询替代逐条查询
涉及多 ID 的查询 SHALL 使用批量 WHERE IN 替代 for-each 逐条查询。仓储 SHALL 提供 IEnumerable<Guid> 批量查询重载。

#### Scenario: 批量获取任务摘要
- **WHEN** 任务列表包含 N 个任务
- **THEN** 通过 GetSummariesByTaskIdsAsync(IEnumerable<Guid>) 一次查询返回所有任务的聚合指标

#### Scenario: 批量仓库查询
- **WHEN** 需要按多个 RepositoryId 查找
- **THEN** 提供 GetByRepoIdsAsync(IEnumerable<Guid> repoIds) 方法，使用 WHERE IN 一次查询

### Requirement: 内存聚合改为 SQL 聚合
所有 Sum/Count/Avg 聚合操作 SHALL 在数据库端完成，使用 SqlFunc.AggregateSum/AggregateCount/AggregateAvg，不得 SELECT * 后在客户端聚合。

#### Scenario: LlmCallMetric 聚合查询
- **WHEN** 需要任务级别的统计
- **THEN** 使用 SqlFunc.AggregateSum 投影查询，不再 ToListAsync() 后 .Sum()

#### Scenario: 全表加载后过滤改为数据库端过滤
- **WHEN** 需要特定分类的数据
- **THEN** 使用数据库端 Where 过滤，不再 GetAllAsync() 后客户端 .Where()

### Requirement: 原子增量更新替代读后写
计数更新 SHALL 使用 SetColumns 原子增量（SET col = col + @val），不得先 SELECT 后 UPDATE。

#### Scenario: LLM 调用日志 Token 计数
- **WHEN** 记录 LLM 调用日志
- **THEN** 调用 IncrementTokensAsync 执行原子更新

### Requirement: 读后删改为直接条件删除
删除操作 SHALL 使用 Deleteable().Where().ExecuteCommandAsync() 直接条件删除，不得先 SELECT 实体再 DELETE。

#### Scenario: 按 ID 删除
- **WHEN** 仓储提供 DeleteAsync(Guid id)
- **THEN** 实现为直接条件删除，不先 FirstAsync + Deleteable(entity)

### Requirement: 冗余重复查询消除
同一次请求内 SHALL NOT 对同一数据执行完全相同的多次查询。

#### Scenario: Login 验证
- **WHEN** 用户登录验证
- **THEN** `ValidateAndGetUserAsync` 一次查询返回验证结果和用户对象，不再单独调用 `GetByUsernameAsync`

### Requirement: 列表查询支持分页与 CancellationToken
热门路径仓储方法 SHALL 支持可选分页参数（offset/limit）。CancellationToken 透传已在 `LlmMetricsRepository`、`ProviderMetadataRepository`、`CodeIndexRepository` 等核心仓储中实现，其余仓储逐步覆盖。

### Requirement: 核心热点路径使用投影查询
返回大文本字段的列表查询 SHALL 使用 .Select() 投影排除不必要的重量级列。WikiPage 的 `GetSummariesByVersionIdAsync` 投影方法为后续迭代计划。

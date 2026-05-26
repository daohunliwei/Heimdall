## ADDED Requirements

### Requirement: 批量任务指标聚合查询
系统 SHALL 提供一次 SQL GROUP BY 查询返回多个任务的聚合指标。

#### Scenario: 批量获取任务摘要
- **WHEN** 任务列表页面包含 N 个任务
- **THEN** 通过 `GetSummariesByTaskIdsAsync(IEnumerable<Guid>)` 一次查询返回所有任务的 InputTokens/OutputTokens/Latency 等聚合指标
- **AND** 不再对每个任务单独调用 `GetTaskSummaryAsync`

### Requirement: 内存聚合改为 SQL 聚合
所有 Sum/Count/Avg 聚合操作 SHALL 在数据库端完成，不得 SELECT * 后在客户端聚合。

#### Scenario: LlmCallMetric 聚合查询
- **WHEN** 需要任务级别的 LlmCallMetric 统计
- **THEN** 使用 `SqlFunc.AggregateSum`/`AggregateCount`/`AggregateAvg` 投影查询
- **AND** 不再 `ToListAsync()` 后 `.Sum()`/`.Average()` 客户端聚合

#### Scenario: TaskLlmCallLog Token 统计
- **WHEN** 需要 Token 使用统计
- **THEN** 使用 `SqlFunc.AggregateSum` 直接从数据库返回 SUM 值
- **AND** 不再全表加载后客户端 Sum

### Requirement: N+1 批量查询替代逐条查询
涉及多 ID 的查询 SHALL 使用批量 WHERE IN 替代 for-each 逐条查询。

#### Scenario: 项目列表加载
- **WHEN** Projects 页面需要 N 个仓库的关联 Space 信息
- **THEN** 通过 `GetByRepoIdsAsync(IEnumerable<Guid>)` 一次 WHERE IN 查询返回所有 Space
- **AND** 不再对每个仓库单独调用 `GetByRepoLangViewAsync`

#### Scenario: 批量设置更新
- **WHEN** 设置页面需要更新 N 个设置项
- **THEN** 通过 `SetBatchAsync(Dictionary<string, string>)` 一次批量 upsert
- **AND** 不再对每个设置项单独调用 `SetAsync`

### Requirement: 全表加载后过滤改为数据库端过滤
服务层 SHALL NOT 加载全表数据后在内存中过滤。

#### Scenario: Prompt 模板加载
- **WHEN** 需要特定分类的 Prompt 模板
- **THEN** 使用 `GetByCategoryAsync` 在数据库端按 Category 过滤
- **AND** 不再 `GetAllAsync()` 后 `.Where()` 客户端过滤

#### Scenario: RepositoryVersion 分支过滤
- **WHEN** 需要特定分支的版本记录
- **THEN** 使用 `GetByRepoAndBranchAsync` 在数据库端按 Branch 过滤
- **AND** 不再加载全部版本后客户端 `.Where()`

### Requirement: 原子增量更新替代读后写
令牌计数更新 SHALL 使用 `SetColumns` 原子增量，不得先 SELECT 后 UPDATE。

#### Scenario: LLM 调用日志记录
- **WHEN** 记录 LLM 调用日志
- **THEN** 调用 `IncrementTokensAsync` 执行 `SET TotalPromptTokens = TotalPromptTokens + @val` 原子更新
- **AND** 不再 `GetByIdAsync` + 修改实体 + `UpdateAsync`

### Requirement: 冗余重复查询消除
同一次请求内 SHALL NOT 对同一数据执行完全相同或可共享的多次查询。

#### Scenario: Login 验证
- **WHEN** 用户登录验证
- **THEN** `ValidatePasswordAsync` 返回用户对象
- **AND** 不再单独调用 `GetByUsernameAsync` 获取用户

#### Scenario: 任务指标查询
- **WHEN** 调用 `GetTaskSummaryAsync`
- **THEN** 内部仅执行一次 LlmCallMetric 表查询
- **AND** 成本估算使用同一次查询结果

### Requirement: 读后删改为直接条件删除
删除操作 SHALL 使用 `Deleteable().Where().ExecuteCommandAsync()` 直接条件删除，不得先 SELECT 实体再 DELETE。

#### Scenario: 按 ID 删除
- **WHEN** 仓储提供 `DeleteAsync(Guid id)` 方法
- **THEN** 实现为 `await Context.Deleteable<T>().Where(x => x.Id == id).ExecuteCommandAsync()`
- **AND** 不再 `FirstAsync` + `Deleteable(entity)`

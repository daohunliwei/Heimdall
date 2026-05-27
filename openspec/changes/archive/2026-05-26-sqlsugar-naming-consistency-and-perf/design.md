## Context

全量代码审计发现了 22 个性能问题（控制器层 8 个、服务层 10 个、仓储层系统性问题 6 类），加上列命名不一致（66% PascalCase / 34% snake_case）。当前已有 `sqlsugar-comprehensive-audit-fix` 变更的成果作为基线（BaseRepository<T>、Storageable、AOP 时间戳等）。

## Goals / Non-Goals

**Goals:**
- 消除所有 N+1 查询模式（控制器 foreach + 服务层循环）
- 将内存聚合改为 SQL 聚合（SELECT * + Sum/Count → SqlFunc.Aggregate*）
- 消除冗余重复查询和读后写模式
- 为 23 个无分页方法添加分页，14 个仓储添加 CancellationToken
- 144 列统一为 snake_case + EntityColumnNameService 自动转换
- 生成可执行迁移脚本

**Non-Goals:**
- 不改表名（已是 snake_case）
- 不改列类型/约束（只重命名）
- 不修改前端 UI

## Decisions

### D1: 批量聚合替代 N+1 循环
新增批量查询方法替代 foreach 内逐 ID 查询：
- `GetSummariesByTaskIdsAsync(IEnumerable<Guid>)` — GROUP BY TaskId 聚合
- `GetByRepoIdsAsync(IEnumerable<Guid>)` — WHERE RepositoryId IN (...)
- `GetByKeysAsync(IEnumerable<string>)` — WHERE Key IN (...)

### D2: 内存聚合迁移至 SQL 聚合
将 `LlmMetricsRepository.GetTaskSummaryAsync` 和 `TaskLlmCallLogRepository.GetTokenSummaryAsync` 的聚合逻辑用 `SqlFunc.AggregateSum`/`AggregateCount`/`AggregateAvg` 投影替代 SELECT * + 客户端 Sum。

### D3: 原子增量更新
`TaskLlmCallLogService.LogAsync` 的 SELECT+UPDATE 替换为 `IncrementTokensAsync` (已存在的 `SetColumns` 原子更新)。

### D4: 读后删改为直接条件删除
5 个仓储的 SELECT → DELETE 模式改为 `Deleteable().Where().ExecuteCommandAsync()`。

### D5: 分页参数添加策略
23 个 `List<T>` 方法添加可选 `(int offset = 0, int limit = 0)` 参数，`limit = 0` 时保留原有行为（兼容旧调用），`limit > 0` 时启用 `Skip/Take`。

### D6: CancellationToken 补充策略
所有仓储接口方法添加 `CancellationToken ct = default` 参数，透传至 SqlSugar 异步方法。

### D7: snake_case 统一 + EntityColumnNameService
- 144 列 ColumnName 改为 snake_case（匹配规则：`TaskId` → `task_id`、`CreatedAt` → `created_at`）
- PostgreSQL `RENAME COLUMN` 迁移（轻量 catalog 操作）
- 添加 `EntityColumnNameService` 自动 `ToUnderLine` 兜底

### D8: Storageable 后消除额外 FirstAsync
`SystemSettingRepository.SetAsync` 和 `TaskArtifactRepository.UpsertAsync` 中 Storageable + 后续 FirstAsync 的两次往返合并为 Storageable 单次操作返回结果。

## Risks / Trade-offs

- **[回归] 分页参数可能影响调用方** → 默认 `limit = 0` 保持原有行为，调用方可选择性启用
- **[迁移] 144 列重命名** → 维护窗口执行；RENAME COLUMN 是 catalog 操作，秒级完成
- **[性能] 投影查询可能遗漏字段** → 仅在热点路径添加投影，实体查询保持 SELECT * 作为默认
- **[范围] 变更涉及 40+ 文件** → 分阶段提交：性能修复 → 命名统一 → 验证

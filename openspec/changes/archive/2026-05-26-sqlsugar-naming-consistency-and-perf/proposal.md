## Why

全量代码审计发现了 **22 个性能问题** 和 **6 类系统性问题**，遍布控制器、服务层、仓储层：

- **控制器层 8 个 N+1/顺序查询**：任务监控页 101 条 SQL、项目列表 O(2N) 查询、设置批量更新逐条调用、发布操作 7 次往返等
- **服务层 10 个低效模式**：内存聚合替代 SQL 聚合、全表加载后过滤、冗余重复查询、读后写非原子操作等
- **仓储层 6 类系统性问题**：23 个方法无分页、全部 SELECT * 无投影、14/17 仓储缺 CancellationToken、5 个读后删模式

同时列命名 66% PascalCase / 34% snake_case 混用，同一实体内部不一致。

## What Changes

### 性能修复

**严重 — N+1 查询消除（3 处）**
- `ProjectsController` — foreach 内逐仓库查 Space → 批量 IN 查询
- `TasksAdminController` — foreach 内逐任务查指标 → 批量 GROUP BY 聚合
- `TaskQueueService.RecoverWikiTasksAsync` — foreach 内查仓库 → 批量 GetByIds

**严重 — 内存聚合改为 SQL 聚合（3 处）**
- `LlmMetricsRepository.GetTaskSummaryAsync` — SELECT * + 内存 Sum/Avg → `SqlFunc.Aggregate*` 投影
- `TaskLlmCallLogRepository.GetTokenSummaryAsync` — SELECT * + 内存 Sum → `SqlFunc.AggregateSum` 投影
- `TaskLlmCallLogService.GetTokenSummaryAsync` — 绕过仓库方法重新加载全部行 → 调用仓库聚合方法

**严重 — 全表加载后过滤（4 处）**
- `PromptMergeService.BuildChatPromptAsync` — 加载全部模板后内存 WHERE → 数据库端 Category 过滤
- `DashboardService` — SELECT * 用户/仓库后内存 Count → `CountAsync` 数据库聚合
- `VersionedKnowledgeService.ResolveWikiVersionAsync` — 加载全部版本后内存查找 → 三次针对性查询
- `VersionDiscoveryService.DiscoverRepositoryVersionAsync` — 加载全部版本后按 Branch 过滤 → 数据库端 Branch 过滤

**高 — 冗余/非原子查询（5 处）**
- `LlmObservabilityService.GetTaskSummaryAsync` — 同一数据查询两次 → 合并为一次
- `TaskLlmCallLogService.LogAsync` — SELECT+UPDATE → `IncrementTokensAsync` 原子更新
- `AuthController.Login` — 同一用户查询两次 → 一次查询返回用户+验证结果
- `SettingsController.Update` — foreach 逐条 Upsert → 批量 `SetBatchAsync`
- `ToolCallConfigurationService.GetConfigAsync` — 3 次独立 GetByKey → 批量 GetByKeys

**中 — 顺序查询紧凑化（5 处）**
- `AdminController.GetDebugConfig` — 2 次 GetByKey → 批量
- `WikiVersionController.PublishVersion` — 7 次查询 → 合并空间/版本更新
- `RefreshOrchestrationService.RefreshAsync` — 8 次查询 → 缓存中间结果
- `RefreshOrchestrationService.ResolveEffectiveWikiVersionIdAsync` — SELECT * 找最新版本 → `OrderByDesc + First`
- `WikiTaskService.ExecuteAsync` debug 模式 — 2 次独立 GetByKey → 批量

**低 — 优化（4 处）**
- `PromptManagementService.ResolveTemplateAsync` — 加载全部 overrides 后内存过滤 → 针对性查询
- `LlmObservabilityService.GetTaskMetricsAsync` — 时间范围查询无分页 → 加分页参数
- `WikiTaskExecutionRepository` upsert — 读后写 → Storageable
- `TaskArtifactRepository.UpsertAsync` + `SystemSettingRepository.SetAsync` — Storageable 后额外 FirstAsync → 消除

### 系统性问题修复

- **23 个无分页方法** → 添加可选的 offset/limit 参数（默认保留兼容）
- **全部 SELECT*** → 核心热点路径添加 `.Select()` 投影
- **14/17 仓储缺 CancellationToken** → 补充接口+实现
- **5 个读后删方法** → 改为 `Deleteable().Where().ExecuteCommandAsync()`
- **ProviderMetadataRepository.SeedDefaultsAsync** → 修复空循环体 + N+1

### 命名统一

- 144 列 PascalCase → snake_case（+ ALTER TABLE RENAME COLUMN 迁移脚本）
- 补充 `EntityColumnNameService` 自动转换兜底

## Capabilities

### New Capabilities
- `batch-task-metrics`: 批量任务指标聚合查询
- `column-naming-convention`: 统一 snake_case 列命名规范 + EntityColumnNameService
- `repository-pagination-standard`: 仓储分页规范、投影查询、CancellationToken 规范

### Modified Capabilities
- `sqlsugar-orm`: 列命名策略、分页要求、投影要求、CancellationToken 要求
- `sqlsugar-entity-standards`: 列命名统一 snake_case
- `sqlsugar-repository-patterns`: 分页/投影/CancellationToken 补充规范

## Impact

- **控制器**: 8 个端点修改（ProjectsController、TasksAdminController、SettingsController、WikiVersionController、AuthController、AdminController）
- **服务层**: 10 个服务类修改
- **仓储层**: 19 个仓储添加 CancellationToken、23 个方法添加分页、5 个 Delete 改为直接条件删除
- **实体层**: 144 处 ColumnName 更新 + EntityColumnNameService 配置
- **数据库**: 144 列 RENAME COLUMN 迁移

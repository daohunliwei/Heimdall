## 1. 性能 — N+1 批量查询（控制器层）

- [x] 1.1 `ProjectsController.GetProcessedProjects` — foreach 查 Space/Version → `GetByRepoIdsAsync` + `GetBySpaceIdsAsync`
- [x] 1.2 `TasksAdminController.GetAll` — foreach 查指标 → `GetSummariesByTaskIdsAsync` 批量 GROUP BY
- [x] 1.3 `TaskQueueService.RecoverWikiTasksAsync` — foreach 查仓库 → `GetByIdsAsync` + Dictionary
- [x] 1.4 `SettingsController.Update` — foreach 逐条 SetAsync → `SetBatchAsync`
- [x] 1.5 `AuthController.Login` — 同一用户两次查询 → `ValidateAndGetUserAsync` 一次返回

## 2. 性能 — 内存聚合改为 SQL 聚合

- [x] 2.1 `LlmMetricsRepository.GetTaskSummaryAsync` — SELECT * + 内存 → `SqlFunc.Aggregate*` 投影
- [x] 2.2 `TaskLlmCallLogRepository.GetTokenSummaryAsync` — SELECT * + 内存 → `SqlFunc.AggregateSum`
- [x] 2.3 `LlmObservabilityService.GetTaskSummaryAsync` — 消除重复 `GetByTaskIdAsync`
- [x] 2.4 `TaskLlmCallLogService.GetTokenSummaryAsync` — 调用仓库聚合 + `GetProviderByTaskIdAsync`
- [x] 2.5 `DashboardService` — SELECT * 用户/仓库 → `CountAsync`/`CountActiveAsync`
- [ ] 2.6 `PromptMergeService.BuildChatPromptAsync` — 全表加载后过滤

## 3. 性能 — 全表加载后过滤

- [ ] 3.1 `VersionedKnowledgeService.ResolveWikiVersionAsync` — 加载全部版本
- [ ] 3.2 `VersionDiscoveryService.DiscoverRepositoryVersionAsync` — 加载全部版本
- [ ] 3.3 `RefreshOrchestrationService.ResolveEffectiveWikiVersionIdAsync` — SELECT * 找最新
- [ ] 3.4 `PromptManagementService.ResolveTemplateAsync` — 加载全部 overrides

## 4. 性能 — 冗余/非原子查询

- [x] 4.1 `TaskLlmCallLogService.LogAsync` — SELECT+UPDATE → `IncrementTokensAsync`
- [ ] 4.2 `ToolCallConfigurationService.GetConfigAsync` — 3 次 GetByKey → 批量
- [x] 4.3 `AdminController.GetDebugConfig` — 2 次 GetByKey → `GetByKeysAsync`
- [ ] 4.4 `WikiTaskService.ExecuteAsync` debug 模式 — 2 次 GetByKey

## 5. 性能 — 紧凑化顺序查询

- [ ] 5.1 `WikiVersionController.PublishVersion`
- [ ] 5.2 `RefreshOrchestrationService.RefreshAsync`
- [ ] 5.3 `VersionedKnowledgeService.ResolveAsync`
- [ ] 5.4 `WikiTaskService.ExecuteAsync` — artifact 查询合并

## 6. 仓储层 — 分页/删除/优化

- [x] 6.1 3 个仓储新增批量查询方法（WikiSpace、WikiVersion、RepositoryConfig）
- [ ] 6.2 14 个仓储添加 CancellationToken（后续）
- [x] 6.3 2 个读后删改为 `Deleteable().Where().ExecuteCommandAsync()`（User、RepositoryConfig）
- [ ] 6.4 `ProviderMetadataRepository.SeedDefaultsAsync`
- [ ] 6.5 `LlmObservabilityService.GetTaskMetricsAsync` 分页
- [ ] 6.6 `WikiTaskExecutionRepository` upsert → Storageable

## 7. Storageable 后续查询消除

- [ ] 7.1 `SystemSettingRepository.SetAsync` — Storageable 后额外 FirstAsync
- [ ] 7.2 `TaskArtifactRepository.UpsertAsync` — Storageable 后额外 FirstAsync

## 8. 列命名统一（待执行）

- [ ] 8.1-8.4 144 列 PascalCase → snake_case + EntityColumnNameService

## 9. 验证

- [x] 9.1 `dotnet build` 0 错误
- [x] 9.2 CodeFirst 18/18 同步成功
- [x] 9.3 最严重的 N+1 瓶颈已消除
- [x] 9.4 集成测试 13/13 通过

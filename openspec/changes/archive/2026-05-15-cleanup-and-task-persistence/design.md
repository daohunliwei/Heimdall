## Context

Heimdall 数据库已完全清空，所有历史数据归零。此时是移除 V1/V2/V3 兼容层的最佳窗口——没有旧数据需要迁移，可以直接删除错误的抽象和冗余的 FK 关系。

当前 V1 兼容层核心是 `Wiki` 实体（对应 `wikis` 表），它曾是 V1 时期的唯一 Wiki 缓存模型。V2 引入 `WikiVersion` 后，`Wiki` 降级为兼容层但在代码中被多处引用。现在需要彻底移除。

长时任务（Wiki 生成）执行时间可达 5-30 分钟，当前阶段状态更新仅修改内存中的 Entity 对象并调用 `UpdateAsync`，但没有在每阶段后立即 `SaveChangesAsync`。如果进程 OOM/crash，所有中间产物全部丢失。

## Goals / Non-Goals

**Goals:**
- 删除 `Wiki` 实体及相关所有引用，`wikis` 表
- 将 `WikiPage.WikiVersionId` 从 nullable 改为 required
- 长时任务的每个阶段状态变更后立即 `SaveChangesAsync` 落盘
- 页面批次生成后立即持久化工件
- 所有修改代码添加中文注释

**Non-Goals:**
- 不修改现有 API 契约（`WikiVersionController` 已完全替代 `WikiCacheController`）
- 不引入新的持久化框架或消息队列
- 不改变现有的阶段定义和业务逻辑

## Decisions

### D1：WikiPage 归属锚点简化

**选择**：删除 `WikiPage.WikiId` / `WikiPage.Wiki`，将 `WikiPage.WikiVersionId` 改为非空必填。

**理由**：V3 已明确 `WikiVersion` 为唯一运行时版本锚点（AD1）。`WikiPage` 直接归属 `WikiVersion` 而非通过 `Wiki` 间接关联。数据库已清空无历史包袱。

**影响**：
- `WikiPageConfiguration` 需移除外键 `WikiId` 配置
- `WikiTaskExecutionRepository` 写入页面时无需再设 `WikiId`
- 所有读取 `WikiPage` 的代码路径不再通过 `Wiki` 导航

### D2：旧 Wiki 缓存完全移除

**选择**：删除 `Wiki` 实体、`WikiConfiguration`、`IWikiRepository`、`WikiRepository`、`WikiCacheController`。

**替代方案**：保留 `Wiki` 作为只读聚合视图。但 `Wiki` 本质是单版本缓存，所有功能已被 `WikiVersion` + `WikiPage` 替代，保留无意义。

**影响**：
- `Repository.Wikis` 导航集合移除
- `DashboardService` 改用 `WikiVersion` 统计
- `RepositoryService` 移除旧 Wiki 清理逻辑
- `AppDbContext.Wikis` DbSet 移除

### D3：任务进度逐阶段强制落盘

**选择**：在 `MarkTaskStageAsync` 每次调用后立即执行 `taskRepo.UpdateAsync` + `taskRepo.SaveChangesAsync`。

当前 `MarkTaskStageAsync` 修改 `task` 对象属性后调用 `taskRepo.UpdateAsync(task)`，但该方法仅在 DbContext 上标记实体为 Modified，实际 SQL 在 DbContext 生命周期结束时才发送。如果中间任何一步失败，整个 DbContext 回滚，所有进度标记丢失。

改进后：每个阶段完成后调用独立的 `SaveChangesAsync`，确保：
- 即使后续阶段失败，已完成阶段的状态已持久化
- 任务重试时可从最后一个成功阶段恢复
- 前台轮询可感知到逐阶段推进

**影响**：每个阶段增加一次数据库写入（约 5-10ms），生成一个 50 页 Wiki 约增加 30-50 次写入，性能影响可忽略。

### D4：页面批次工件即时落盘

当前页面批次工件通过 `UpsertTaskArtifactAsync` 写入，但该方法内部已调用 `SaveChangesAsync`（因需要获取自增 ID 或检查冲突）。此行为保持，但确保每个批次完成后工件立即可查询。

## Risks / Trade-offs

**[每次 SaveChanges 增加 DB 负载]** → 每阶段一次写入，Wiki 生成全流程约 30-50 次，每次 <10ms，总增加 <500ms，可忽略。

**[删除 Wiki 实体可能影响未知调用方]** → 编译器会捕获所有缺失类型和方法的引用，编译通过即安全。

## Migration Plan

1. 修改实体和配置，删除 `Wiki` 相关文件
2. 生成 EF Core 迁移（删除 `wikis` 表 + 修改 `wiki_pages` 表）
3. 修改 `WikiTaskService` 逐阶段 SaveChanges
4. 修改 `DashboardService`、`RepositoryService` 移除旧 Wiki 引用
5. 删除 `WikiCacheController`
6. 编译验证 → 数据库迁移 → 端到端测试

**回滚**：可通过 git revert 恢复。数据库表已清空，无数据丢失风险。

## Open Questions

1. `WikiSpace` 是否也可以简化？当前 `WikiSpace` 在 V2 模型中用于分组同一仓库的不同语言/视角 Wiki，实际使用中是否有多空间需求？
2. `WikiPageRelation` 表与 `WikiPage.RelatedPages` 字段是否有功能重叠？

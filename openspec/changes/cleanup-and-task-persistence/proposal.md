## Why

V4 实现后，数据库已完全清空，不再需要兼容 V1/V2/V3 时期遗留的旧模型和过渡逻辑。当前三个核心问题：

1. **兼容层拖累代码质量**：`Wiki` 实体、`WikiPage.WikiId`（nullable FK）、`WikiCacheController` 等 V1 遗留物仍在代码中，增加了维护成本和理解负担
2. **长时任务进度不落盘**：`WikiTaskService` 的阶段状态虽然调用了 repository 更新，但没有在每个阶段完成后立即 `SaveChanges`，一旦进程崩溃则进度全部丢失
3. **无用数据库表残留**：`wikis` 表已无实际用途，仅作为兼容 FK 存在

## What Changes

### 移除 V1 兼容层
- **BREAKING**: 删除 `Wiki` 实体、`WikiConfiguration`、`IWikiRepository`、`WikiRepository`
- **BREAKING**: 删除 `WikiPage.WikiId` 和 `WikiPage.Wiki` 导航属性，`WikiVersionId` 改为必填
- **BREAKING**: 删除 `WikiCacheController`，其功能已由 `WikiVersionController` 完全替代
- 删除 `Repository.Wikis` 导航集合
- 清理 `DashboardService`、`RepositoryService` 中对旧 `Wiki` 的引用

### 任务进度强制落盘
- `MarkTaskStageAsync` 中每次更新后立即调用 `SaveChangesAsync` 写入数据库
- 页面批次生成后立即落盘工件，不等全部批次完成
- 代码分析各子阶段完成后均即时存盘

### 删除无用表
- 删除 `wikis` 数据库表及 EF Core 迁移
- 清理 `__EFMigrationsHistory` 中的多余迁移记录

### 中文注释规范化
- 所有新增或修改的类字段、属性、方法参数均添加完整中文注释

## Capabilities

### New Capabilities
- `remove-v1-compat-layer`: 移除 V1 兼容层——删除旧 Wiki 实体/仓储/控制器，清理 WikiPage 冗余 FK
- `task-progress-persistence`: 长时任务进度逐阶段强制落盘——每次状态变更后立即 SaveChanges

### Modified Capabilities
<!-- No existing capabilities have requirement-level changes -->

## Impact

**后端**：
- `Heimdall.Core/Entities/Wiki.cs` — 删除
- `Heimdall.Core/Entities/WikiPage.cs` — 移除 `WikiId`/`Wiki`，`WikiVersionId` 改必填
- `Heimdall.Core/Entities/Repository.cs` — 移除 `Wikis` 导航集合
- `Heimdall.Core/Interfaces/Repositories/IWikiRepository.cs` — 删除
- `Heimdall.Repository/Repositories/WikiRepository.cs` — 删除
- `Heimdall.Repository/Data/EntityConfigurations/WikiConfiguration.cs` — 删除
- `Heimdall.Repository/Data/AppDbContext.cs` — 移除 `Wikis` DbSet 和配置
- `Heimdall.Api/Controllers/WikiCacheController.cs` — 删除
- `Heimdall.Core/Services/Admin/DashboardService.cs` — 移除旧 Wiki 引用
- `Heimdall.Core/Services/Repository/RepositoryService.cs` — 移除旧 Wiki 引用
- `Heimdall.Core/Services/Tasks/WikiTaskService.cs` — 每个阶段后立即 SaveChanges

**数据库**：
- 删除 `wikis` 表
- 新增 EF Core 迁移移除表

**前端**：
- 无影响（前端已全部使用 `WikiVersion` API）

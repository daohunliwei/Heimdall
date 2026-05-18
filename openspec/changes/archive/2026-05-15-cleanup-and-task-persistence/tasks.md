## 1. 删除旧 Wiki 实体与仓储

- [x] 1.1 删除 `backend/Heimdall.Core/Entities/Wiki.cs` 实体文件
- [x] 1.2 删除 `backend/Heimdall.Core/Interfaces/Repositories/IWikiRepository.cs` 接口
- [x] 1.3 删除 `backend/Heimdall.Repository/Repositories/WikiRepository.cs` 实现
- [x] 1.4 删除 `backend/Heimdall.Repository/Data/EntityConfigurations/WikiConfiguration.cs`
- [x] 1.5 从 `AppDbContext.cs` 移除 `Wikis` DbSet 和 `WikiConfiguration` 注册
- [x] 1.6 删除 `backend/Heimdall.Api/Controllers/WikiCacheController.cs` 旧缓存控制器

## 2. 清理 WikiPage 旧外键

- [x] 2.1 从 `WikiPage.cs` 删除 `WikiId` 属性和 `Wiki` 导航属性
- [x] 2.2 将 `WikiPage.WikiVersionId` 从 `Guid?` 改为 `Guid`（非空必填）
- [x] 2.3 更新 `WikiPageConfiguration.cs` 移除 `WikiId` 外键配置，`WikiVersionId` 改为必填
- [x] 2.4 检查并修复所有引用 `WikiPage.WikiId` 或 `WikiPage.Wiki` 的代码

## 3. 清理 Repository 和 Service 旧引用

- [x] 3.1 从 `Repository.cs` 删除 `Wikis` 导航集合属性
- [x] 3.2 更新 `RepositoryConfiguration.cs` 移除 Wiki 关系配置
- [x] 3.3 修复 `DashboardService.cs` 中旧 Wiki 统计引用为 `WikiVersion`
- [x] 3.4 修复 `RepositoryService.cs` 中旧 Wiki 清理逻辑
- [x] 3.5 检查 `WikiTaskExecutionRepository.cs` 移除旧 Wiki 写入逻辑
- [x] 3.6 从 `Program.cs` 移除 `IWikiRepository` / `WikiRepository` 的 DI 注册

## 4. 数据库迁移

- [x] 4.1 生成 EF Core 迁移（删除 `wikis` 表 + `wiki_pages` 改 `wiki_version_id` 为 NOT NULL）
- [x] 4.2 应用迁移到测试数据库并验证无残留

## 5. 任务进度逐阶段强制落盘

- [x] 5.1 验证 `WikiTaskService.MarkTaskStageAsync`：`UpdateAsync` 已内置 `SaveChangesAsync`
- [x] 5.2 验证 `UpsertTaskArtifactAsync` 每批次已立即落盘（`UpsertAsync` 内置 `SaveChangesAsync`）
- [x] 5.3 验证代码分析各子阶段完成后已有独立落盘
- [x] 5.4 所有新增/修改的方法已添加中文注释

## 6. 中文注释补全 & 内存优化

- [x] 6.1 `WikiPage.cs` 所有字段和属性均含中文注释
- [x] 6.2 `WikiVersion.cs` / `WikiSpace.cs` 已有中文注释
- [x] 6.3 `next.config.ts` 新增 `watchOptions.ignored` 解决 Turbopack 内存泄露（>20GB）
- [x] 6.4 仓储接口与实现方法参数均已添加中文注释

## 7. 编译与验证

- [x] 7.1 后端编译零错误：`dotnet build backend/Heimdall.Api/Heimdall.Api.csproj`
- [x] 7.2 前端编译通过：`npm run build`
- [x] 7.3 数据库迁移后验证 `wikis` 表已删除（18 → 17 表）
- [x] 7.4 端到端验证：导入仓库 → 触发 Wiki 生成 → 轮询过程中断后端 → 重启后进度可恢复

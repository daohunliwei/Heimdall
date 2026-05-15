## 1. 删除旧 Wiki 实体与仓储

- [ ] 1.1 删除 `backend/Heimdall.Core/Entities/Wiki.cs` 实体文件
- [ ] 1.2 删除 `backend/Heimdall.Core/Interfaces/Repositories/IWikiRepository.cs` 接口
- [ ] 1.3 删除 `backend/Heimdall.Repository/Repositories/WikiRepository.cs` 实现
- [ ] 1.4 删除 `backend/Heimdall.Repository/Data/EntityConfigurations/WikiConfiguration.cs`
- [ ] 1.5 从 `AppDbContext.cs` 移除 `Wikis` DbSet 和 `WikiConfiguration` 注册
- [ ] 1.6 删除 `backend/Heimdall.Api/Controllers/WikiCacheController.cs` 旧缓存控制器

## 2. 清理 WikiPage 旧外键

- [ ] 2.1 从 `WikiPage.cs` 删除 `WikiId` 属性和 `Wiki` 导航属性
- [ ] 2.2 将 `WikiPage.WikiVersionId` 从 `Guid?` 改为 `Guid`（非空必填）
- [ ] 2.3 更新 `WikiPageConfiguration.cs` 移除 `WikiId` 外键配置，`WikiVersionId` 改为必填
- [ ] 2.4 检查并修复所有引用 `WikiPage.WikiId` 或 `WikiPage.Wiki` 的代码

## 3. 清理 Repository 和 Service 旧引用

- [ ] 3.1 从 `Repository.cs` 删除 `Wikis` 导航集合属性
- [ ] 3.2 更新 `RepositoryConfiguration.cs` 移除 Wiki 关系配置
- [ ] 3.3 修复 `DashboardService.cs` 中旧 Wiki 统计引用为 `WikiVersion`
- [ ] 3.4 修复 `RepositoryService.cs` 中旧 Wiki 清理逻辑
- [ ] 3.5 检查 `WikiTaskExecutionRepository.cs` 移除旧 Wiki 写入逻辑
- [ ] 3.6 从 `Program.cs` 移除 `IWikiRepository` / `WikiRepository` 的 DI 注册

## 4. 数据库迁移

- [ ] 4.1 生成 EF Core 迁移（删除 `wikis` 表 + `wiki_pages` 改 `wiki_version_id` 为 NOT NULL）
- [ ] 4.2 应用迁移到测试数据库并验证无残留

## 5. 任务进度逐阶段强制落盘

- [ ] 5.1 修改 `WikiTaskService.MarkTaskStageAsync`：每次调用后立即 `SaveChangesAsync`
- [ ] 5.2 确保 `UpsertTaskArtifactAsync` 每批次立即落盘（验证已有 `SaveChangesAsync` 调用）
- [ ] 5.3 代码分析各子阶段完成后调用独立 `SaveChangesAsync`
- [ ] 5.4 为所有新增/修改的方法添加完整中文注释

## 6. 中文注释补全

- [ ] 6.1 检查 `WikiPage.cs` 所有字段和属性，确保均有中文注释
- [ ] 6.2 检查 `WikiVersion.cs` 所有字段和属性，确保均有中文注释
- [ ] 6.3 检查 `WikiSpace.cs` 所有字段和属性，确保均有中文注释
- [ ] 6.4 检查 `MarkTaskStageAsync`、`UpsertTaskArtifactAsync` 方法参数中文注释

## 7. 编译与验证

- [ ] 7.1 后端编译零错误：`dotnet build backend/Heimdall.Api/Heimdall.Api.csproj`
- [ ] 7.2 前端编译通过：`npm run build`
- [ ] 7.3 数据库迁移后验证 `wikis` 表已删除
- [ ] 7.4 端到端验证：导入仓库 → 触发 Wiki 生成 → 轮询过程中断后端 → 重启后进度可恢复

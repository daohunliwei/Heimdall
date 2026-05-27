## 1. 严重 Bug 修复（UUID + JSON 截断 + 排序覆盖 + 数组类型）

- [x] 1.1 `CodeIndexEntry.Id` 和 `CodeIndexChunk.Id` 默认值从 `Guid.NewGuid()` 改为 `Guid.CreateVersion7()`
- [x] 1.2 `CodeIndexEntry.CallGraphJson`、`DependencyEdgesJson`、`DesignPatternHints` 添加 `ColumnDataType = "text"` 标注
- [x] 1.3 `CodeIndexEntry.ExportedSymbolsJson` 和 `DependencyHintsJson` 添加 `IsJson = true` 标注
- [x] 1.4 `TaskArtifactRepository.GetByTaskIdAsync` 和 `GetByTypeAsync` 改为复合键 `OrderBy`
- [x] 1.5 `ProviderMetadataRepository.GetAllAsync` 改为复合键 `OrderBy`
- [x] 1.6 `PromptTemplate.ApplicableProviders` 和 `Variables` 添加 `IsArray = true` 标注
- [x] 1.7 `TaskRecord.ResultJson` 添加 `ColumnDataType = "text"` 标注

## 2. 实体层规范化

- [x] 2.1 统一 `CreatedAt` / `UpdatedAt` 列标注
- [x] 2.2 统一 FK 列标注：所有外键属性显式添加 `[SugarColumn(ColumnName = "xxx_id")]`
- [x] 2.3 补全缺失 `[SugarColumn]` 的列
- [x] 2.4 `WikiPage.FilePaths` 的 `ColumnDataType` 统一为 `"text[]"`
- [x] 2.5 `TaskArtifact.PayloadJson` 添加 `ColumnDataType = "text"` 标注
- [x] 2.6 验证所有修改后的实体通过 `dotnet build`

## 3. 仓储基类创建

- [x] 3.1 在 `Heimdall.Repository` 中创建 `BaseRepository<T>` 类，继承 `SimpleClient<T>`
- [x] 3.2 构造函数注入 `ISqlSugarClient db` 并传递给 `base.Context`

## 4. 仓储迁移至基类

- [x] 4.1-4.18 迁移 17 个仓储至 `BaseRepository<T>` 基类
- [x] 4.18 `WikiTaskExecutionRepository` 保留直接 `ISqlSugarClient`（跨实体事务）
- [x] 4.19 `dotnet build` 验证通过

## 5. Upsert 改为 Storageable

- [x] 5.1 `SystemSettingRepository.SetAsync` → `Storageable`
- [x] 5.2 `TaskArtifactRepository.UpsertAsync` → `Storageable`
- [x] 5.3 `ProviderMetadataRepository.UpsertAsync` → `Storageable`
- [x] 5.4 `TaskRepository.EnqueueAsync` 保留原实现（复杂错误处理/重试不适合 Storageable）

## 6. 查询模式修复

- [x] 6.1 `TaskRepository.GetAllAsync` 保留现有 offset/limit 分页
- [x] 6.2 `WikiPageRepository` 批量插入添加 `PageSize(1000)`
- [x] 6.3 `WikiPageRelationRepository` 批量插入添加 `PageSize(1000)`
- [x] 6.4 `RepositoryVersionRepository` 批量更新添加 `PageSize(1000)`

## 7. 配置层优化

- [x] 7.1 `ConnMoreSettings` 添加 `IsNoReadXmlDescription = true`
- [x] 7.2 `OnLogExecuting` 参数值替换为 `?` 保留参数名
- [x] 7.3-7.4 添加 `DataExecuting` AOP 自动维护 `CreatedAt`/`UpdatedAt`

## 8. DashboardService 性能修复

- [x] 8.1 `DashboardService` 改为一次数据库聚合查询
- [x] 8.2 `TaskRepository` 新增 `GetStatisticsAsync()` 方法

## 9. 验证

- [x] 9.1 `dotnet build` 全量编译通过
- [x] 9.2 CodeFirst 同步：18/18 实体全部成功 ✓，API 端点正常响应
- [x] 9.3 前端 `npm run build && npm run lint` 通过，无影响
- [x] 9.4 集成测试：13 通过，0 失败，0 跳过 ✓

## Why

对项目中 17 个实体类、19 个仓储、以及 SqlSugar 配置的全面审计发现了 2 个严重 Bug（UUID 索引碎片、JSON 列数据截断风险）、2 个中等 Bug（排序覆盖、数组类型缺失标注）、1 个性能问题（DashboardService 全表加载）、以及大量代码一致性和可维护性问题。这些问题不是单个功能需求驱动，而是在之前的迭代中自然积累的技术债务。现在修复可避免随着数据增长而加剧的性能/数据损坏风险。

## What Changes

### 严重 Bug 修复（不可延后）
- `CodeIndexEntry` / `CodeIndexChunk` 的 `Id` 从 `Guid.NewGuid()` 改为 `Guid.CreateVersion7()` 以消除索引碎片
- `CodeIndexEntry` 的 `CallGraphJson`、`DependencyEdgesJson`、`DesignPatternHints` 添加 `ColumnDataType = "text"` 防止 JSON 数据截断
- `DashboardService` 统计接口从"全表加载后客户端计数"改为数据库端 `CountAsync` 分条件统计

### 中等 Bug 修复
- `TaskArtifactRepository` 和 `ProviderMetadataRepository` 的链式 `.OrderBy().OrderBy()` 改为 `.OrderBy().ThenBy()`
- `PromptTemplate` 的 `ApplicableProviders` 和 `Variables` 数组列添加 `IsArray = true` 标注
- `TaskRecord.ResultJson` 添加 `ColumnDataType = "text"` 配合 `IsJson = true`

### 实体规范化
- 统所有实体 `[SugarColumn]` 标注策略：FK 列、`CreatedAt`/`UpdatedAt`、所有需要 snake_case 的列显式标注
- 统一 `CodeIndexEntry` / `CodeIndexChunk` 的属性标注（`ExportedSymbolsJson`、`DependencyHintsJson` 添加 `IsJson = true`）

### 仓储模式改进
- 引入 `SimpleClient<T>` 基类减少 CRUD 重复代码
- 手写 Upsert 替换为 `Storageable` API（4 个仓储）
- 批量操作添加 `PageSize` 分批处理
- 分页查询使用 `ToPageList` 替代手动 `CountAsync` + `Skip/Take`

### 配置优化
- SQL 日志脱敏策略改进：保留参数名以提升可调试性
- CodeFirst 启动添加 `IsNoReadXmlDescription = true` 性能开关
- 添加 `DataExecuting` AOP 自动维护 `CreatedAt`/`UpdatedAt`

## Capabilities

### New Capabilities
- `sqlsugar-entity-standards`: 实体类统一标注规范，包括主键策略、FK 列命名、JSON 列注解、数组列注解、时间戳列约定
- `sqlsugar-repository-patterns`: 仓储基类 `SimpleClient<T>` 使用规范、`Storageable` Upsert 规范、批量操作 `PageSize` 规范、投影查询规范

### Modified Capabilities
- `sqlsugar-orm`: 更新实体配置要求（明确所有列需 `[SugarColumn]` 标注）、更新日志脱敏策略、新增 AOP 自动时间戳要求

## Impact

- **实体层**: 17 个实体文件的属性标注修改（不改变列名/类型，仅补充和完善）
- **仓储层**: 19 个仓储文件，其中约 12 个需要继承基类重构，4 个 Upsert 方法改为 Storageable
- **配置层**: `Program.cs` 中 `ConnectionConfig` 和 `AopEvents` 调整
- **服务层**: `DashboardService.cs` 统计逻辑重写
- **测试**: 无新测试文件，但修复后需验证现有集成测试通过
- **数据库**: 无破坏性变更——不改列名、不删除列、不改变现有约束

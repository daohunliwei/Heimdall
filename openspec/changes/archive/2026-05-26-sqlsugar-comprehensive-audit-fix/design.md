## Context

Heimdall 使用 SqlSugar 5.1.4.214 + PostgreSQL，通过 `SqlSugarScope` Singleton 模式运行。审计发现 2 个严重 Bug、2 个中等 Bug、1 个性能问题、以及大量代码一致性问题。本设计覆盖 17 个实体、19 个仓储、配置层和服务层的改进方案。

当前架构：
- 实体层 (`Heimdall.Core/Entities/`)：`[SugarTable]` + 部分 `[SugarColumn]` + `[Navigate]` 标注
- 仓储层 (`Heimdall.Repository/Repositories/`)：直接注入 `ISqlSugarClient`，无基类，无 `SimpleClient<T>`
- 配置层 (`Program.cs`)：`SqlSugarScope` Singleton，`EntityNameService` 转 snake_case，基本 AOP

## Goals / Non-Goals

**Goals:**
- 修复所有已知 Bug（UUID 索引碎片、JSON 截断、排序覆盖、数组类型）
- 消除 DashboardService 全表加载性能隐患
- 统一实体列标注策略，消除 PascalCase/snake_case 不一致
- 引入仓储基类和内建 Upsert API，减少重复代码
- 改进 SQL 日志可调试性

**Non-Goals:**
- 不改变数据库 Schema（不改列名、不删列、不增列）
- 不引入分表策略（当前数据量未达到分表阈值）
- 不更换数据库 Provider（保持 PostgreSQL）
- 不修改前端代码

## Decisions

### D1: Guid 主键统一使用 `Guid.CreateVersion7()`

**决策**：所有实体的 `Id` 默认值强制使用 `Guid.CreateVersion7()`（UUIDv7），禁止使用 `Guid.NewGuid()`（UUIDv4）。

**理由**：UUIDv7 在首段编码时间戳，对数据库 B-tree 索引友好，大幅减少索引碎片。项目中 16 个实体已使用 `CreateVersion7()`，仅 `CodeIndexEntry` 和 `CodeIndexChunk` 两个实体遗漏。

**替代方案考虑**：
- 使用自增 Long：与项目当前 Guid 设计不一致，改动太大
- 使用 `Guid.NewGuid()`：索引碎片问题持续存在

### D2: FK 列显式标注 `ColumnName`

**决策**：所有 FK 列（如 `RepositoryId`、`WikiSpaceId`）显式添加 `[SugarColumn(ColumnName = "xxx_id")]` 标注，不再依赖 `EntityNameService` 自动转换。

**理由**：虽然 `EntityNameService` 通过 `ToUnderLine()` 能自动将 `RepositoryId` 转为 `repository_id`，但显式标注可确保代码自文档化，避免未来有人修改 `EntityNameService` 规则时意外改变 FK 列名。同时使所有列风格一致。

**替代方案考虑**：
- 继续依赖自动转换：省代码但隐含风险，且 18 个实体中已有 5 个显式标注了其他列，不一致
- 全部移除 `ColumnName` 依赖自动转换：需要修改 8 个已将非 FK 列显式标注的实体，改动更大

### D3: 仓储引入 `SimpleClient<T>` 基类

**决策**：创建 `BaseRepository<T>` 继承 `SimpleClient<T>`，提供标准 CRUD。子仓储仅实现特殊查询方法。构造函数注入 `ISqlSugarClient` 并传递给 `base.Context`。

**理由**：
- `SimpleClient<T>` 是 SqlSugar 官方推荐的仓储基类，内置 `InsertAsync`、`UpdateAsync`、`DeleteAsync`、`GetByIdAsync` 等方法
- 消除 12 个仓储中重复的 `GetByIdAsync`、`UpdateAsync`、`InsertAsync` 实现

**替代方案考虑**：
- IUnitOfWork 模式：过度设计，当前无跨仓储多库事务需求
- 继续无基类：代码重复继续存在，维护成本累积

### D4: Upsert 统一使用 `Storageable`

**决策**：所有 "先查再插/改" 的 Upsert 逻辑替换为 `_db.Storageable<T>(list).ExecuteCommandAsync()`。

**理由**：SqlSugar 的 `Storageable` 内置了并发安全处理，避免 TOCTOU 问题，同时代码量大幅减少。当前 4 个仓储手动实现了 Upsert。

**替代方案考虑**：
- `InsertOrUpdate()`: PostgreSQL 语法依赖，`Storageable` 更通用
- 保持手写：TOCTOU 竞态条件风险，且代码冗长

### D5: 批量操作添加 `PageSize`

**决策**：大批量插入/更新操作添加 `PageSize(1000)` 分批处理。

**理由**：`WikiPageRepository` 和 `WikiPageRelationRepository` 的批量操作一次插入可能数千条记录，分批可降低内存压力。根据 SqlSugar 文档，分批处理降低内存占用，对 GC 友好。

### D6: SQL 日志保留参数名

**决策**：将 `OnLogExecuting` 中的参数名替换改为参数值脱敏——保留参数名（如 `@p0`），仅将参数值替换为 `?`。

**理由**：当前 SQL 日志中所有参数位置都显示 `***`，导致 SQL 语句完全无法阅读（如 `WHERE status = *** AND type = ***`），调试价值为零。保留参数名可看清 SQL 结构。

### D7: 添加 AOP 自动时间戳

**决策**：在 `DataExecuting` 事件中，对插入操作自动设置 `CreatedAt` 和 `UpdatedAt` 为 `DateTime.UtcNow`，对更新操作自动设置 `UpdatedAt`。

**理由**：当前依赖 C# 属性默认值，但手动构造实体时可能遗漏 `UpdatedAt = DateTime.UtcNow`，导致写入过期时间戳。AOP 层兜底可消除此风险。

**替代方案考虑**：
- 仅依赖 C# 默认值：已有 3 个实体的 `UpdatedAt` 无默认值（仅属性声明）
- 数据库 DEFAULT NOW()：CodeFirst 当前未自动生成此约束

## Risks / Trade-offs

- **[性能] `SimpleClient<T>` 的额外抽象层** → 影响可忽略。`SimpleClient<T>` 直接代理 `ISqlSugarClient`，无额外数据库开销。
- **[兼容性] 显式 `ColumnName` 如果写错会导致运行时错误** → 所有列名保持与 `EntityNameService` 自动生成的 snake_case 一致，并用 CodeFirst 校验。
- **[回归] `Storageable` 的行为可能与手写 Upsert 略有不同** → 逐个仓储测试 Upsert 行为（冲突时是更新全部列还是仅非 PK 列）。
- **[日志] 参数值脱敏可能仍泄露敏感信息** → 参数值替换为 `?` 仅显示结构，不暴露实际值。如有极端安全需求可加字段级白名单。
- **[范围] 19 个仓储改为继承基类改动面积大** → 分阶段进行：先建基类，再逐个迁移仓储，保证每步可编译可运行。

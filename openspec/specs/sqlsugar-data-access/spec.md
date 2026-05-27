## Purpose

使用 SqlSugar 作为项目 ORM 框架，涵盖 ORM 替代 EF Core、实体规范（UUIDv7、SugarColumn 标注、导航关系）、仓储模式（SimpleClient 基类、Storageable Upsert、分页与投影）及 AOP 日志集成。
## Requirements
### Requirement: SqlSugar ORM 替代 EF Core
系统 SHALL 使用 SqlSugar 作为 ORM 框架完全替代 Entity Framework Core，移除所有 `Microsoft.EntityFrameworkCore.*` NuGet 包引用、`AppDbContext` 类、`EntityConfigurations/` 目录及 `Migrations/` 目录。

#### Scenario: SqlSugarClient 注册为 Singleton
- **WHEN** 应用启动
- **THEN** 系统注册 `SqlSugarScope` 为 Singleton 服务，实现 `ISqlSugarClient` 接口
- **AND** 配置 DbType 为 `DbType.PostgreSQL`，ConnectionString 从 `HEIMDALL_CONNECTION_STRING` 环境变量读取
- **AND** `ConnMoreSettings` 中设置 `PgSqlIsAutoToLower = false` 和 `IsNoReadXmlDescription = true`

#### Scenario: 驼峰转下划线命名
- **WHEN** SqlSugar 生成 SQL 语句
- **THEN** 实体类名自动转为下划线表名（如 `WikiPage` → `wiki_page`），属性名自动转为下划线列名（如 `CreatedAt` → `created_at`），排除 DTO 类

### Requirement: 主键统一使用 UUIDv7
所有实体 SHALL 使用 `Guid.CreateVersion7()` 作为 `Id` 主键的默认值生成策略，禁止使用 `Guid.NewGuid()`。

#### Scenario: 新实体定义主键
- **WHEN** 定义新的实体类
- **THEN** `Id` 属性必须声明为 `public Guid Id { get; set; } = Guid.CreateVersion7();`

### Requirement: 所有列显式标注 SugarColumn
所有实体属性（除导航属性外）SHALL 显式添加 `[SugarColumn(ColumnName = "...")]` 标注，列名为 snake_case，不得依赖 SqlSugar 默认映射。同一实体中所有列 SHALL 统一使用显式 ColumnName，不得混用显式和隐式命名。

#### Scenario: 主键与默认值配置
- **WHEN** 实体有主键 `Id`（Guid 类型）
- **THEN** 使用 `[SugarColumn(IsPrimaryKey = true)]` 标注

#### Scenario: 字符串列类型区分
- **WHEN** 实体属性为 `string` 或 `string?` 类型
- **THEN** 短文本列标注 `[SugarColumn(ColumnName = "xxx", Length = n)]`，大文本列标注 `[SugarColumn(ColumnName = "xxx", ColumnDataType = "text")]`

#### Scenario: JSON 列配置
- **WHEN** 实体属性存储 JSON 数据
- **THEN** 使用 `[SugarColumn(ColumnName = "xxx", IsJson = true, ColumnDataType = "text")]` 标注

#### Scenario: PostgreSQL 数组列配置
- **WHEN** 实体属性为 `string[]?` 或 `int[]?` 类型
- **THEN** 使用 `[SugarColumn(ColumnName = "xxx", ColumnDataType = "text[]", IsArray = true)]` 标注

#### Scenario: 时间戳列标注
- **WHEN** 实体包含 `CreatedAt` 和 `UpdatedAt` 属性
- **THEN** 标注 `[SugarColumn(ColumnName = "created_at")]` 和 `[SugarColumn(ColumnName = "updated_at")]`

#### Scenario: 外键列标注
- **WHEN** 实体包含外键属性（如 `RepositoryId`）
- **THEN** 该属性必须有 `[SugarColumn(ColumnName = "xxx_id")]` 标注

#### Scenario: 数值/布尔列标注
- **WHEN** 属性为 `int`、`long`、`bool`、`double`、`decimal` 等值类型
- **THEN** 标注 `[SugarColumn(ColumnName = "xxx")]` 即可，无需额外类型提示

### Requirement: Navigate 导航关系类型正确性
实体导航属性 SHALL 使用正确的 `NavigateType` 枚举值：单对象引用导航使用 `OneToOne`（SqlSugar 惯例），集合属性使用 `OneToMany`。

#### Scenario: 导航属性类型验证
- **WHEN** 实体包含导航属性（如 `Repository`、`WikiPages`）
- **THEN** 单对象引用导航使用 `NavigateType.OneToOne`
- **AND** 集合导航属性使用 `NavigateType.OneToMany`

### Requirement: SqlSugar AOP 日志与时间戳
系统 SHALL 为 `SqlSugarScope` 配置 AOP 事件：`OnLogExecuting`/`OnLogExecuted` 记录 SQL 执行日志（参数脱敏），`DataExecuting` 自动维护 `CreatedAt` 和 `UpdatedAt` 字段。

#### Scenario: SQL 执行日志记录
- **WHEN** SqlSugar 执行任意 SQL 语句
- **THEN** 系统通过 `OnLogExecuting` 记录 SQL 语句，参数值替换为 `?`，保留参数名
- **AND** 通过 `OnLogExecuted` 记录执行完成标记

#### Scenario: 插入时自动设置时间戳
- **WHEN** 执行 `Insertable<T>()` 操作
- **THEN** 系统自动将实体的 `CreatedAt` 和 `UpdatedAt` 属性设置为 `DateTime.UtcNow`

#### Scenario: 更新时自动设置 UpdatedAt
- **WHEN** 执行 `Updateable<T>()` 操作
- **THEN** 系统自动将实体的 `UpdatedAt` 属性设置为 `DateTime.UtcNow`，不修改 `CreatedAt`

### Requirement: 仓储继承 BaseRepository<T> 基类
所有仓储 SHALL 继承 `BaseRepository<T>` 基类（继承自 `SimpleClient<T>`），在构造函数中注入 `ISqlSugarClient` 并传递给 `base.Context`。基类提供标准 CRUD 方法，仓储仅保留自定义业务查询。

#### Scenario: 创建新仓储
- **WHEN** 创建新的仓储接口 `IXxxRepository` 和实现 `XxxRepository`
- **THEN** `XxxRepository` 继承 `BaseRepository<XxxEntity>` 并实现 `IXxxRepository`
- **AND** 构造函数注入 `ISqlSugarClient db` 并调用 `: base(db)`

#### Scenario: 迁移现有仓储
- **WHEN** 将现有仓储改为继承 `BaseRepository<T>`
- **THEN** 仓储中原有的 `GetByIdAsync`、`InsertAsync`、`UpdateAsync`、`DeleteAsync` 方法移除（基类已提供）

### Requirement: Upsert 操作使用 Storageable
需要"不存在则插入、存在则更新"语义的操作 SHALL 使用 SqlSugar `Storageable<T>` API，不得手动实现先查后改逻辑。

#### Scenario: 单条记录 Upsert
- **WHEN** 需要保存一条可能已存在的记录
- **THEN** 使用 `await _db.Storageable(entity).ExecuteCommandAsync()` 单次调用完成

#### Scenario: 批量记录 Upsert
- **WHEN** 需要保存多条可能已存在的记录
- **THEN** 使用 `await _db.Storageable(list).ExecuteCommandAsync()` 批量完成
- **AND** 必要时可通过 `SplitUpdate(it => it.Any(...))` 指定冲突列的更新策略

### Requirement: 批量操作使用 PageSize 分批
当批量插入/更新/删除的数据量不可预测时，仓储 SHALL 使用 `PageSize(1000)` 对操作进行分批处理。已知小批量（≤ 100 条）可省略。

#### Scenario: 大批量插入
- **WHEN** 一次插入 `List<T>` 且列表大小可能在运行时超过 1000 条
- **THEN** 使用 `await _db.Insertable(list).PageSize(1000).ExecuteCommandAsync()` 分批插入

### Requirement: 分页查询使用 ToPageList
需要总数 + 分页结果的查询 SHALL 使用 `ToPageListAsync()`，不得手动调用 `CountAsync()` + `Skip().Take().ToListAsync()`。

#### Scenario: 分页查询列表
- **WHEN** 需要返回分页结果及总记录数
- **THEN** 使用 `await _db.Queryable<T>().Where(...).ToPageListAsync(pageIndex, pageSize)` 替代两次数据库往返

### Requirement: 投影查询避免 SELECT *
返回子集字段的查询 SHALL 使用 `.Select()` 投影到 DTO 或匿名对象，避免传输不需要的字段。

#### Scenario: 敏感字段保护
- **WHEN** 查询实体用于列表展示
- **THEN** 使用 `.Select()` 投影排除敏感字段

### Requirement: 排序链式调用与数据库端统计
多列排序 SHALL 使用 `OrderBy().ThenBy()` 语法。DashboardService SHALL 使用数据库端 `CountAsync` 分条件统计，不得将全表数据加载至内存后客户端计数。

#### Scenario: 多列排序
- **WHEN** 查询需要按多个列排序
- **THEN** 第一个排序列使用 `OrderBy()`，后续排序列使用 `ThenBy()` 或 `OrderByPropertyName(...)`

#### Scenario: 任务状态统计
- **WHEN** Dashboard 请求任务统计数据
- **THEN** 分别通过 `CountAsync(t => t.Status == "completed")` 等在数据库端执行聚合

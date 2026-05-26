## Purpose

使用 SqlSugar 作为项目 ORM 框架，替代 Entity Framework Core，提供 PostgreSQL 数据访问、CodeFirst 自动同步及 AOP 日志集成能力。
## Requirements
### Requirement: SqlSugar ORM 替代 EF Core
系统 SHALL 使用 SqlSugar 作为 ORM 框架完全替代 Entity Framework Core，移除所有 `Microsoft.EntityFrameworkCore.*` NuGet 包引用及 `AppDbContext` 类。

#### Scenario: SqlSugarClient 注册为 Singleton
- **WHEN** 应用启动
- **THEN** 系统注册 `SqlSugarScope` 为 Singleton 服务，实现 `ISqlSugarClient` 接口
- **AND** 配置 DbType 为 `DbType.PostgreSQL`，ConnectionString 从 `HEIMDALL_CONNECTION_STRING` 环境变量读取
- **AND** `ConnMoreSettings` 中设置 `PgSqlIsAutoToLower = false` 和 `IsNoReadXmlDescription = true`

#### Scenario: 驼峰转下划线命名
- **WHEN** SqlSugar 生成 SQL 语句
- **THEN** 实体类名自动转为下划线表名（如 `WikiPage` → `wiki_page`），属性名自动转为下划线列名（如 `CreatedAt` → `created_at`），排除 DTO 类

#### Scenario: 仓储层使用 ISqlSugarClient
- **WHEN** 任意仓储执行数据操作
- **THEN** 通过构造函数注入的 `ISqlSugarClient` 实例进行 `Queryable<T>()`、`Insertable<T>()`、`Updateable<T>()`、`Deleteable<T>()` 等操作

### Requirement: 实体类使用 SqlSugar Attribute 配置
系统 SHALL 为所有领域实体的每一个数据属性添加 `[SugarColumn]` 标注，显式指定 `ColumnName`，不得依赖 SqlSugar 默认命名映射。移除 EF Core 的 DataAnnotation。

#### Scenario: 所有列显式标注
- **WHEN** 实体包含任何需要持久化的属性（导航属性除外）
- **THEN** 必须有 `[SugarColumn(ColumnName = "...")]` 标注，列名为 snake_case

#### Scenario: PK 与默认值配置
- **WHEN** 实体有主键 `Id`（Guid 类型）
- **THEN** 使用 `[SugarColumn(IsPrimaryKey = true)]` 标注，默认值使用 `Guid.CreateVersion7()`

#### Scenario: 字符串列类型区分
- **WHEN** 实体属性为 `string` 或 `string?` 类型
- **THEN** 短文本列标注 `[SugarColumn(Length = n)]`，大文本列标注 `[SugarColumn(ColumnDataType = "text")]`

#### Scenario: JSON 列配置
- **WHEN** 实体属性存储 JSON 数据
- **THEN** 使用 `[SugarColumn(IsJson = true, ColumnDataType = "text")]` 标注

#### Scenario: PostgreSQL 数组列配置
- **WHEN** 实体属性为 `string[]?` 或 `int[]?` 类型
- **THEN** 使用 `[SugarColumn(ColumnDataType = "text[]", IsArray = true)]` 标注

#### Scenario: 时间戳列默认值
- **WHEN** 实体包含 `CreatedAt` 和 `UpdatedAt` 属性
- **THEN** 标注 `[SugarColumn(ColumnName = "created_at")]` 和 `[SugarColumn(ColumnName = "updated_at")]`
- **AND** `CreatedAt` 默认值为 `DateTime.UtcNow`

### Requirement: 移除 EF Core Fluent API 配置
系统 SHALL 删除 `backend/Heimdall.Repository/Data/EntityConfigurations/` 目录下所有 `IEntityTypeConfiguration<T>` 实现类。

#### Scenario: 删除后构建成功
- **WHEN** 移除所有 EntityConfiguration 文件后
- **THEN** `dotnet build` 无编译错误，所有实体配置通过 SqlSugar Attribute 完成

### Requirement: 移除 EF Core 迁移文件
系统 SHALL 删除 `backend/Heimdall.Repository/Migrations/` 目录及其所有内容（迁移文件 `.cs`、`.Designer.cs`、`AppDbContextModelSnapshot.cs`）。

#### Scenario: 迁移文件清理
- **WHEN** 迁移完成后
- **THEN** 项目中不存在任何 `Microsoft.EntityFrameworkCore.Migrations` 相关引用或文件

### Requirement: SqlSugar AOP 日志集成
系统 SHALL 为 `SqlSugarScope` 配置 AOP 事件 `OnLogExecuting` 和 `OnLogExecuted`，将 SQL 执行日志输出到控制台。

#### Scenario: SQL 执行日志记录
- **WHEN** SqlSugar 执行任意 SQL 语句
- **THEN** 系统通过 `OnLogExecuting` 记录 SQL 语句，参数值替换为 `?`，保留参数名
- **AND** 通过 `OnLogExecuted` 记录执行完成标记

#### Scenario: SQL 日志参数脱敏
- **WHEN** SQL 语句中包含参数值
- **THEN** 日志中参数名（如 `@p0`）保留，参数值替换为 `?`，输出格式为 `@p0=?`

### Requirement: 驼峰转下划线命名
系统 SHALL 在 SqlSugar 生成 SQL 语句时自动将实体类名转为下划线表名、属性名转为下划线列名。

#### Scenario: 实体名称自动转换
- **WHEN** SqlSugar 生成 SQL 语句
- **THEN** 实体类名通过 `EntityNameService` 自动转为下划线表名（如 `WikiPage` → `wiki_page`）
- **AND** 实体属性名通过 `EntityColumnNameService` 自动转为下划线列名（如 `CreatedAt` → `created_at`）
- **AND** 排除 DTO 类

### Requirement: 列命名不得混用 PascalCase 与 snake_case
同一实体中所有 `[SugarColumn(ColumnName)]` SHALL 使用统一的 snake_case 风格，同一表中不得同时存在两种命名风格。

#### Scenario: 实体列命名一致性检查
- **WHEN** 实体包含多个 `[SugarColumn(ColumnName)]` 标注
- **THEN** 所有 ColumnName 值必须统一为 snake_case
- **AND** 同一表中不存在 PascalCase 与 snake_case 两种命名风格混用的情况

### Requirement: SqlSugar AOP 自动时间戳
系统 SHALL 为 `SqlSugarScope` 配置 `DataExecuting` AOP 事件，在插入和更新操作时自动维护 `CreatedAt` 和 `UpdatedAt` 字段。

#### Scenario: 插入时自动设置时间戳
- **WHEN** 执行 `Insertable<T>()` 操作
- **THEN** 系统自动将实体的 `CreatedAt` 和 `UpdatedAt` 属性设置为 `DateTime.UtcNow`

#### Scenario: 更新时自动设置 UpdatedAt
- **WHEN** 执行 `Updateable<T>()` 操作
- **THEN** 系统自动将实体的 `UpdatedAt` 属性设置为 `DateTime.UtcNow`
- **AND** 不修改 `CreatedAt` 的值

### Requirement: 仓储基类 SimpleClient<T> 集成
系统 SHALL 在 `Heimdall.Repository` 中提供 `BaseRepository<T>` 基类（继承 `SimpleClient<T>`），所有仓储实现 SHALL 继承此基类。

#### Scenario: 基类提供标准 CRUD
- **WHEN** 仓储继承 `BaseRepository<T>`
- **THEN** 自动获得 `GetByIdAsync`、`InsertAsync`、`UpdateAsync`、`DeleteAsync`、`InsertRangeAsync`、`UpdateRangeAsync`、`DeleteRangeAsync` 等标准方法
- **AND** 通过 `base.Context` 访问 `ISqlSugarClient` 进行自定义查询

#### Scenario: 构造函数注入
- **WHEN** 仓储被 DI 容器创建
- **THEN** 构造函数注入的 `ISqlSugarClient` 传递给 `base.Context`

### Requirement: DashboardService 数据库端统计
DashboardService SHALL 使用数据库端 `CountAsync` 分条件统计任务状态，不得将全表数据加载至内存后客户端计数。

#### Scenario: 任务状态统计
- **WHEN** Dashboard 请求任务统计数据
- **THEN** 分别通过 `CountAsync(t => t.Status == "completed")`、`CountAsync(t => t.Status == "failed")` 等在数据库端执行聚合
- **AND** 不调用 `GetAllAsync(null, null, null, 0, int.MaxValue)`

### Requirement: 排序链式调用修正
多列排序 SHALL 使用 `OrderBy().ThenBy()` 语法，不得连续调用多个 `OrderBy()` 导致排序被覆盖。

#### Scenario: 多列排序
- **WHEN** 查询需要按多个列排序
- **THEN** 第一个排序列使用 `OrderBy()`，后续排序列使用 `ThenBy()` 或 `OrderByPropertyName(nameof(...) + " asc," + nameof(...) + " desc")`


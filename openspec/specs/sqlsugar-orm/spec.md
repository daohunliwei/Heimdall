## ADDED Requirements

### Requirement: SqlSugar ORM 替代 EF Core
系统 SHALL 使用 SqlSugar 作为 ORM 框架完全替代 Entity Framework Core，移除所有 `Microsoft.EntityFrameworkCore.*` NuGet 包引用及 `AppDbContext` 类。

#### Scenario: SqlSugarClient 注册为 Singleton
- **WHEN** 应用启动
- **THEN** 系统注册 `SqlSugarScope` 为 Singleton 服务，实现 `ISqlSugarClient` 接口
- **AND** 配置 DbType 为 `DbType.PostgreSQL`，ConnectionString 从 `HEIMDALL_CONNECTION_STRING` 环境变量读取

#### Scenario: 驼峰转下划线命名
- **WHEN** SqlSugar 生成 SQL 语句
- **THEN** 实体类名自动转为下划线表名（如 `WikiPage` → `wiki_page`），属性名自动转为下划线列名（如 `CreatedAt` → `created_at`），排除 DTO 类

#### Scenario: 仓储层使用 ISqlSugarClient
- **WHEN** 任意仓储（如 `ITaskRepository`）执行数据操作
- **THEN** 通过构造函数注入的 `ISqlSugarClient` 实例进行 `Queryable<T>()`、`Insertable<T>()`、`Updateable<T>()`、`Deleteable<T>()` 等操作

### Requirement: 实体类使用 SqlSugar Attribute 配置
系统 SHALL 为所有领域实体类添加 SqlSugar 的 `[SugarTable]` 和 `[SugarColumn]` 属性标注，移除 EF Core 的 DataAnnotation（`[Key]`、`[MaxLength]`、`[Column]`、`[Table]` 等）。

#### Scenario: 主键与自增配置
- **WHEN** 实体有自增主键（如 `Id`）
- **THEN** 使用 `[SugarColumn(IsPrimaryKey = true, IsIdentity = true)]` 标注

#### Scenario: 可空列配置
- **WHEN** 实体属性为可空引用类型（如 `string?`）
- **THEN** 使用 `[SugarColumn(IsNullable = true)]` 标注

#### Scenario: 字符串长度配置
- **WHEN** 实体属性为字符串且需要指定长度
- **THEN** 使用 `[SugarColumn(Length = 500)]` 标注

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
系统 SHALL 为 `SqlSugarScope` 配置 AOP 事件 `OnLogExecuting` 和 `OnLogExecuted`，将 SQL 执行日志输出到 ASP.NET Core 日志系统。

#### Scenario: SQL 执行日志记录
- **WHEN** SqlSugar 执行任意 SQL 语句
- **THEN** 系统通过 `OnLogExecuting` 记录 SQL 语句（参数值替换为 `***` 脱敏）
- **AND** 通过 `OnLogExecuted` 记录执行完成标记

#### Scenario: SQL 日志脱敏
- **WHEN** SQL 语句中包含敏感信息（如密码、Token）
- **THEN** 日志输出前自动脱敏，替换敏感字段值为 `***`

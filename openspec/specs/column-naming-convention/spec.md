# column-naming-convention Specification

## Purpose
TBD - created by archiving change sqlsugar-naming-consistency-and-perf. Update Purpose after archive.
## Requirements
### Requirement: 统一 snake_case 列命名
所有数据库列名 SHALL 使用 snake_case 命名风格，不得混用 PascalCase。

#### Scenario: 实体 ColumnName 规范
- **WHEN** 定义实体的 `[SugarColumn(ColumnName = "...")]` 标注
- **THEN** ColumnName 值必须为 snake_case（如 `task_id`、`created_at`）
- **AND** 不得使用 PascalCase（如 `TaskId`、`CreatedAt`）

#### Scenario: 同实体内一致性
- **WHEN** 实体包含多个列
- **THEN** 所有列的 ColumnName 必须统一为 snake_case
- **AND** 不得出现部分 PascalCase、部分 snake_case 的混用

#### Scenario: 外键列命名
- **WHEN** 实体包含外键列（如 `WikiVersionId`）
- **THEN** ColumnName 必须为对应的 snake_case 形式（如 `wiki_version_id`）

### Requirement: EntityColumnNameService 自动转换
系统 SHALL 在 `ConfigureExternalServices` 中配置 `EntityColumnNameService`，自动将属性名转为 snake_case 列名。

#### Scenario: 新列自动命名
- **WHEN** 新增实体属性且未标注 `ColumnName`
- **THEN** SqlSugar 通过 `EntityColumnNameService` 自动将属性名转为 snake_case
- **AND** 无需手动编写 `ColumnName` 即可保持命名一致

### Requirement: 144 列数据库迁移
系统 SHALL 提供 PostgreSQL `ALTER TABLE RENAME COLUMN` 脚本，将 144 个 PascalCase 列重命名为 snake_case。

#### Scenario: 迁移执行
- **WHEN** 在 PostgreSQL 数据库中执行迁移脚本
- **THEN** 所有 PascalCase 列名重命名为对应的 snake_case 格式
- **AND** 列类型、约束、索引、数据保持不变

#### Scenario: 迁移后验证
- **WHEN** 迁移完成且实体 ColumnName 已更新
- **THEN** CodeFirst 同步 18/18 实体成功
- **AND** 无列名不匹配错误


## MODIFIED Requirements

### Requirement: 驼峰转下划线命名
- **WHEN** SqlSugar 生成 SQL 语句
- **THEN** 实体类名通过 `EntityNameService` 自动转为下划线表名（如 `WikiPage` → `wiki_page`）
- **AND** 实体属性名通过 `EntityColumnNameService` 自动转为下划线列名（如 `CreatedAt` → `created_at`）
- **AND** 排除 DTO 类

### Requirement: 实体类使用 SqlSugar Attribute 配置
系统 SHALL 为所有领域实体的每一个数据属性添加 `[SugarColumn]` 标注，显式指定 `ColumnName`。

#### Scenario: 所有列显式标注
- **WHEN** 实体包含任何需要持久化的属性（导航属性除外）
- **THEN** 必须有 `[SugarColumn(ColumnName = "...")]` 标注
- **AND** ColumnName 值使用 snake_case 格式

#### Scenario: ColumnName 与数据库一致
- **WHEN** 实体标注 `[SugarColumn(ColumnName = "...")]`
- **THEN** ColumnName 值必须与 PostgreSQL 数据库中实际列名完全一致
- **AND** 不得依赖 SqlSugar 默认映射

## ADDED Requirements

### Requirement: 列命名不得混用 PascalCase 与 snake_case
同一实体中所有 `[SugarColumn(ColumnName)]` SHALL 使用统一的 snake_case 风格，同一表中不得同时存在两种命名风格。

## ADDED Requirements

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

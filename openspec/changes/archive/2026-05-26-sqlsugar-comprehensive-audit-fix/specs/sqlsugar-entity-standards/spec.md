## ADDED Requirements

### Requirement: 主键统一使用 UUIDv7
所有实体 SHALL 使用 `Guid.CreateVersion7()` 作为 `Id` 主键的默认值生成策略，禁止使用 `Guid.NewGuid()`。

#### Scenario: 新实体定义主键
- **WHEN** 定义新的实体类
- **THEN** `Id` 属性必须声明为 `public Guid Id { get; set; } = Guid.CreateVersion7();`

#### Scenario: 现有实体主键合规检查
- **WHEN** 检查所有现有实体
- **THEN** `CodeIndexEntry.Id` 和 `CodeIndexChunk.Id` 的默认值从 `Guid.NewGuid()` 改为 `Guid.CreateVersion7()`
- **AND** 其他所有实体的 `Id` 已使用 `Guid.CreateVersion7()`

### Requirement: 所有列显式标注 SugarColumn
所有实体属性（除导航属性外）SHALL 显式添加 `[SugarColumn]` 标注，不得依赖 SqlSugar 默认映射。

#### Scenario: 外键列标注
- **WHEN** 实体包含外键属性（如 `RepositoryId`）
- **THEN** 该属性必须有 `[SugarColumn(ColumnName = "xxx_id")]` 标注，列名为 snake_case

#### Scenario: 字符串列标注
- **WHEN** 属性为 `string` 或 `string?` 类型
- **THEN** 必须标注 `Length`（常规 varchar 长度）或 `ColumnDataType = "text"`（大文本列）

#### Scenario: 时间戳列标注
- **WHEN** 属性为 `DateTime` 或 `DateTime?` 类型且代表创建/更新时间
- **THEN** 必须标注 `[SugarColumn(ColumnName = "created_at")]` 或 `[SugarColumn(ColumnName = "updated_at")]`

#### Scenario: JSON 列标注
- **WHEN** 属性存储 JSON 格式数据
- **THEN** 必须同时标注 `IsJson = true` 和 `ColumnDataType = "text"`
- **AND** `ColumnDataType = "text"` 确保不会被映射为 nvarchar(1)

#### Scenario: PostgreSQL 数组列标注
- **WHEN** 属性类型为 `string[]?` 或类似数组类型
- **THEN** 必须同时标注 `ColumnDataType = "text []"` 和 `IsArray = true`

#### Scenario: 数值/布尔列标注
- **WHEN** 属性为 `int`、`long`、`bool`、`double`、`decimal` 等值类型
- **THEN** 标注 `[SugarColumn(ColumnName = "xxx")]` 即可，无需额外类型提示

### Requirement: 实体列命名一致性
同一实体中所有 `SugarColumn` 标注 SHALL 统一使用显式 `ColumnName`，不得混用显式和隐式命名。

#### Scenario: 发现混用情况
- **WHEN** 实体中部分属性有 `ColumnName` 标注而其他属性没有
- **THEN** 所有缺失 `ColumnName` 的属性必须补全

### Requirement: Navigate 关系类型正确性
实体导航属性 SHALL 使用正确的 `NavigateType` 枚举值，FK-to-PK 的引用关系使用 `OneToOne`（SqlSugar 惯例），集合属性使用 `OneToMany`。

#### Scenario: 导航属性类型验证
- **WHEN** 实体包含导航属性（如 `Repository`、`WikiPages`）
- **THEN** 单对象引用导航使用 `NavigateType.OneToOne`
- **AND** 集合导航属性使用 `NavigateType.OneToMany`

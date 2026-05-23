## ADDED Requirements

### Requirement: Code First 配置开关
系统 SHALL 在 `appsettings.json` 中提供 `CodeFirst` 配置节，包含 `AutoSync` 布尔属性，控制启动时是否自动同步数据库结构。

#### Scenario: AutoSync 为 true 时自动同步
- **WHEN** `appsettings.json` 中 `CodeFirst.AutoSync` 为 `true` 且应用启动
- **THEN** 系统在应用启动后调用 `db.CodeFirst.SetStringDefaultLength(200).InitTables(entityTypes)` 自动创建或更新所有实体对应的数据库表

#### Scenario: AutoSync 为 false 时跳过同步
- **WHEN** `appsettings.json` 中 `CodeFirst.AutoSync` 为 `false` 或未配置
- **THEN** 系统跳过 Code First 同步步骤，仅输出 Information 级别日志 "CodeFirst.AutoSync 已禁用，跳过自动同步"

#### Scenario: 环境变量覆盖配置
- **WHEN** 环境变量 `HEIMDALL_CODEFIRST_AUTOSYNC` 设置为 `true` 或 `false`
- **THEN** 系统优先使用环境变量值覆盖 `appsettings.json` 配置

### Requirement: 启动时扫描实体类型
系统 SHALL 在启动时自动扫描所有需要 Code First 同步的实体类型，通过反射获取 `Core.Entities` 命名空间下所有 SqlSugar 实体类。

#### Scenario: 实体类型扫描
- **WHEN** Code First 同步启动
- **THEN** 系统从 `Heimdall.Core.Entities` 程序集中扫描所有标注了 `[SugarTable]` 或符合实体命名约定的类型
- **AND** 排除 DTO 和纯数据类（通过命名空间或基类过滤）

#### Scenario: 实体扫描失败回退
- **WHEN** 实体扫描或属性反射失败
- **THEN** 系统记录 Error 级别日志，不阻塞应用启动

### Requirement: Code First 同步失败不阻塞启动
系统 SHALL 确保 Code First 自动同步失败时不影响应用正常启动和服务。

#### Scenario: 单表同步失败继续处理
- **WHEN** `InitTables` 对某个实体因表冲突或权限问题失败
- **THEN** 系统记录 Error 级别日志（含表名和异常详情），继续处理其余实体

#### Scenario: 全部失败时记录错误并启动
- **WHEN** 所有实体同步均失败（如数据库连接不可用）
- **THEN** 系统记录 Critical 级别日志，提示管理员使用 SQL 脚本手动初始化，应用继续启动

### Requirement: 同步结果日志输出
系统 SHALL 在 Code First 同步完成后输出完整日志摘要，包含成功表数、失败表数、总耗时。

#### Scenario: 同步完成日志摘要
- **WHEN** Code First 同步流程结束
- **THEN** 系统输出 Information 级别日志："Code First 同步完成: 成功 {successCount} 张表, 失败 {failedCount} 张表, 耗时 {elapsedMs}ms"
- **AND** 如有失败表，在日志中列出表名和原因

## ADDED Requirements

### Requirement: SQL 日志运行时开关

系统 SHALL 提供运行时 API 端点，允许管理员动态开启/关闭 EF Core SQL 命令日志（`Microsoft.EntityFrameworkCore.Database.Command` 类别），无需重启应用或修改配置文件。

#### Scenario: 开启 SQL 日志
- **WHEN** 管理员发送 `POST /api/admin/logging/filter` 请求体 `{ "showSql": true }`
- **THEN** 系统将 EF Core Database.Command 日志级别从 `None` 切换为 `Information`
- **AND** 后续所有数据库查询的 SQL 语句和参数出现在控制台输出中

#### Scenario: 关闭 SQL 日志
- **WHEN** 管理员发送 `POST /api/admin/logging/filter` 请求体 `{ "showSql": false }`
- **THEN** 系统将 EF Core Database.Command 日志级别切换为 `None`
- **AND** 后续数据库查询不再输出 SQL 语句

#### Scenario: 查询当前日志过滤状态
- **WHEN** 管理员发送 `GET /api/admin/logging/status`
- **THEN** 系统返回当前日志过滤配置：`{ "showSql": false, "showEfCore": false }`

### Requirement: EF Core 日志类别独立过滤

SQL 命令日志（`Microsoft.EntityFrameworkCore.Database.Command`）与 EF Core 其他类别日志（`Microsoft.EntityFrameworkCore`）SHALL 可独立控制，互不影响。

#### Scenario: EF Core 警告日志始终可见
- **WHEN** `showSql` 为 false 且 EF Core 产生数据库连接警告
- **THEN** 警告日志正常输出，仅 SQL 命令日志被抑制
- **AND** EF Core 的 Infrastructure/ModelValidation 等类别日志不受 `showSql` 开关影响

### Requirement: 任务结构化进度日志

Wiki 生成任务 SHALL 在每个关键步骤输出结构化进度日志，包含：当前步骤名称、当前页码/总页数、已用时间、LLM 调用参数摘要。

#### Scenario: 页面生成步骤的进度日志
- **WHEN** Wiki 生成进入页面生成阶段
- **THEN** 系统每完成一个页面输出日志：`[WikiTask] 进度: 3/12 页 | 步骤: 页面生成 | 页面: 核心服务架构 | LLM: ollama/gemma4:e2b | 耗时: 12.3s`
- **AND** 日志级别为 `Information`，前缀为 `[WikiTask]`便于过滤

#### Scenario: 弱页面重生成步骤的进度日志
- **WHEN** 弱页面重生成阶段开始
- **THEN** 系统输出：`[WikiTask] 弱页面重生成: 检测到 3 个弱页面，开始重新生成 | TaskId: xxx`
- **AND** 每个弱页面重生成完成时输出：`[WikiTask] 弱页面: 2/3 | 页面: 数据流详解 | LLM: ollama/gemma4:e2b | 耗时: 8.1s`

#### Scenario: 任务完成的汇总日志
- **WHEN** Wiki 生成任务全部完成
- **THEN** 系统输出：`[WikiTask] 生成完成 | 总页数: 12 | 总耗时: 245.6s | LLM 调用: 13 次 | Token: 输入 125K / 输出 48K`

### Requirement: 日志级别分类展示

系统 SHALL 在控制台输出中对不同严重级别的日志使用视觉区分，错误日志包含完整异常堆栈，警告日志包含上下文信息，进度日志使用统一前缀。

#### Scenario: 错误日志输出
- **WHEN** 系统发生异常（如 LLM API 调用失败）
- **THEN** 错误日志包含：时间戳、`[ERROR]` 标签、错误消息、异常堆栈（如有）、关联的 TaskId

#### Scenario: SQL 日志与业务日志交错显示时的区分
- **WHEN** `showSql` 开启且业务日志和 SQL 日志同时输出
- **THEN** SQL 日志带有 `[SQL]` 前缀，业务日志带有各自模块前缀（如 `[WikiTask]`、`[Heimdall]`），便于视觉区分

## Purpose

任务生命周期管理——涵盖长时任务的逐阶段进度落盘、批次工件持久化、断点续跑（手动恢复 + 启动自动恢复）及前端可恢复状态展示。
## Requirements
### Requirement: 逐阶段强制落盘
长时任务（Wiki 生成）的每个阶段状态变更后，系统 SHALL 立即将 `TaskRecord` 的当前阶段、状态、进度百分比写入数据库。

#### Scenario: 阶段完成落盘
- **WHEN** 任意阶段标记为 completed
- **THEN** `TaskRecord` 的 `CurrentStage`、`ProgressPercent`、`ProgressMessage` SHALL 已写入数据库

#### Scenario: 进程崩溃后进度可恢复
- **WHEN** 任务在"页面生成"阶段中途崩溃
- **THEN** 数据库中 `TaskRecord` 的 `CurrentStage` SHALL 为最后完成的阶段

#### Scenario: 前台轮询感知进度
- **WHEN** 前台通过 `GET /api/tasks/{taskId}/status` 轮询
- **THEN** 每次请求 SHALL 返回数据库中最新的 `CurrentStage` 和 `ProgressPercent`

### Requirement: 页面批次工件即时落盘
每批页面生成完成后，系统 SHALL 在进入下一批之前将当前批次工件写入数据库，支持断点续跑。

#### Scenario: 批次工件落盘
- **WHEN** 第 2 批次页面生成完成
- **THEN** 第 1、2 批次的 `page_batch_artifact` 工件 SHALL 已在数据库中

#### Scenario: 断点续跑
- **WHEN** 任务在第 4 批次失败后重新启动
- **THEN** 系统 SHALL 从数据库恢复前 3 批次的工件，从第 4 批次继续生成

### Requirement: 代码分析阶段落盘
代码结构索引、分层摘要、系统摘要各子阶段完成后 SHALL 即时落盘。

#### Scenario: 代码分析可恢复
- **WHEN** 代码分析阶段完成
- **THEN** `code_analysis_artifact` SHALL 已持久化，后续重试可直接恢复

### Requirement: 任务手动恢复
系统 SHALL 提供 `POST /api/tasks/{taskId}/resume` 端点，允许从前端页面点击按钮恢复中断的 Wiki 生成任务。恢复 SHALL 跳过已完成的阶段和批次，从第一个未完成的检查点开始执行。

#### Scenario: 手动恢复中断任务
- **WHEN** 用户对 `Failed` 或 `Cancelled` 状态的任务点击"恢复"
- **THEN** 系统读取已落盘的阶段和批次工件，从中断点继续执行，`ResumeCount` 递增 1

#### Scenario: 从批次检查点恢复
- **WHEN** 任务在 Stage 5 第 3 批次中断后恢复
- **THEN** 系统跳过前 2 批次，从第 3 批次开始继续生成

#### Scenario: 从阶段检查点恢复
- **WHEN** 任务在 Stage 3（深度代码理解）中途中断且无批次工件
- **THEN** 系统从 Stage 3 重新开始执行（该阶段无批次概念，整体重做）

#### Scenario: 已完成任务拒绝恢复
- **WHEN** 客户端对 `Completed` 状态任务调用 resume
- **THEN** 系统返回 400 Bad Request

#### Scenario: 正在运行的任务不可重复恢复
- **WHEN** 客户端对 `Running` 状态任务调用 resume
- **THEN** 系统返回 409 Conflict

#### Scenario: 无检查点任务恢复即重新开始
- **WHEN** 任务在 Stage 1 开始前就失败，无任何检查点工件
- **THEN** 系统从 Stage 1 重新开始执行

### Requirement: 启动时自动恢复扫描
系统 SHALL 通过 `TaskResumeService`（`IHostedService` 实现）在应用启动完成后自动扫描并恢复因进程崩溃而中断的任务。

#### Scenario: 启动扫描到僵尸任务
- **WHEN** 应用启动，扫描到 `Running` 状态且 `UpdatedAt` 超过 5 分钟无更新的任务
- **THEN** 系统自动恢复这些任务，按创建时间顺序串行执行

#### Scenario: 新启动的运行中任务不触发恢复
- **WHEN** 扫描到 `Running` 状态但 `UpdatedAt` 在 1 分钟内的任务
- **THEN** 系统判定该任务仍在正常执行，不触发恢复

#### Scenario: 恢复重试上限
- **WHEN** 某任务连续自动恢复失败 3 次
- **THEN** 任务状态标记为 `Failed`，系统不再自动恢复该任务

#### Scenario: 无中断任务时静默跳过
- **WHEN** 扫描未发现需要恢复的任务
- **THEN** 系统静默跳过，仅 Debug 级别日志记录

### Requirement: TaskRecord 恢复计数字段
`TaskRecord` 实体 SHALL 包含 `ResumeCount` 字段（默认 0），每次恢复执行时递增 1。

#### Scenario: 首次恢复
- **WHEN** 任务首次被恢复（手动或自动）
- **THEN** `ResumeCount` 变为 1

### Requirement: 前端任务列表展示可恢复状态
前端任务列表 SHALL 为可恢复的任务展示"恢复"按钮和可恢复标记。

#### Scenario: 展示恢复按钮
- **WHEN** 用户查看任务列表，其中包含一个 `Failed` 状态的 Wiki 任务且有检查点工件
- **THEN** 该任务行显示"恢复"按钮，鼠标悬浮时提示"从检查点继续执行"

#### Scenario: 无检查点任务不展示恢复按钮
- **WHEN** 任务在 Stage 1 开始前就失败，无任何检查点工件
- **THEN** 该任务不展示"恢复"按钮，仅显示"重新开始"按钮

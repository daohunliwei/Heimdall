## ADDED Requirements

### Requirement: 逐阶段强制落盘
长时任务（Wiki 生成）的每个阶段状态变更后，系统 SHALL 立即调用 `SaveChangesAsync` 将 `TaskRecord` 的当前阶段、状态、进度百分比写入数据库。

#### Scenario: 仓库准备阶段落盘
- **WHEN** "仓库准备"阶段标记为 completed
- **THEN** `TaskRecord` 的 `CurrentStage`、`ProgressPercent`、`ProgressMessage` SHALL 已写入数据库

#### Scenario: 进程崩溃后进度可恢复
- **WHEN** 任务在"页面生成"阶段中途崩溃
- **THEN** 数据库中 `TaskRecord` 的 `CurrentStage` SHALL 为最后完成的阶段，`ProgressPercent` 为最后记录的百分比

#### Scenario: 前台轮询感知进度
- **WHEN** 前台通过 `GET /api/tasks/{taskId}/status` 轮询
- **THEN** 每次请求 SHALL 返回数据库中最新的 `CurrentStage` 和 `ProgressPercent`

### Requirement: 页面批次工件即时落盘
每批页面生成完成后，系统 SHALL 在进入下一批之前将当前批次工件（`page_batch_artifact`）写入数据库。

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

### Requirement: 任务恢复端点
系统 SHALL 提供 `POST /api/tasks/{taskId}/resume` 端点，从中断任务的最后检查点继续执行后续阶段和批次。

#### Scenario: 从批次检查点恢复
- **WHEN** 任务在 Stage 5 第 3 批次中断，客户端调用 `POST /api/tasks/{taskId}/resume`
- **THEN** 系统读取已落盘的批次工件，识别前 2 批次已完成，从第 3 批次开始继续生成

#### Scenario: 从阶段检查点恢复
- **WHEN** 任务在 Stage 3（深度代码理解）中途中断且无批次工件，客户端调用 resume
- **THEN** 系统从 Stage 3 重新开始执行（该阶段无批次概念，整体重做）

#### Scenario: 已完成任务拒绝恢复
- **WHEN** 客户端对 `Completed` 状态任务调用 resume
- **THEN** 系统返回 400 Bad Request，错误消息"任务已完成，无需恢复"

#### Scenario: 无检查点任务恢复即重新开始
- **WHEN** 任务在 Stage 1（仓库准备）开始前就失败，无任何检查点工件，客户端调用 resume
- **THEN** 系统从 Stage 1 重新开始执行

### Requirement: 启动时自动恢复扫描
系统 SHALL 通过 `TaskResumeService`（实现 `IHostedService`）在应用启动完成后自动扫描并恢复因进程崩溃而中断的任务。

#### Scenario: 启动扫描到僵尸任务
- **WHEN** 应用启动，`TaskResumeService` 扫描 `TaskRecord` 表，发现 2 个 `Running` 状态且 `UpdatedAt` 超过 5 分钟无更新的任务
- **THEN** 系统自动恢复这 2 个任务，按创建时间顺序串行执行

#### Scenario: 新启动的运行中任务不触发恢复
- **WHEN** 应用启动，`TaskResumeService` 扫描到 `Running` 状态但 `UpdatedAt` 在 1 分钟内的任务
- **THEN** 系统判定该任务仍在正常执行（可能由另一个实例处理），不触发恢复

#### Scenario: 恢复重试上限
- **WHEN** 某任务已通过自动恢复重试 3 次但均失败
- **THEN** 任务状态标记为 `Failed`，`ProgressMessage` 记录最后一次失败原因，系统不再自动恢复该任务

### Requirement: TaskRecord 恢复计数字段
`TaskRecord` 实体 SHALL 新增 `ResumeCount` 字段（默认 0），每次恢复执行时递增 1。

#### Scenario: 首次恢复
- **WHEN** 任务首次被恢复（手动或自动）
- **THEN** `ResumeCount` 变为 1

#### Scenario: 多次恢复
- **WHEN** 任务第 3 次被恢复执行
- **THEN** `ResumeCount` 变为 3

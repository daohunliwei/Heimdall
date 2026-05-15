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

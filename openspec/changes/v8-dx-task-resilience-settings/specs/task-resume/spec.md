## ADDED Requirements

### Requirement: 任务手动恢复
系统 SHALL 为中断的 Wiki 生成任务提供手动恢复端点 `POST /api/tasks/{taskId}/resume`，允许从前端页面点击按钮恢复执行。

#### Scenario: 手动恢复中断任务
- **WHEN** 用户在前端任务列表中对一个 `Failed` 或 `Cancelled` 状态的 Wiki 任务点击"恢复"按钮
- **THEN** 前端调用 `POST /api/tasks/{taskId}/resume`，后端读取任务最后完成的阶段和批次，从中断点继续执行后续阶段

#### Scenario: 已完成任务不可恢复
- **WHEN** 用户尝试恢复一个 `Completed` 状态的任务
- **THEN** API 返回 400 错误，提示"任务已完成，无需恢复"

#### Scenario: 正在运行的任务不可重复恢复
- **WHEN** 用户尝试恢复一个 `Running` 状态的任务
- **THEN** API 返回 409 错误，提示"任务正在运行中"

#### Scenario: 恢复后任务状态更新
- **WHEN** 任务恢复成功并开始执行
- **THEN** 原 `TaskRecord` 的 `Status` 从 `Failed`/`Cancelled` 变为 `Running`，`UpdatedAt` 更新时间戳，`ResumeCount` 递增 1

### Requirement: 服务启动自动恢复
系统 SHALL 在服务启动时自动扫描并恢复因进程崩溃而中断的 Wiki 任务。自动恢复由 `TaskResumeService`（`IHostedService` 实现）在应用启动完成后执行。

#### Scenario: 启动时发现可恢复任务
- **WHEN** 应用启动完成，`TaskResumeService` 扫描到状态为 `Running` 但 `UpdatedAt` 超过 5 分钟无更新的 Wiki 任务
- **THEN** 系统自动将该任务加入恢复队列，从最后检查点继续执行

#### Scenario: 无中断任务时静默跳过
- **WHEN** 应用启动完成，`TaskResumeService` 扫描未发现需要恢复的任务
- **THEN** 系统静默跳过，不产生日志噪音（仅 Debug 级别日志记录扫描完成）

#### Scenario: 恢复失败不再重试
- **WHEN** 某任务连续自动恢复失败 3 次
- **THEN** 任务状态标记为 `Failed`，`ProgressMessage` 记录最后一次失败原因，系统不再自动恢复该任务

#### Scenario: 启动恢复时的并发控制
- **WHEN** 有多个可恢复任务
- **THEN** 系统按创建时间顺序逐个恢复（串行），避免同时执行多个重量级 Wiki 生成任务导致资源耗尽

### Requirement: 恢复粒度——批次级检查点
系统 SHALL 在恢复任务时跳过已完成的阶段和批次，从第一个未完成的检查点开始执行。检查点判定 SHALL 基于 `task-progress-persistence` 中已落盘的阶段工件和批次工件。

#### Scenario: 阶段级恢复——跳过已完成阶段
- **WHEN** 任务在 Stage 5（页面生成）第 3 批次时中断，前 2 个阶段（仓库准备、代码索引）和 Stage 5 的前 2 批次已完成
- **THEN** 恢复后系统跳过仓库准备和代码索引阶段，从 Stage 5 第 3 批次继续生成

#### Scenario: 编码阶段恢复——从 Stage 1 开始
- **WHEN** 任务在 Stage 3（深度代码理解）中途中断且该阶段无检查点工件
- **THEN** 恢复后系统从 Stage 3 重新开始（该阶段无批次概念，整体重做）

### Requirement: 前端任务列表展示可恢复状态
前端任务列表 SHALL 为可恢复的任务（`Failed` 或 `Cancelled` 状态，且存在检查点工件）展示"恢复"按钮和可恢复标记。

#### Scenario: 展示恢复按钮
- **WHEN** 用户查看任务列表，其中包含一个 `Failed` 状态的 Wiki 任务
- **THEN** 该任务行显示"恢复"按钮，鼠标悬浮时提示"从检查点继续执行"

#### Scenario: 无检查点任务不展示恢复按钮
- **WHEN** 任务在 Stage 1（仓库准备）开始前就失败，无任何检查点工件
- **THEN** 该任务不展示"恢复"按钮，仅显示"重新开始"按钮

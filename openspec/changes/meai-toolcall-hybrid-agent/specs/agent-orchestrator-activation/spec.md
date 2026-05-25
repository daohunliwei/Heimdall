## ADDED Requirements

### Requirement: WikiTaskService 集成 AgentOrchestratorService
系统 SHALL 在 `WikiTaskService.ExecuteAsync` 的 Stage 2（结构规划）完成后，调用 `AgentOrchestratorService.ShouldUseSubAgents(sourceFileCount)` 判断是否启用子代理模式。若启用，SHALL 调用 `AgentOrchestratorService.AssignModules(moduleNames, entries)` 获取模块分组，然后将各模块分发给子代理并行处理页面生成。

#### Scenario: 大仓库触发子代理模式
- **WHEN** 仓库包含 5000 个源代码文件，`AgentOrchestratorService.ShouldUseSubAgents(5000)` 返回 `true`
- **THEN** 系统调用 `AssignModules` 获取分组结果（如 6 个模块分给 3 个子代理）
- **AND** 每个子代理独立执行 Stage 3-5-6（代码理解→页面生成→质量审查）
- **AND** 主代理（Coordinator）等待所有子代理完成后执行 Stage 7-8（渲染→持久化）

#### Scenario: 小仓库不触发子代理模式
- **WHEN** 仓库包含 200 个源代码文件，`ShouldUseSubAgents(200)` 返回 `false`
- **THEN** 系统按现有 8 阶段管线顺序执行，不创建子代理

#### Scenario: 子代理并发数量受控
- **WHEN** 6 个模块需要子代理处理，`AgentOrchestratorService` 最大并发数为 3
- **THEN** 前 3 个子代理通过 `AcquireSlotAsync` 获取信号量开始执行
- **AND** 后 3 个等待前面的子代理释放信号量

### Requirement: 子代理任务上下文传递
每个子代理 SHALL 接收包含以下信息的任务上下文：分配的模块名称列表、模块包含的文件路径列表、结构规划产出的父页面引用（parent page links）、当前使用的 Provider 和 Model 配置。子代理 SHALL 通过 `TaskLlmService` 发起 LLM 调用（与主代理共享同一 Provider）。

#### Scenario: 子代理接收任务上下文
- **WHEN** 子代理被分配到"数据访问层"模块（含 50 个文件）
- **THEN** 子代理收到包含模块名、50 个文件路径、父页面 "架构概览" 的引用的上下文对象
- **AND** 子代理使用与主任务相同的 Provider 和 Model 配置

### Requirement: 子代理只读约束
子代理 SHALL 只能读取代码文件和调用检索工具，SHALL NOT 修改数据库记录、文件系统或更新任务状态。所有写操作（Wiki 页面持久化、工件保存、任务状态更新）SHALL 由主代理在收集子代理结果后统一执行。

#### Scenario: 子代理生成页面内容
- **WHEN** 子代理完成"数据访问层"模块的页面生成
- **THEN** 子代理将生成的页面内容（Markdown 文本）和元数据（页面标题、父页面引用）返回给主代理
- **AND** 主代理负责调用 `UpsertWikiPageAsync` 持久化到数据库
- **AND** 主代理负责调用 `UpsertTaskArtifactAsync` 保存工件

### Requirement: 子代理失败隔离
单个子代理的失败 SHALL NOT 影响其他子代理或整体流程。主代理 SHALL 捕获子代理异常，调用 `HandleSubAgentFailure` 执行降级处理（由主代理直接处理该模块），并继续等待其他子代理完成。

#### Scenario: 单个子代理超时失败
- **WHEN** 3 个子代理并行运行，其中 1 个因 Provider 超时失败
- **THEN** 主代理记录错误日志并调用 `HandleSubAgentFailure`
- **AND** 主代理直接使用传统方式为该模块生成页面（降级模式）
- **AND** 其他 2 个子代理继续运行不受影响
- **AND** 最终 Wiki 包含所有模块的页面（失败的模块由降级路径补充）

#### Scenario: 所有子代理失败
- **WHEN** 全部 3 个子代理均因 Provider 错误失败
- **THEN** 主代理回退到传统 8 阶段管线顺序执行
- **AND** 记录 Critical 日志

### Requirement: 模块间交叉引用一致性
主代理 SHALL 在所有子代理返回结果后，执行全局一致性合并阶段。合并 SHALL 检测跨模块引用（如模块 A 页面提到模块 B 的类），验证目标页面存在，并生成正确的 Wiki 交叉链接。

#### Scenario: 自动生成交叉链接
- **WHEN** 模块 A 生成的页面中提到 `IUserRepository`，该接口定义在模块 B 中
- **THEN** 全局合并阶段自动将 `IUserRepository` 文本替换为 `[IUserRepository](../module-b/interfaces/iuserrepository.md)` 形式的 Wiki 链接
- **AND** 如果模块 B 的页面中不存在 `IUserRepository` 的说明，追加一条 `@待补充` 标记

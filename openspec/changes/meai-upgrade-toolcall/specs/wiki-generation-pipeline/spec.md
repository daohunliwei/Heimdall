## ADDED Requirements

### Requirement: Stage 5 Tool Call 增强
系统 SHALL 在页面生成阶段的 LLM 调用中，根据配置开关 `ToolCall.Stage5.Enabled` 决定是否在 `ChatOptions.Tools` 中注入 `ReadCodeFile` 和 `SearchSymbols` 的 `AIFunction` 列表。`FunctionInvokingChatClient` SHALL 自动处理工具调用往返。

#### Scenario: 主动检索缺失的代码上下文
- **WHEN** `ToolCall.Stage5.Enabled` 为 `true`，LLM 正在生成《数据访问层设计》页面
- **THEN** `ChatOptions.Tools` 包含 `ReadCodeFile` 和 `SearchSymbols`
- **AND** LLM 发现预置上下文中缺少 `DbSession` 类定义
- **AND** LLM 调用 `SearchSymbols("DbSession")` 找到文件路径
- **AND** `FunctionInvokingChatClient` 自动执行搜索并将结果返回给 LLM
- **AND** LLM 基于搜索结果继续撰写页面

#### Scenario: Tool Call 未启用时的降级
- **WHEN** `ToolCall.Stage5.Enabled` 为 `false`
- **THEN** `ChatOptions.Tools` 为 `null`
- **AND** `FunctionInvokingChatClient` 直接透传请求/响应
- **AND** 行为与当前版本完全一致

### Requirement: WikiTaskService Orchestrator 分支
系统 SHALL 在 `WikiTaskService.ExecuteAsync` 的 Stage 2（结构规划）完成后，判断是否启用 Orchestrator 路径。若 `AgentOrchestratorService.ShouldUseSubAgents(sourceFileCount)` 返回 `true`，SHALL 使用 Orchestrator 路径并行分发子代理。

#### Scenario: Orchestrator 路径分叉
- **WHEN** `ShouldUseSubAgents` 返回 `true`
- **THEN** 系统调用 `AssignModules` 获取模块分组
- **AND** 子代理并行执行 Stage 3（代码理解）+ Stage 5（页面生成）+ Stage 6（质量审查）
- **AND** 主代理收集结果后执行全局一致性合并和后续持久化

#### Scenario: 传统管线路径
- **WHEN** `ShouldUseSubAgents` 返回 `false`
- **THEN** 系统按 Stage 1→2→3→4→5→6→7→8 顺序执行
- **AND** 不创建子代理

## MODIFIED Requirements

### Requirement: 结构规划阶段
Wiki 生成管线 SHALL 在结构规划阶段根据 `StructurePlanning.Strategy` 配置选择策略。结构规划完成后，若满足子代理触发条件，系统 SHALL 可选择使用 Orchestrator 路径进行后续阶段。

#### Scenario: Deterministic 策略执行
- **WHEN** 策略为 `Deterministic` 且结构规划阶段开始
- **THEN** 系统调用 `DeterministicStructurePlanner.BuildStructure(CodeIndexResult)` 直接返回 WikiStructureDto
- **AND** 不发起任何 LLM 调用
- **AND** 耗时 < 100ms

#### Scenario: 结构规划完成后触发 Orchestrator 评估（新增）
- **WHEN** 结构规划阶段完成并产出 `WikiStructureDto`
- **THEN** 系统调用 `AgentOrchestratorService.ShouldUseSubAgents(sourceFileCount)` 评估是否启用子代理
- **AND** 若启用，后续阶段使用 Orchestrator 路径并行执行
- **AND** 若不启用，后续阶段使用传统 8 阶段管线顺序执行

#### Scenario: 策略变更不影响已运行任务
- **WHEN** 某任务已开始执行结构规划
- **THEN** 该任务使用开始时的策略配置完成，中途变更不影响

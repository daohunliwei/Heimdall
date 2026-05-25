## ADDED Requirements

### Requirement: Stage 5 页面生成 Tool Call 增强
系统 SHALL 在页面生成阶段的 LLM 调用中，根据配置开关 `ToolCall.Stage5.Enabled` 决定是否绑定 `ReadCodeFile` 和 `SearchSymbols` 工具。绑定后，LLM SHALL 能够在发现预检索上下文不完整时，主动调用工具获取缺失的代码信息，替代"一次性打包"模式。

#### Scenario: 主动检索缺失的代码上下文
- **WHEN** `ToolCall.Stage5.Enabled` 为 `true`，LLM 正在生成《数据访问层设计》页面
- **THEN** LLM 发现预置上下文中缺少 `DbSession` 类的定义
- **AND** LLM 调用 `SearchSymbols("DbSession")` 找到该类的文件路径
- **AND** LLM 调用 `ReadCodeFile("/src/Data/DbSession.cs")` 获取完整实现
- **AND** LLM 基于获取的代码内容继续撰写页面

#### Scenario: 避免重复检索
- **WHEN** 同一批次（PageBatch）中的多个页面需要同一文件的代码
- **THEN** LLM 应在生成批次第一个页面时检索该文件，后续页面直接引用已获取的内容
- **AND** 系统在消息历史中保留工具调用结果，LLM 可通过上下文回溯而非重复调用

#### Scenario: Tool Call 未启用时的降级行为
- **WHEN** `ToolCall.Stage5.Enabled` 为 `false`
- **THEN** Stage 5 LLM 调用使用传统 `GenerateTextAsync`，使用预检索上下文
- **AND** 行为与当前版本完全一致

### Requirement: WikiTaskService Orchestrator 分支
系统 SHALL 在 `WikiTaskService.ExecuteAsync` 的 Stage 2（结构规划）完成后，判断是否启用子代理模式。若 `AgentOrchestratorService.ShouldUseSubAgents(sourceFileCount)` 返回 `true`，SHALL 使用 Orchestrator 路径将模块分组并行分发；否则 SHALL 按现有 8 阶段管线顺序执行。

#### Scenario: Orchestrator 路径分叉
- **WHEN** `ShouldUseSubAgents` 返回 `true`
- **THEN** 系统调用 `AssignModules` 获取模块分组
- **AND** 系统为每个子代理组创建独立的 `TaskLlmService` 调用上下文
- **AND** 子代理并行执行 Stage 3（代码理解）、Stage 5（页面生成）、Stage 6（质量审查）
- **AND** 主代理收集所有子代理结果后执行全局一致性合并
- **AND** 主代理执行 Stage 7（渲染后处理）和 Stage 8（持久化）

#### Scenario: 传统管线路径
- **WHEN** `ShouldUseSubAgents` 返回 `false`
- **THEN** 系统按 Stage 1→2→3→4→5→6→7→8 顺序执行
- **AND** 不创建子代理，不使用 Orchestrator 路径

#### Scenario: Orchestrator 超时保护
- **WHEN** 子代理执行超过配置的超时时间（默认 30 分钟）
- **THEN** 主代理取消超时的子代理
- **AND** 主代理调用 `HandleSubAgentFailure` 降级处理超时模块
- **AND** 其余子代理继续运行

## MODIFIED Requirements

### Requirement: 结构规划阶段
Wiki 生成管线 SHALL 在结构规划阶段根据 `StructurePlanning.Strategy` 配置选择策略：`Deterministic`（默认）使用代码索引数据直接生成 WikiStructureDto；`LlmJson` 使用 LLM 生成 JSON 后解析；`LlmEnhanced` 使用算法骨架 + LLM 润色。最终产物均为 `WikiStructureDto`，页面生成阶段无感知。结构规划完成后，若满足子代理触发条件，系统 SHALL 可选择使用 Orchestrator 路径进行后续阶段。

#### Scenario: Deterministic 策略执行
- **WHEN** 策略为 `Deterministic` 且结构规划阶段开始
- **THEN** 系统调用 `DeterministicStructurePlanner.BuildStructure(CodeIndexResult)` 直接返回 WikiStructureDto
- **AND** 不发起任何 LLM 调用
- **AND** 耗时 < 100ms

#### Scenario: LlmJson 策略执行（当前行为）
- **WHEN** 策略为 `LlmJson` 且结构规划阶段开始
- **THEN** 系统使用现有 LLM prompt 生成 JSON，解析为 WikiStructureDto
- **AND** 保留全部重试和回退逻辑

#### Scenario: 结构规划完成后触发 Orchestrator 评估（新增）
- **WHEN** 结构规划阶段完成并产出 `WikiStructureDto`（含模块分组信息）
- **THEN** 系统调用 `AgentOrchestratorService.ShouldUseSubAgents(sourceFileCount)` 评估是否启用子代理
- **AND** 若启用，后续阶段（代码理解→页面生成→审查）使用 Orchestrator 路径
- **AND** 若不启用，后续阶段使用传统 8 阶段管线顺序执行

#### Scenario: 策略变更不影响已运行任务
- **WHEN** 某任务已开始执行结构规划
- **THEN** 该任务使用开始时的策略配置完成，中途变更不影响

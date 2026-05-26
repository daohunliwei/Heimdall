## ADDED Requirements

### Requirement: Stage 3 Tool Call 绑定
系统 SHALL 在深度代码理解阶段的 LLM 调用中，根据配置开关 `ToolCall.Stage3.Enabled` 决定是否在 `ChatOptions.Tools` 中注入 `QueryCallGraph` 和 `RetrieveClassDefinition` 的 `AIFunction` 列表。`FunctionInvokingChatClient` SHALL 自动处理工具调用往返。

#### Scenario: Tool Call 增强的代码理解
- **WHEN** `ToolCall.Stage3.Enabled` 为 `true`，Stage 3 LLM 调用开始
- **THEN** `ChatOptions.Tools` 包含 `QueryCallGraph` 和 `RetrieveClassDefinition`
- **AND** LLM 在分析时发现静态分析调用关系标记为低置信度，可调用 `QueryCallGraph` 获取精确关系
- **AND** `FunctionInvokingChatClient` 自动执行工具并返回结果给 LLM

#### Scenario: Tool Call 未启用时的降级
- **WHEN** `ToolCall.Stage3.Enabled` 为 `false`
- **THEN** `ChatOptions.Tools` 为 `null`
- **AND** `FunctionInvokingChatClient` 检测到无工具需求，直接透传请求/响应
- **AND** 行为与不使用 `UseFunctionInvocation()` 时完全一致

## MODIFIED Requirements

### Requirement: LLM 辅助架构理解
系统 SHALL 在本地 AST 分析完成后，执行 1-2 次 LLM 调用对分析结果进行高级架构理解。LLM 输入 SHALL 包含：基于 AST 提取的模块列表、精确的依赖拓扑、高置信度调用图摘要（≥0.95 边）、继承链与接口实现关系。当 `ToolCall.Stage3.Enabled` 为 `true` 时，LLM 调用 SHALL 通过 `ChatOptions.Tools` 绑定 `QueryCallGraph` 和 `RetrieveClassDefinition`，由 `FunctionInvokingChatClient` 自动处理工具交互。

#### Scenario: 识别分层架构（AST 数据输入）
- **WHEN** AST 分析显示 20+ Controller 继承同一基类、15+ Repository 实现同一仓储接口、10+ Service 注入 Controller 中
- **THEN** LLM 基于精确的继承/实现/调用数据确认为"分层架构"，输出各层职责和数据流描述

#### Scenario: Tool Call 辅助解析复杂模式（新增）
- **WHEN** AST 分析显示某方法实现了接口但实现类通过多层继承间接获得接口方法签名
- **THEN** LLM 通过 `FunctionInvokingChatClient` 调用 `RetrieveClassDefinition` 获取各层类的定义
- **AND** `FunctionInvokingChatClient` 自动将工具结果返回给 LLM
- **AND** LLM 输出包含完整继承链的架构描述

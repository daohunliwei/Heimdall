## ADDED Requirements

### Requirement: Stage 3 LLM 调用绑定工具集
系统 SHALL 在深度代码理解阶段的 LLM 调用中，根据配置开关 `ToolCall.Stage3.Enabled` 决定是否绑定 `QueryCallGraph` 和 `RetrieveClassDefinition` 工具。绑定后，LLM SHALL 能够在发现静态分析结果存在歧义或不完整时，主动调用工具获取关键类的完整定义或精确调用关系。

#### Scenario: Tool Call 增强的代码理解
- **WHEN** `ToolCall.Stage3.Enabled` 为 `true`，Stage 3 LLM 调用开始
- **THEN** 系统将 `QueryCallGraph` 和 `RetrieveClassDefinition` 的 `AIFunction` 注入 `ChatOptions.Tools`
- **AND** LLM 在分析 `ServiceA` 调用链时，若发现 AST 提取的调用关系标记为"低置信度"，可调用 `QueryCallGraph("ServiceA.Process")` 获取精确调用关系
- **AND** LLM 在发现预置上下文中缺少 `IMiddleware` 接口定义时，可调用 `RetrieveClassDefinition("IMiddleware")` 获取完整定义

#### Scenario: Tool Call 未启用时的降级行为
- **WHEN** `ToolCall.Stage3.Enabled` 为 `false`
- **THEN** Stage 3 LLM 调用使用传统 `GenerateTextAsync`，不绑定任何工具
- **AND** 行为与当前版本完全一致

#### Scenario: Tool Call 触发条件
- **WHEN** LLM 在代码理解过程中发现以下任一情况
- **THEN** LLM 可以（非强制）触发工具调用：调用图中存在置信度 < 0.95 的边、预置上下文中缺少某关键接口的完整定义、发现设计模式信号但本地 AST 分析置信度 < 0.8

## MODIFIED Requirements

### Requirement: LLM 辅助架构理解
系统 SHALL 在本地 AST 分析完成后，执行 1-2 次 LLM 调用对分析结果进行高级架构理解。LLM 输入 SHALL 包含：基于 AST 提取的模块列表、精确的依赖拓扑、高置信度调用图摘要（≥0.95 边）、继承链与接口实现关系。当 `ToolCall.Stage3.Enabled` 为 `true` 时，LLM 调用 SHALL 绑定 `QueryCallGraph` 和 `RetrieveClassDefinition` 工具，允许 LLM 主动探查不确定的调用关系和类定义。

#### Scenario: 识别分层架构（AST 数据输入）
- **WHEN** AST 分析显示 20+ Controller 继承同一基类、15+ Repository 实现同一仓储接口、10+ Service 注入 Controller 中
- **THEN** LLM 基于精确的继承/实现/调用数据确认为"分层架构"，输出各层职责和数据流描述

#### Scenario: Tool Call 辅助解析复杂模式（新增）
- **WHEN** AST 分析显示某方法实现了接口但实现类通过多层继承间接获得接口方法签名，LLM 无法从预置上下文确定实际继承链
- **THEN** LLM 调用 `RetrieveClassDefinition` 获取各层类的定义，构建完整继承链
- **AND** 输出准确的架构描述，包含继承层级和职责分布

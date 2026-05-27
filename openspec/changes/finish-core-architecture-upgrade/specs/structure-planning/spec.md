## MODIFIED Requirements

### Requirement: LlmJson 策略 AST 数据注入（结构化消息）
`LlmJson` 策略的提示词 SHALL 从 DB 加载（通过 `IPromptMergeService`），以结构化消息发送。System 消息包含角色定义和输出格式约束，User 消息包含 Tree-sitter AST 产出的代码理解数据（调用图摘要——来自新 AST 调用图、模块依赖拓扑、设计模式列表——来自新 AST 模式检测）、代码索引统计摘要及仓库文件树。

#### Scenario: 调用图数据注入 User 消息
- **WHEN** 系统构建结构规划消息且 `CodeUnderstandingResult` 可用
- **THEN** User 消息包含来自 Tree-sitter AST 的调用图摘要（节点数、边数、最大深度）和模块依赖拓扑

#### Scenario: 设计模式数据注入 User 消息
- **WHEN** 新 AST 分析检测到设计模式
- **THEN** User 消息列出检测到的模式名称、置信度和参与类
- **AND** LLM 优先为设计模式相关代码创建专题页面

### Requirement: 三种可配置的结构规划策略
系统 SHALL 通过 `IPromptMergeService` 从 DB 加载结构规划提示词模板。策略配置（LlmJson / Deterministic / LlmEnhanced）不变，但提示词来源从硬编码改为 DB 驱动。

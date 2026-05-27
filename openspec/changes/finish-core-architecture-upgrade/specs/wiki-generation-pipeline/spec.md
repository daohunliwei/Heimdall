## MODIFIED Requirements

### Requirement: Wiki 生成管线 LLM 调用使用结构化消息
Wiki 生成管线的所有 LLM 调用 SHALL 使用 `List<ChatMessage>` 结构化消息列表，System 角色和 User 角色的提示词内容 SHALL 分离为独立消息。不再允许将系统指令、上下文数据、用户指令拼接为单字符串发送。

#### Scenario: 结构规划 LLM 调用
- **WHEN** WikiTaskService 执行结构规划阶段
- **THEN** 调用 `_taskLlm.GenerateWithMetricsAsync` 的结构化消息重载，传入 `[ChatMessage(ChatRole.System, systemPrompt), ChatMessage(ChatRole.User, userContext)]`

#### Scenario: 页面生成 LLM 调用
- **WHEN** WikiTaskService 执行页面生成阶段
- **THEN** 系统指令作为 `ChatRole.System` 消息，代码上下文和页面要求作为独立 `ChatRole.User` 消息

#### Scenario: 弱页面重生成 LLM 调用
- **WHEN** 弱页面触发重生成
- **THEN** 重生成 prompt 同样使用结构化消息：System 消息包含原始内容摘要和质量反馈，User 消息包含额外代码片段

### Requirement: 检索增强页面生成
页面生成阶段 SHALL 使用当前已落地的 BM25 检索与版本化工件上下文获取真实代码片段，通过结构化消息注入 LLM 调用。输出 SHALL 包含真实代码引用，不得包含虚构的示例代码。

#### Scenario: 页面生成含真实代码（结构化消息）
- **WHEN** 生成用户认证 Wiki 页面
- **THEN** System 消息包含角色设定和输出约束，User 消息包含检索到的代码片段和页面主题
- **AND** 代码片段与页面主题作为独立的上下文消息，不与系统指令混合

### Requirement: 差异化提示词
Stage 5 页面生成 SHALL 根据页面的 ContentDepthLevel（overview/section/article）使用差异化提示词，通过 `IPromptMergeService` 从 DB 加载对应模板。

#### Scenario: article 级页面使用深度提示词
- **WHEN** 页面 ContentDepthLevel=article
- **THEN** DB 加载 article 级提示词模板（Sys+User 分离），侧重代码深挖、逐方法分析、时序图

### Requirement: 结构规划阶段
Wiki 生成管线 SHALL 在结构规划阶段通过 `IPromptMergeService` 从 DB 获取提示词模板，根据 `StructurePlanning.Strategy` 配置选择策略。所有 LLM 调用 SHALL 使用结构化消息。

#### Scenario: LlmJson 策略执行（结构化消息）
- **WHEN** 策略为 LlmJson
- **THEN** System 消息包含角色和输出格式要求，User 消息包含 AST 数据、代码索引统计和仓库文件树

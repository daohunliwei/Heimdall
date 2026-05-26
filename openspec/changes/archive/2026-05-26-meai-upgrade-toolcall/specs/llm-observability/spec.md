## MODIFIED Requirements

### Requirement: MEAI OpenTelemetry 自动追踪
系统 SHALL 通过 `ChatClientBuilder.UseOpenTelemetry()` 中间件自动记录 LLM 调用的 Trace 和 Metrics。**变更**：原自定义 `LoggingChatClient` 弃用，统一使用 MEAI 内置 `UseOpenTelemetry()` + `UseLogging()` 替代；自定义 `RetryChatClient` 弃用，重试逻辑由 `UseResilience()` 处理。

#### Scenario: Trace span 自动创建
- **WHEN** 任意通过 `ChatClientBuilder` 管道发起的 LLM 调用
- **THEN** OpenTelemetry 中间件自动创建 span，携带 `gen_ai.system`、`gen_ai.request.model`、`gen_ai.usage.input_tokens` 等标准属性

#### Scenario: 与 LlmCallMetrics 表共存
- **WHEN** MEAI OpenTelemetry 中间件记录 Trace
- **THEN** 系统同时将指标持久化到 `llm_call_metrics` 表
- **AND** 两条记录通过 `ResponseId` 或 `ConversationId` 关联

### Requirement: LLM 调用级指标收集
系统 SHALL 对每次 LLM 调用记录结构化指标。指标数据来源从 `ChatResponse.Usage`（`UsageDetails`）读取。

#### Scenario: UsageDetails 导入指标
- **WHEN** LLM 调用完成并返回 `ChatResponse`
- **THEN** 从 `response.Usage.InputTokenCount` 读取 InputTokens
- **AND** 从 `response.Usage.OutputTokenCount` 读取 OutputTokens
- **AND** 从 `response.Usage.CachedInputTokenCount` 读取 CacheHitTokens

### Requirement: 流式调用指标实时记录
系统 SHALL 在流式调用完成后立即记录指标。

#### Scenario: 流式成功调用耗时
- **WHEN** 流式调用成功完成
- **THEN** 系统记录 LatencyMs = 最后一个 chunk 到达时间 - 请求开始时间
- **AND** FirstTokenLatencyMs = 第一个 chunk 到达时间 - 请求开始时间

### Requirement: Tool Call 日志扩展（新增）
系统 SHALL 在 `TaskLlmCallLog` 中新增 `ToolCallLogs` 集合属性，记录每次 Tool Call 的工具名、参数、返回长度、耗时和轮次索引。数据 SHALL 从 `ChatResponse` 的 `AdditionalProperties` 中提取。

#### Scenario: Tool Call 日志记录
- **WHEN** Stage 5 页面生成中 LLM 调用 2 次工具
- **THEN** `TaskLlmCallLog.ToolCallLogs` 包含两条记录
- **AND** 每条记录包含 `ToolName`、`Arguments`（脱敏）、`ResultLength`、`DurationMs`、`RoundIndex`

## REMOVED Requirements

### Requirement: LoggingChatClient 自定义日志中间件
**Reason**: MEAI 10.6.0 内置 `UseLogging()` 提供等效功能。
**Migration**: 删除 `ChatClientBuilderExtensions.cs` 中的 `LoggingChatClient` 类，改用 `UseLogging()`。

### Requirement: RetryChatClient 自定义重试中间件
**Reason**: MEAI 10.6.0 的 `UseResilience()` 提供等效功能。
**Migration**: 删除 `ChatClientBuilderExtensions.cs` 中的 `RetryChatClient` 类，`LlmRetryPipeline` 逻辑通过 `UseResilience()` 注入。

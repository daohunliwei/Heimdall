## MODIFIED Requirements

### Requirement: LLM 调用级指标收集
系统 SHALL 对每次 LLM 调用记录结构化指标。**变更**：新增 `IsStreaming`（是否流式调用）、`IsEstimated`（Token 是否估算值）、`FirstTokenLatencyMs`（首 Token 延迟）字段。指标数据来源从自研 `ChatCompletionResponse.Usage` 改为 MEAI 的 `ChatResponse.Usage`（`UsageDetails`）。

#### Scenario: 流式调用记录首 Token 延迟
- **WHEN** 流式 `GetStreamingResponseAsync` 返回第一个 `ChatResponseUpdate`
- **THEN** 系统记录 FirstTokenLatencyMs = 第一个 chunk 到达时间 - 请求开始时间

#### Scenario: 非流式调用 FirstTokenLatencyMs
- **WHEN** 非流式 `GetResponseAsync` 完成调用
- **THEN** FirstTokenLatencyMs 设为等于 LatencyMs

#### Scenario: UsageDetails 导入指标
- **WHEN** LLM 调用完成并返回 `ChatResponse`
- **THEN** 从 `response.Usage.InputTokenCount` 读取 InputTokens
- **AND** 从 `response.Usage.OutputTokenCount` 读取 OutputTokens
- **AND** 从 `response.Usage.CachedInputTokenCount` 读取 CacheHitTokens
- **AND** 从 `response.Usage.AdditionalCounts["IsEstimated"]` 读取 IsEstimated 标记

### Requirement: Provider 不返回 usage 时估算
系统 SHALL 在 Provider 响应不包含 token usage 信息时使用 TokenCounter 估算。**变更**：该规则扩展覆盖流式调用场景，流式完成后统一通过 TokenCounter 估算 `InputTokenCount` 和 `OutputTokenCount`。

#### Scenario: 流式无 usage 字段时估算
- **WHEN** 流式 LLM 调用完成，最后一个 `ChatResponseUpdate` 不含 `UsageDetails`
- **THEN** 系统使用 TokenCounter 对完整 prompt 和 response 文本估算 Token
- **AND** 即使估算失败，也不影响主流程

#### Scenario: 流式有 usage 字段时直接使用
- **WHEN** 流式 LLM 调用的最后一个 `ChatResponseUpdate` 包含 `UsageDetails`
- **THEN** 系统直接采用，不做估算

## ADDED Requirements

### Requirement: MEAI OpenTelemetry 自动追踪
系统 SHALL 通过 `ChatClientBuilder.UseOpenTelemetry()` 中间件自动记录 LLM 调用的 Trace 和 Metrics，无需手动在 Provider 中编码指标收集逻辑。

#### Scenario: Trace span 自动创建
- **WHEN** 任意通过 `ChatClientBuilder` 管道发起的 LLM 调用
- **THEN** OpenTelemetry 中间件自动创建 span，携带 `gen_ai.system`、`gen_ai.request.model`、`gen_ai.usage.input_tokens` 等标准属性

#### Scenario: 与 LlmCallMetrics 表共存
- **WHEN** MEAI OpenTelemetry 中间件记录 Trace
- **THEN** 系统同时将指标持久化到 `llm_call_metrics` 表（用于管理后台查询）
- **AND** 两条记录通过 `ResponseId` 或 `ConversationId` 关联

### Requirement: 流式调用指标实时记录
系统 SHALL 在流式调用完成后立即记录指标，非流式调用的指标记录逻辑不变。

#### Scenario: 流式成功调用耗时
- **WHEN** 流式调用成功完成（所有 `ChatResponseUpdate` 收集完毕）
- **THEN** 系统记录 LatencyMs = 最后一个 chunk 到达时间 - 请求开始时间
- **AND** FirstTokenLatencyMs = 第一个 chunk 到达时间 - 请求开始时间

#### Scenario: 流式失败调用指标
- **WHEN** 流式调用中途因网络或 API 错误失败
- **THEN** 系统记录 Success=false，ErrorType=具体错误类型
- **AND** InputTokens 使用请求前的估算值，OutputTokens=0

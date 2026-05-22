## ADDED Requirements

### Requirement: UsageDetails 替代自研 TokenUsage
系统 SHALL 使用 MEAI 的 `UsageDetails` 替代自研 `TokenUsage` 模型，所有 LLM 调用的 Token 统计以 `UsageDetails` 为准。

#### Scenario: 字段映射
- **WHEN** Provider 返回 `ChatResponse`
- **THEN** `UsageDetails.InputTokenCount` 映射为输入 Token 数
- **AND** `UsageDetails.OutputTokenCount` 映射为输出 Token 数
- **AND** `UsageDetails.CachedInputTokenCount` 映射为缓存命中 Token 数（原 `CacheHitTokens`）
- **AND** `UsageDetails.TotalTokenCount` 为总 Token 数
- **AND** `UsageDetails.ReasoningTokenCount` 映射为思考/推理 Token 数（DeepSeek 等思考模型）

### Requirement: 流式调用 Token 估算
系统 SHALL 在流式 LLM 调用完成后，收集所有 `ChatResponseUpdate` 拼接完整响应文本，通过现有 `TokenCounter.EstimateTokenCount` 方法估算 Token 消耗。

#### Scenario: 流式无 Usage 时估算
- **WHEN** 流式 LLM 调用完成但 `ChatResponseUpdate` 链中无 `Usage` 信息
- **THEN** 系统将完整 input 文本传入 `TokenCounter.EstimateTokenCount` 估算 InputTokenCount
- **AND** 将完整 output 文本传入 `TokenCounter.EstimateTokenCount` 估算 OutputTokenCount
- **AND** 在 `UsageDetails.AdditionalCounts` 中标记 `"IsEstimated": true`

#### Scenario: 流式有 Usage 时直接使用
- **WHEN** 流式 LLM 调用的最后一个 `ChatResponseUpdate` 包含 `UsageDetails`
- **THEN** 系统优先使用 API 返回的 usage 值，不做估算
- **AND** `AdditionalCounts["IsEstimated"]` 设为 `false`

#### Scenario: CachedInputTokenCount 默认值
- **WHEN** 流式估算 Token 时 Provider 不提供缓存命中信息
- **THEN** UsageDetails.CachedInputTokenCount 设为 0

### Requirement: Token 估算异常安全
系统 SHALL 确保 Token 估算过程的任何异常都不影响主业务流程。

#### Scenario: TokenCounter 估算抛出异常
- **WHEN** `TokenCounter.EstimateTokenCount` 因输入异常抛出异常
- **THEN** 系统 catch 异常，记录 Warning 级别日志
- **AND** UsageDetails 设为默认值（所有 Token Count 为 0，AdditionalCounts["IsEstimated"] = true）
- **AND** 流式响应内容正常返回

#### Scenario: 空内容估算
- **WHEN** 流式调用返回空响应
- **THEN** `EstimateTokenCount(null)` 返回 0，不抛异常

### Requirement: 流式调用指标记录
系统 SHALL 在流式调用完成后，将 `UsageDetails` 信息与延迟、成功状态等记录到 `LlmCallMetrics` 表。

#### Scenario: 流式成功调用指标持久化
- **WHEN** 流式调用成功完成
- **THEN** 系统创建 `LlmCallMetric` 记录，从 `UsageDetails` 提取 Token 数据
- **AND** 记录 `IsStreaming = true`、`FirstTokenLatencyMs`、`IsEstimated` 字段

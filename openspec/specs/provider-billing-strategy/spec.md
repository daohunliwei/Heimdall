## MODIFIED Requirements

### Requirement: Provider 响应标准化
系统 SHALL 使用 MEAI 的 `ChatResponse` 和 `UsageDetails` 作为统一的 LLM 响应模型。**变更**：废弃自研 `ChatCompletionResponse` 和 `TokenUsage`，改为 MEAI 标准类型。`UsageDetails.AdditionalCounts` 用于标记 `IsEstimated` 等扩展信息。

#### Scenario: ChatResponse 替代自研模型
- **WHEN** 任意 LLM 调用完成
- **THEN** 返回 `ChatResponse` 对象，Content 通过 `response.Messages.Last().Text` 获取
- **AND** Token 统计通过 `response.Usage.InputTokenCount`、`OutputTokenCount`、`CachedInputTokenCount` 获取

#### Scenario: 流式调用 Token 估算标记
- **WHEN** 流式 LLM 调用完成且 API 未返回 usage
- **THEN** 系统使用 TokenCounter 估算并设置 `UsageDetails.AdditionalCounts["IsEstimated"] = true`

#### Scenario: 流式调用 API 返回 usage
- **WHEN** 流式 LLM 调用的最后一个 chunk 包含 usage 字段
- **THEN** 系统优先使用 API 返回的 usage 值

### Requirement: Provider 计费模型元数据
系统 SHALL 为每个 Provider/Model 组合维护计费模型元数据。**变更**：元数据字段新增 `SupportsStreaming`（是否支持流式，默认 true），用于 Ask 接口判断是否可使用流式端点。新增 `RawEndpoint`（OpenAI 兼容 API 的自定义 endpoint URL）。

#### Scenario: 流式支持标记
- **WHEN** 系统加载 Provider 元数据
- **THEN** `SupportsStreaming` 字段指示该 Provider/Model 是否支持流式输出

#### Scenario: OpenAI 兼容 Provider 自定义 endpoint
- **WHEN** Provider 为 OpenAI 兼容 API（OpenRouter / DashScope / DeepSeek）
- **THEN** `RawEndpoint` 字段存储自定义 API 端点 URL，用于 `OpenAIClient` 初始化时覆盖默认 endpoint

## ADDED Requirements

### Requirement: 流式调用计费策略
系统 SHALL 对流式 LLM 调用应用与非流式相同的计费策略（TokenPlan / CodingPlan），Token 消耗以 `UsageDetails` 的估算值或实际值为准。

#### Scenario: 流式 TokenPlan 计费
- **WHEN** 模型 BillingType 为 TokenPlan 且使用流式调用
- **THEN** 系统流式完成后以估算（或实际）的 `InputTokenCount` 和 `OutputTokenCount` 计算费用

#### Scenario: 流式 CodingPlan 计费
- **WHEN** 模型 BillingType 为 CodingPlan 且使用流式调用
- **THEN** 系统按调用次数计费，Token 估算值仅用于记录

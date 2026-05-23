## ADDED Requirements

### Requirement: OllamaSharp + IChatClient 适配器
系统 SHALL 基于 `OllamaSharp` 库实现 `OllamaChatClient : IChatClient`，替代当前的 `OllamaChatProvider`。不使用已废弃的 `Microsoft.Extensions.AI.Ollama` 包。

#### Scenario: 非流式 Ollama 调用
- **WHEN** 调用 `OllamaChatClient.GetResponseAsync(messages, options, ct)`
- **THEN** 使用 `OllamaSharp` 的 `IOllamaApiClient.ChatAsync()` 方法
- **AND** 将 MEAI 的 `ChatMessage[]` 转为 Ollama 的消息格式
- **AND** 返回 `ChatResponse` 包含 `Usage`（如有 `total_duration` 等 Ollama 元数据，填入 `AdditionalCounts`）

#### Scenario: 流式 Ollama 调用
- **WHEN** 调用 `OllamaChatClient.GetStreamingResponseAsync(messages, options, ct)`
- **THEN** 使用 `OllamaSharp` 的流式 `ChatAsync()` 方法
- **AND** 每个增量 chunk 通过 `yield return new ChatResponseUpdate { Text = delta }` 返回
- **AND** 最后一个 chunk 携带结束信号

### Requirement: Google Gemini IChatClient 适配器
系统 SHALL 基于 `Google.GenerativeAI` SDK（或手写 HttpClient）实现 `GeminiChatClient : IChatClient`，替代当前的 `GoogleChatProvider`。

#### Scenario: 非流式 Gemini 调用
- **WHEN** 调用 `GeminiChatClient.GetResponseAsync(messages, options, ct)`
- **THEN** 将 `ChatMessage[]` 转为 Gemini 的 `contents` 格式（role 映射：system → systemInstruction，user/assistant → contents）
- **AND** POST 到 `https://generativelanguage.googleapis.com/v1/models/{model}:generateContent`
- **AND** 解析响应中 `candidates[0].content.parts[0].text`
- **AND** 从 `usageMetadata` 提取 `promptTokenCount` / `candidatesTokenCount` / `totalTokenCount`

#### Scenario: 流式 Gemini 调用
- **WHEN** 调用 `GeminiChatClient.GetStreamingResponseAsync(messages, options, ct)`
- **THEN** POST 到 `streamGenerateContent` 端点
- **AND** 逐行读取 SSE 流，解析 JSON 提取 `candidates[0].content.parts[0].text` 增量
- **AND** 通过 `yield return new ChatResponseUpdate { Text = delta }` 返回

### Requirement: MiniMax IChatClient 适配器
系统 SHALL 实现 `MiniMaxChatClient : IChatClient`，替代当前的 `MiniMaxChatProvider`。

#### Scenario: 非流式 MiniMax 调用
- **WHEN** 调用 `MiniMaxChatClient.GetResponseAsync(messages, options, ct)`
- **THEN** POST 到 MiniMax Chat Completion API
- **AND** 解析 `choices[0].message.content`
- **AND** 从 `usage` 提取 `prompt_tokens`（→ InputTokenCount）、`completion_tokens`（→ OutputTokenCount）
- **AND** 从 `usage.prompt_tokens_details.cached_tokens` 或 `usage.cache_read_input_tokens` 提取 → CachedInputTokenCount

#### Scenario: 流式 MiniMax 调用
- **WHEN** 调用 `MiniMaxChatClient.GetStreamingResponseAsync(messages, options, ct)`
- **THEN** 请求体中 `"stream": true`
- **AND** 解析 SSE 事件流，提取 `choices[0].delta.content` 增量
- **AND** 最后一个 chunk 如有 usage 信息，填充到 `ChatResponseUpdate.AdditionalProperties`

### Requirement: 自定义 Backend 的 ChatOptions 扩展参数传递
系统 SHALL 通过 `ChatOptions.AdditionalProperties` 传递 Provider 特有的扩展参数（如 DeepSeek 的 `thinking`、MiniMax 的 `max_completion_tokens` 等），自定义 `IChatClient` 适配器负责读取并映射到 API 请求。

#### Scenario: DeepSeek thinking 参数传递
- **WHEN** 业务层设置 `ChatOptions.AdditionalProperties["thinking"] = new { type = "enabled" }`
- **THEN** OpenAI Backend 将该属性映射为 API 请求体中的 `"thinking": { "type": "enabled" }`

#### Scenario: MiniMax max_completion_tokens 传递
- **WHEN** 业务层设置 `ChatOptions.MaxOutputTokens = 196608`
- **THEN** `MiniMaxChatClient` 将该值映射为 API 请求体中的 `"max_completion_tokens": 196608`

### Requirement: 自定义 Backend 的 DI 集成
每个自定义 `IChatClient` 适配器 SHALL 通过工厂方法注册到 DI 容器，并被 `ChatClientBuilder` 中间件管道包裹。

#### Scenario: OllamaChatClient DI 注册
- **WHEN** 应用启动
- **THEN** `OllamaChatClient` 作为 `IChatClient` 的 Singleton 实例注册
- **AND** 通过 `ChatClientBuilder` 包裹 OpenTelemetry 和重试中间件
- **AND** 最终的 `IChatClient` 注册到 DI 容器供 `ProviderRegistry` 或等效服务使用

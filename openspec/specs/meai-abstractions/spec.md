## ADDED Requirements

### Requirement: 废弃自研 IChatProvider，迁移到 IChatClient
系统 SHALL 废弃 `Heimdall.Infrastructure.Providers.IChatProvider` 接口及其 `GenerateAsync` / `GenerateWithMetricsAsync` 方法，所有 Provider 实现类改为实现 `Microsoft.Extensions.AI.IChatClient` 接口。

#### Scenario: IChatClient 替代 IChatProvider
- **WHEN** 任意 Provider 类完成迁移
- **THEN** 该类实现 `IChatClient.GetResponseAsync(IEnumerable<ChatMessage>, ChatOptions?, CancellationToken)` 非流式方法
- **AND** 该类实现 `IChatClient.GetStreamingResponseAsync(IEnumerable<ChatMessage>, ChatOptions?, CancellationToken)` 流式方法
- **AND** 删除原 `IChatProvider` 的实现代码

#### Scenario: ChatMessage 替代 ProviderChatRequest
- **WHEN** 业务层发起 LLM 调用
- **THEN** 使用 `List<ChatMessage>` 传递对话历史，`ChatMessage` 支持 `ChatRole.System` / `ChatRole.User` / `ChatRole.Assistant`
- **AND** 使用 `ChatOptions` 传递 Temperature、MaxOutputTokens、TopP、TopK、ModelId 等参数

#### Scenario: ChatResponse 替代自研响应模型
- **WHEN** Provider 返回非流式响应
- **THEN** 返回 `ChatResponse` 对象，包含 `Messages`（响应消息列表）、`Usage`（UsageDetails）、`FinishReason`、`ModelId`

### Requirement: OpenAI 兼容 Provider 统一使用 Microsoft.Extensions.AI.OpenAI
系统 SHALL 对 OpenAI、OpenRouter、DashScope、DeepSeek 四个 OpenAI 兼容 API 的 Provider 统一使用 `Microsoft.Extensions.AI.OpenAI` 包中的 `OpenAIClient`。Azure OpenAI 使用 `AzureOpenAIClient`。

#### Scenario: OpenAI Provider 迁移
- **WHEN** 系统初始化 OpenAI Provider 的 `IChatClient`
- **THEN** 使用 `new OpenAIClient(apiKey).GetChatClient(model)` 或自定义 endpoint 的 `OpenAIClientOptions`
- **AND** 通过 `ChatClientBuilder` 注册到 DI 容器

#### Scenario: OpenRouter Provider 迁移
- **WHEN** 系统初始化 OpenRouter Provider 的 `IChatClient`
- **THEN** 使用 `OpenAIClient` 配合 `OpenAIClientOptions { Endpoint = new Uri("https://openrouter.ai/api/v1") }`
- **AND** 添加 `HTTP-Referer` 和 `X-Title` 头到 HttpClient

#### Scenario: DashScope Provider 迁移
- **WHEN** 系统初始化 DashScope Provider 的 `IChatClient`
- **THEN** 使用 `OpenAIClient` 配合阿里云 DashScope endpoint
- **AND** 添加 `X-DashScope-WorkSpace` 头（如有配置）

#### Scenario: DeepSeek Provider 迁移
- **WHEN** 系统初始化 DeepSeek Provider 的 `IChatClient`
- **THEN** 使用 `OpenAIClient` 配合 `https://api.deepseek.com/v1` endpoint
- **AND** 通过 `ChatOptions.AdditionalProperties` 传递 `thinking` 相关配置

#### Scenario: Azure OpenAI Provider 迁移
- **WHEN** 系统初始化 Azure Provider 的 `IChatClient`
- **THEN** 使用 `new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey)).GetChatClient(deploymentName)`

### Requirement: AWS Bedrock 使用官方 MEAI 包
系统 SHALL 使用 `AWSSDK.Extensions.Bedrock.MEAI` 包中的 `BedrockChatClient` 替代当前的 `BedrockChatProvider`。

#### Scenario: Bedrock IChatClient 初始化
- **WHEN** 系统初始化 Bedrock 的 `IChatClient`
- **THEN** 使用 `new BedrockChatClient(amazonBedrockRuntimeClient, modelId)` 直接获得 `IChatClient` 实例
- **AND** `AmazonBedrockRuntimeClient` 由 AWS 凭证自动配置

### Requirement: ChatClientBuilder 中间件管道
系统 SHALL 在 `Program.cs` 中使用 `ChatClientBuilder` 构建中间件管道，为所有 `IChatClient` 注册统一添加 OpenTelemetry、日志、重试、速率限制中间件。

#### Scenario: 中间件管道注册
- **WHEN** 应用启动并注册 AI 服务
- **THEN** 系统对每个 `IChatClient` Backend 调用 `new ChatClientBuilder(innerClient).UseOpenTelemetry().UseResilience(pipeline).Build()`
- **AND** 注册到 DI 容器为 `AddSingleton<IChatClient>(sp => builder.Build())`

#### Scenario: OpenTelemetry 自动追踪
- **WHEN** 任意 LLM 调用通过 `IChatClient` 发起
- **THEN** 系统自动创建 Trace span，记录 Provider、Model、InputTokens、OutputTokens、LatencyMs 等属性
- **AND** 无需在业务代码中手动调用指标收集方法

#### Scenario: 重试中间件
- **WHEN** LLM 调用因 HTTP 429 或 5xx 错误失败
- **THEN** 重试中间件自动按 Polly 指数退避策略重试
- **AND** 原 `LlmRetryPolicy` 服务废弃

### Requirement: IChatProvider 向后兼容过渡
系统 SHALL 在 MEAI 迁移期间提供 `ChatProviderToChatClientAdapter` 适配器，将旧 `IChatProvider` 实现包装为 `IChatClient`，确保业务层可以逐步迁移而不阻塞。

#### Scenario: 适配器桥接
- **WHEN** 部分 Provider 尚未来得及实现 `IChatClient`
- **THEN** 适配器将 `IChatProvider.GenerateAsync` 映射为 `IChatClient.GetResponseAsync`
- **AND** 适配器将流式调用回退到非流式 `GenerateAsync` 并包装为单次 `ChatResponseUpdate`
- **AND** 记录 Warning 日志提示该 Provider 尚未完成 MEAI 迁移

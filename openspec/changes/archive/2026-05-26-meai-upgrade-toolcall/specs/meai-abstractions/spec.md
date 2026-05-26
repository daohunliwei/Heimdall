## MODIFIED Requirements

### Requirement: 废弃自研 IChatProvider，迁移到 IChatClient
系统 SHALL 废弃 `Heimdall.Infrastructure.Providers.IChatProvider` 接口及其 `GenerateAsync` / `GenerateWithMetricsAsync` 方法，所有 Provider 实现类改为实现 `Microsoft.Extensions.AI.IChatClient` 接口。**变更**：Provider 注册方式从 `AddKeyedSingleton<IChatClient>` 改为 `AddChatClient()` + `ChatClientBuilder` 管道模式，每个 Provider 管道 SHALL 包含 `UseFunctionInvocation()`、`UseOpenTelemetry()`、`UseLogging()` 中间件。

#### Scenario: ChatClientBuilder 管道注册替代手动注册
- **WHEN** 系统初始化 OpenAI Provider 的 `IChatClient`
- **THEN** 使用 `AddChatClient(pipeline => { var client = new OpenAIClient(...).GetChatClient(...); return client.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().UseLogging().Build(); })`
- **AND** 不再使用 `AddKeyedSingleton<IChatClient>(...)` 手动注册

#### Scenario: ChatMessage 替代 ProviderChatRequest
- **WHEN** 业务层发起 LLM 调用
- **THEN** 使用 `List<ChatMessage>` 传递对话历史
- **AND** 使用 `ChatOptions` 传递 Temperature、MaxOutputTokens、ModelId 及 `Tools` 列表

#### Scenario: ChatResponse 替代自研响应模型
- **WHEN** Provider 返回非流式响应
- **THEN** 返回 `ChatResponse` 对象，包含 `Messages`、`Usage`（UsageDetails）、`FinishReason`、`ModelId`

### Requirement: ChatClientBuilder 中间件管道
系统 SHALL 在 `Program.cs` 中使用 `ChatClientBuilder` 构建中间件管道，为所有 `IChatClient` 注册统一添加 `FunctionInvocation`、`OpenTelemetry`、`Logging` 中间件。**变更**：原 `UseOpenTelemetry()` → `UseResilience()` 管道改为 `UseFunctionInvocation()` → `UseOpenTelemetry()` → `UseLogging()`，`UseResilience()` 由 MEAI 内置 Resilience 替换。

#### Scenario: 新管道顺序
- **WHEN** 应用启动并注册 AI 服务
- **THEN** 系统对每个 `IChatClient` 调用 `innerClient.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().UseLogging().Build()`
- **AND** `UseFunctionInvocation` 在最内层（最先处理 Tool Call 往返）
- **AND** `UseOpenTelemetry` 包含自动 Trace span 和 Metrics 记录
- **AND** `UseLogging` 记录请求/响应日志

#### Scenario: 重试行为由 Resilience 处理
- **WHEN** LLM 调用因 HTTP 429 或 5xx 错误失败
- **THEN** `UseResilience()` 中间件按 Polly 指数退避策略自动重试
- **AND** 原 `LlmRetryPolicy` 服务保留逻辑但改为通过 `UseResilience()` 调用

### Requirement: ChatClientFactory 废弃
系统 SHALL 将 `ChatClientFactory` 标记为 `[Obsolete]`。所有 `IChatClient` 的查找 SHALL 通过 `IServiceProvider.GetKeyedService<IChatClient>(key)` 直接完成。`TaskLlmService` SHALL 改为注入 `[FromKeyedServices(providerId)] IChatClient`。

#### Scenario: 直接 Keyed DI 替代工厂
- **WHEN** `TaskLlmService` 需要 `IChatClient`
- **THEN** 通过 `[FromKeyedServices(providerId)] IChatClient` 注入
- **AND** 不再调用 `_chatClientFactory.GetClient(providerId)`

#### Scenario: ChatClientFactory 过渡兼容
- **WHEN** 其他服务调用 `ChatClientFactory.GetClient(providerId)`
- **THEN** 内部委托给 `_serviceProvider.GetKeyedService<IChatClient>(providerId)`
- **AND** 记录 Obsolete 警告

## REMOVED Requirements

### Requirement: LoggingChatClient 自定义日志包装器
**Reason**: MEAI 10.6.0 内置 `UseLogging()` 中间件提供等效的请求/响应日志功能。
**Migration**: 删除 `ChatClientBuilderExtensions.cs` 中的 `LoggingChatClient` 类，改用 `UseLogging()`。

### Requirement: RetryChatClient 自定义重试包装器
**Reason**: MEAI 10.6.0 的 `UseResilience()` 中间件提供等效的 Polly 重试功能。
**Migration**: 删除 `ChatClientBuilderExtensions.cs` 中的 `RetryChatClient` 类，`LlmRetryPipeline` 逻辑通过 `UseResilience()` 注入。

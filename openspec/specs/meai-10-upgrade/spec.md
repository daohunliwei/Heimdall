## ADDED Requirements

### Requirement: NuGet 包升级到 10.6.0
系统 SHALL 将 `Microsoft.Extensions.AI` 和 `Microsoft.Extensions.AI.OpenAI` 包版本从 `9.4.3-preview.1.25230.7` 升级到 `10.6.0`。升级后所有 9 个 Provider 的非流式 `GetResponseAsync` 和流式 `GetStreamingResponseAsync` 调用 SHALL 保持正常工作。

#### Scenario: 所有 Provider 非流式调用正常
- **WHEN** 升级完成后对每个 Provider 发起非流式 `GetResponseAsync` 调用
- **THEN** 所有 9 个 Provider（OpenAI、OpenRouter、DashScope、DeepSeek、Azure、Bedrock、Ollama、Gemini、MiniMax）均返回有效的 `ChatResponse`
- **AND** `ChatResponse.Usage` 包含 InputTokenCount 和 OutputTokenCount（Provider 支持的情况下）

#### Scenario: 所有 Provider 流式调用正常
- **WHEN** 升级完成后对每个 Provider 发起流式 `GetStreamingResponseAsync` 调用
- **THEN** 所有 9 个 Provider 均逐块返回 `ChatResponseUpdate`
- **AND** 最后一个 `ChatResponseUpdate` 的 `UsageDetails` 包含 Token 统计（Provider 支持的情况下）

### Requirement: ChatClientBuilder 管道注册
系统 SHALL 在 `Program.cs` 中使用 `ChatClientBuilder` 管道模式注册所有 IChatClient，替代手动 `AddKeyedSingleton<IChatClient>` 注册。每个 Provider 的管道 SHALL 包含 `UseOpenTelemetry()`（追踪）、`UseLogging()`（日志）、`UseFunctionInvocation()`（Tool Call 支持）。

#### Scenario: OpenAI 管道注册
- **WHEN** 应用启动并注册 OpenAI Provider
- **THEN** 系统使用 `OpenAIClient` 创建 Inner Client
- **AND** 通过 `innerClient.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().UseLogging().Build()` 构建管道
- **AND** 构建后的 `IChatClient` 注册到 DI 容器，Key 为 `"openai"`

#### Scenario: 管道顺序一致性
- **WHEN** 多个 Provider 注册到 DI
- **THEN** 每个 Provider 使用相同的管道构建顺序：`FunctionInvocation → OpenTelemetry → Logging`
- **AND** `UseFunctionInvocation` 位于最内层（最先处理 Tool Call，然后传递给外层的追踪和日志）

#### Scenario: 不支持 Function Calling 的 Provider
- **WHEN** Ollama 或 Gemini Provider 的模型不支持原生 Function Calling
- **THEN** `UseFunctionInvocation()` 仍然注册（不抛异常）
- **AND** LLM 在收到 Tool 列表时自动忽略或返回不支持错误
- **AND** `FunctionInvokingChatClient` 因没有 `FunctionCallContent` 而直接透传响应

### Requirement: ChatClientFactory 废弃
系统 SHALL 将 `ChatClientFactory` 标记为 `[Obsolete]`，所有 `IChatClient` 的查找 SHALL 通过 `IServiceProvider.GetKeyedService<IChatClient>(key)` 直接完成。`TaskLlmService` SHALL 改为注入 `[FromKeyedServices(providerId)] IChatClient`。

#### Scenario: TaskLlmService 直接注入 IChatClient
- **WHEN** `TaskLlmService` 使用 `providerId = "deepseek"` 发起 LLM 调用
- **THEN** 系统通过 `[FromKeyedServices("deepseek")] IChatClient` 直接获取构建好的管道客户端
- **AND** 不再经过 `ChatClientFactory.GetClient("deepseek")`

#### Scenario: ChatClientFactory 过渡兼容
- **WHEN** 其他服务（如 `AskTaskService`）仍调用 `ChatClientFactory.GetClient(providerId)`
- **THEN** `ChatClientFactory.GetClient` 内部委托给 `_serviceProvider.GetKeyedService<IChatClient>(providerId)`
- **AND** 记录 Warning 日志提示使用 `[Obsolete]` API

### Requirement: 废弃自研日志和重试包装器
系统 SHALL 删除 `ChatClientBuilderExtensions.cs` 中的 `LoggingChatClient` 和 `RetryChatClient` 内部类，改为使用 MEAI 10.6.0 内置的 `UseOpenTelemetry()` 和 `UseLogging()` 中间件。`LlmRetryPipeline` 服务已移除（MEAI 10.6.0 暂不包含内置 `UseResilience()` 中间件，重试逻辑留待 `Microsoft.Extensions.AI.Resilience` 包发布后集成）。

#### Scenario: OpenTelemetry 自动追踪替代自定义日志
- **WHEN** 任意 LLM 调用通过管道发起
- **THEN** `UseOpenTelemetry()` 自动创建 Trace span，记录 `gen_ai.system`、`gen_ai.request.model`、`gen_ai.usage.input_tokens` 等标准属性
- **AND** `UseLogging()` 自动记录请求/响应日志
- **AND** 不再依赖 `LoggingChatClient` 类

#### Scenario: Provider 层重试仍由底层客户端处理
- **WHEN** LLM 调用因 HTTP 429 失败
- **THEN** 底层 `OpenAIClient` / HttpClient 的内置重试策略处理瞬时错误
- **AND** 管道中间件不重复实现重试逻辑
- **AND** 待 `Microsoft.Extensions.AI.Resilience` 包发布后可通过 `UseResilience()` 统一管理

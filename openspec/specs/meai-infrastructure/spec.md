## Purpose

统一 MEAI（Microsoft.Extensions.AI）基础设施——涵盖 IChatClient 抽象与 Keyed DI、ChatClientBuilder 管道、自定义后端适配器（Ollama/Gemini/MiniMax）、10.6.0 版本升级及 Tool Call 支持。
## Requirements
### Requirement: IChatClient 作为唯一 AI 抽象
系统 SHALL 以 `Microsoft.Extensions.AI.IChatClient` 作为唯一聊天抽象，通过 Keyed DI 获取所有 Provider 客户端。`ChatClientFactory` 已废弃，Provider 生命周期完全由 DI 容器管理。

#### Scenario: 动态 Provider 通过 Keyed DI 获取
- **WHEN** ChatController、AskTaskService 或 TaskLlmService 需要根据 providerId 获取聊天客户端
- **THEN** 系统通过 `IServiceProvider.GetRequiredKeyedService<IChatClient>(providerId)` 获取
- **AND** 若指定 Key 未注册，立即抛出配置错误，不回退到默认客户端

#### Scenario: ChatClientFactory 不再参与运行时链路
- **WHEN** 系统编译并运行
- **THEN** 生产代码中不存在对 ChatClientFactory 的注入和调用

### Requirement: ChatClientBuilder 管道注册
系统 SHALL 在 `Program.cs` 中使用 `ChatClientBuilder` 管道模式注册所有 IChatClient。每个 Provider 的管道 SHALL 包含 `UseFunctionInvocation()` → `UseOpenTelemetry()` → `UseLogging()`（由内到外）。

#### Scenario: Provider 注册方式收敛
- **WHEN** 应用启动并注册多个 Provider
- **THEN** 各 Provider 通过统一的 Builder 组装辅助逻辑构建 IChatClient
- **AND** 管道构建顺序一致：FunctionInvocation → OpenTelemetry → Logging

### Requirement: 业务层使用结构化消息
系统 SHALL 以 `IReadOnlyList<ChatMessage>` + `ChatOptions` 作为业务层调用 IChatClient 的主模型。`ChatOptions` 负责承载 ModelId、MaxOutputTokens、Temperature 与 Tools。

#### Scenario: 结构化消息成为主入口
- **WHEN** 业务层发起 LLM 调用
- **THEN** 主入口接收结构化消息列表与 ChatOptions
- **AND** 多轮历史不再被强制折叠为单条用户消息

### Requirement: 自定义适配器 IChatClient 实现
每个自定义 IChatClient 适配器（OllamaChatClient、GeminiChatClient、MiniMaxChatClient）SHALL 实现 IChatClient 接口的全部成员，包括 GetService<T>() 方法和 Metadata 属性。

#### Scenario: GetService 与 Metadata
- **WHEN** 调用 `OllamaChatClient.GetService<ChatClientMetadata>()`
- **THEN** 返回 ChatClientMetadata 包含 ProviderName 和 ModelId
- **AND** Metadata 属性返回相同信息

#### Scenario: 非流式 Ollama 调用
- **WHEN** 调用 `OllamaChatClient.GetResponseAsync(messages, options, ct)`
- **THEN** 使用 OllamaSharp 的 `IOllamaApiClient.ChatAsync()` 方法，返回包含 Usage 的 ChatResponse

#### Scenario: Gemini 非流式调用
- **WHEN** 调用 `GeminiChatClient.GetResponseAsync`
- **THEN** 将 MEAI ChatMessage 转为 Gemini API 格式，POST 到 Gemini Chat API

#### Scenario: MiniMax 非流式调用
- **WHEN** 调用 `MiniMaxChatClient.GetResponseAsync`
- **THEN** POST 到 MiniMax Chat Completion API，解析 choices[0].message.content

### Requirement: 自定义适配器 Tool Call 支持检测
每个自定义适配器 SHALL 在 GetResponseAsync / GetStreamingResponseAsync 中检测 ChatOptions.Tools 是否非空。若底层 Provider 不支持原生 Function Calling，SHALL 记录 Warning 日志并忽略工具列表，确保 FunctionInvokingChatClient 中间件可以安全透传。

#### Scenario: Ollama 工具调用降级
- **WHEN** OllamaChatClient 收到 ChatOptions.Tools 非空但模型不支持 Tool Call
- **THEN** 记录 Warning 并忽略工具列表，正常发起 LLM 调用

#### Scenario: Gemini 工具调用支持
- **WHEN** GeminiChatClient 收到 ChatOptions.Tools 非空且模型支持 Function Calling
- **THEN** 将 AIFunction 列表转换为 Gemini API 的 tools.functionDeclarations 格式

### Requirement: NuGet 包升级到 10.6.0
系统 SHALL 使用 `Microsoft.Extensions.AI` 和 `Microsoft.Extensions.AI.OpenAI` 10.6.0 版本。所有 9 个 Provider 的非流式和流式调用 SHALL 保持正常工作。

#### Scenario: 所有 Provider 调用正常
- **WHEN** 升级完成后对每个 Provider 发起调用
- **THEN** 所有 9 个 Provider 均返回有效的 ChatResponse，Usage 包含 Token 统计

### Requirement: 废弃自研日志和重试包装器
系统 SHALL 删除自定义 LoggingChatClient 和 RetryChatClient，改为使用 MEAI 内置的 UseOpenTelemetry() 和 UseLogging() 中间件。

#### Scenario: OpenTelemetry 自动追踪
- **WHEN** 任意 LLM 调用通过管道发起
- **THEN** UseOpenTelemetry() 自动创建 Trace span，UseLogging() 自动记录请求/响应日志
- **AND** 不再依赖自定义 LoggingChatClient 类

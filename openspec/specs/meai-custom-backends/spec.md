## ADDED Requirements

### Requirement: IChatClient 10.6.0 API 适配
每个自定义 `IChatClient` 适配器（`OllamaChatClient`、`GeminiChatClient`、`MiniMaxChatClient`）SHALL 实现 10.6.0 `IChatClient` 接口的全部成员，包括 `GetService<T>(object? key)` 方法和 `Metadata` 属性。

#### Scenario: GetService 默认实现
- **WHEN** 调用 `OllamaChatClient.GetService<ChatClientMetadata>()`
- **THEN** 返回 `new ChatClientMetadata { ProviderName = "Ollama", ... }`
- **AND** 对于未知的 `T` 类型，返回 `null`

#### Scenario: Metadata 属性
- **WHEN** 读取 `OllamaChatClient.Metadata`
- **THEN** 返回 `ChatClientMetadata` 对象，包含 `ProviderName = "Ollama"`、`ModelId = 当前模型ID`

### Requirement: 自定义适配器 Tool Call 支持检测
每个自定义适配器 SHALL 在 `GetResponseAsync` / `GetStreamingResponseAsync` 中检测 `ChatOptions.Tools` 是否非空。若适配器底层 Provider 不支持原生 Function Calling，SHALL 记录 Warning 日志并忽略工具列表（不抛异常），确保 `FunctionInvokingChatClient` 中间件可以安全透传。

#### Scenario: Ollama Provider 工具调用降级
- **WHEN** `OllamaChatClient.GetResponseAsync` 收到 `ChatOptions.Tools` 非空但 Ollama 模型不支持 Tool Call
- **THEN** 记录 Warning：`Ollama 不支持 Tool Call，忽略工具列表`
- **AND** 正常发起 LLM 调用（不含 Tools 参数）
- **AND** 不抛出异常

#### Scenario: Gemini Provider 工具调用支持
- **WHEN** `GeminiChatClient.GetResponseAsync` 收到 `ChatOptions.Tools` 非空且 Gemini 模型支持 Function Calling
- **THEN** 将 `AIFunction` 列表转换为 Gemini API 的 `tools.functionDeclarations` 格式
- **AND** 正常发起 LLM 调用并处理 `functionCall` 响应

## MODIFIED Requirements

### Requirement: OllamaSharp + IChatClient 适配器
系统 SHALL 基于 `OllamaSharp` 库实现 `OllamaChatClient : IChatClient`。额外实现 10.6.0 新增的 `GetService<T>()` 方法和 `Metadata` 属性；检测并处理 `ChatOptions.Tools`。

#### Scenario: 非流式 Ollama 调用
- **WHEN** 调用 `OllamaChatClient.GetResponseAsync(messages, options, ct)`
- **THEN** 使用 `OllamaSharp` 的 `IOllamaApiClient.ChatAsync()` 方法
- **AND** 将 MEAI 的 `ChatMessage[]` 转为 Ollama 的消息格式
- **AND** 返回 `ChatResponse` 包含 `Usage`

#### Scenario: Tool Call 检测与降级（新增）
- **WHEN** `ChatOptions.Tools` 非空且 Ollama 模型支持 Tool Call
- **THEN** 转换为 Ollama Chat API 的 `tools` 参数
- **AND** 若模型不支持，记录 Warning 并忽略工具列表

### Requirement: Google Gemini IChatClient 适配器
系统 SHALL 实现 `GeminiChatClient : IChatClient`。额外实现 `GetService<T>()`、`Metadata`；支持 Gemini 原生 Function Calling。

#### Scenario: Tool Call 支持（新增）
- **WHEN** `ChatOptions.Tools` 非空
- **THEN** 转换为 Gemini API 的 `tools[].functionDeclarations` 格式
- **AND** 解析响应中的 `functionCall` 并包装为 `FunctionCallContent`

### Requirement: MiniMax IChatClient 适配器
系统 SHALL 实现 `MiniMaxChatClient : IChatClient`。额外实现 `GetService<T>()` 和 `Metadata`。

#### Scenario: 非流式 MiniMax 调用
- **WHEN** 调用 `MiniMaxChatClient.GetResponseAsync(messages, options, ct)`
- **THEN** POST 到 MiniMax Chat Completion API
- **AND** 解析 `choices[0].message.content`
- **AND** 提取 usage 信息

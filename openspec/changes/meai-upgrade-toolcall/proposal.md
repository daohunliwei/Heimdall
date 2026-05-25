## Why

当前项目使用 `Microsoft.Extensions.AI` 9.4.3-preview 版本，仅用其 `IChatClient` 基础抽象，未触及 Tool Call、管道中间件、DI 集成等正式版核心能力。MEAI 10.6.0 正式版已发布，其内置的 `FunctionInvokingChatClient` 可自动处理 Tool Call 往返逻辑，`ChatClientBuilder` 管道模式可统一注册 OpenTelemetry、分布式缓存、函数调用等中间件。直接升级并利用这些内置能力，可让我们原计划的 Tool Call 改动从 ~150 行手动循环缩减为 ~5 行管道配置，同时获得日志、追踪、对话压缩等额外能力。

## What Changes

- **升级 MEAI NuGet 包**：`Microsoft.Extensions.AI` 和 `Microsoft.Extensions.AI.OpenAI` 从 `9.4.3-preview.1.25230.7` 升级到 `10.6.0`（**BREAKING**：需验证自定义适配器兼容性）
- **重构 Provider 注册为 ChatClientBuilder 管道**：`Program.cs` 中 9 个 Provider 的 `AddKeyedSingleton<IChatClient>` 改为 `AddChatClient()` + `UseFunctionInvocation()` + `UseOpenTelemetry()` + `UseLogging()` 管道模式
- **废弃 ChatClientFactory**：原 `ChatClientFactory` 的缓存和查找逻辑被 `IServiceProvider.GetKeyedService<IChatClient>()` 替代，`TaskLlmService` 改为直接注入 Keyed `IChatClient`
- **利用 FunctionInvokingChatClient 替代手动 Tool Call 实现**：删除此前设计的 `GenerateWithToolsAsync` 手动往返循环，改为在 `ChatClientBuilder` 上调用 `.UseFunctionInvocation()`，MEAI 自动处理往返
- **弃用自研 LoggingChatClient / RetryChatClient 包装器**：`ChatClientBuilderExtensions.cs` 中的自定义中间件由 `UseOpenTelemetry()` 和 `UseResilience()` 替代
- **新增 Wiki 生成专用工具集**：在 `Heimdall.Core/Tools/` 下创建 `ReadCodeFileTool`、`SearchSymbolsTool`、`QueryCallGraphTool`、`RetrieveClassDefinitionTool`
- **Stage 3/5 绑定 Tool Call**：代码理解和页面生成阶段通过 `ChatOptions.Tools` 注入对应工具集
- **激活 AgentOrchestratorService**：集成到 `WikiTaskService` 大仓库分支
- **OllamaChatClient / GeminiChatClient 适配 10.6.0 API**：补充 `GetService<T>()` 方法和 `Metadata` 属性

## Capabilities

### New Capabilities

- `meai-10-upgrade`: MEAI 9.4.3-preview → 10.6.0 升级与 Provider 注册重构——ChatClientBuilder 管道替代手动 AddSingleton，废弃 ChatClientFactory
- `function-invoking-client`: FunctionInvokingChatClient 集成——利用 MEAI 内置 Tool Call 自动往返替代手动实现，统一 Tool Call 行为的日志、追踪、重试
- `wiki-generation-tools`: Wiki 生成专用工具集——ReadCodeFile、SearchSymbols、QueryCallGraph、RetrieveClassDefinition
- `agent-orchestrator-activation`: AgentOrchestratorService 激活——将已注册但未调用的服务集成到 Wiki 生成管线

### Modified Capabilities

- `meai-abstractions`: Provider 注册方式变更——`AddKeyedSingleton<IChatClient>` → `AddChatClient().Use*().Build()` 管道模式；`ChatClientFactory` 废弃，`TaskLlmService` 改为直接注入 `IChatClient`
- `meai-custom-backends`: `OllamaChatClient` 和 `GeminiChatClient` 需适配 10.6.0 `IChatClient` 接口的新成员（`GetService<T>`、`Metadata`），并实现 Tool Call 支持或标记降级
- `deep-code-understanding`: Stage 3 LLM 调用绑定 `QueryCallGraph` + `RetrieveClassDefinition` 工具，通过 `ChatOptions.Tools` 注入
- `wiki-generation-pipeline`: Stage 5 LLM 调用绑定 `ReadCodeFile` + `SearchSymbols` 工具；`WikiTaskService` 新增 Orchestrator 分支
- `llm-observability`: 自研 `LoggingChatClient` / `RetryChatClient` 包装器弃用，由 `UseOpenTelemetry()` / `UseLogging()` 内置中间件替代

## Impact

- **NuGet 包升级**：`Microsoft.Extensions.AI` 和 `Microsoft.Extensions.AI.OpenAI` 版本号变更；可能需要新增 `Microsoft.Extensions.AI.Abstractions` 显式依赖
- **后端 `Heimdall.Api`**：`Program.cs` AI 服务注册段重构（~60 行变更为 ChatClientBuilder 管道）
- **后端 `Heimdall.Infrastructure`**：`ChatClientFactory` 废弃（可保留兼容过渡）；`ChatClientBuilderExtensions` 中自研 Logging/Retry 包装器弃用；`OllamaChatClient` / `GeminiChatClient` 适配新 API
- **后端 `Heimdall.Core`**：`TaskLlmService` 改为直接注入 Keyed `IChatClient`（替代 `ChatClientFactory`）；新增 `Tools/` 目录
- **破坏性变更**：`ChatClientFactory` 公开 API 废弃（标记 `[Obsolete]`）；自研 `LoggingChatClient` / `RetryChatClient` 移除（内部类，无公开 API）
- **数据库**：新增 `ToolCall.Enabled`、`ToolCall.Stage3.Enabled`、`ToolCall.Stage5.Enabled` 三个 SystemSetting 配置项

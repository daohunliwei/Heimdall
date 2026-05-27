## MODIFIED Requirements

### Requirement: 废弃自研 IChatProvider，迁移到 IChatClient
系统 SHALL 继续以 `Microsoft.Extensions.AI.IChatClient` 作为唯一聊天抽象。所有运行时 Provider 获取 SHALL 统一通过 Keyed DI 完成，禁止继续依赖 `ChatClientFactory`、默认客户端 fallback 或其他并行解析方式。

#### Scenario: 动态 Provider 通过 Keyed DI 获取
- **WHEN** `ChatController`、`AskTaskService` 或 `TaskLlmService` 需要根据 `providerId` 获取聊天客户端
- **THEN** 系统通过 `IServiceProvider.GetRequiredKeyedService<IChatClient>(providerId)` 获取
- **AND** 若指定 Key 未注册，立即抛出配置错误
- **AND** 不再回退到默认 `IChatClient`

#### Scenario: `ChatClientFactory` 不再参与运行时链路
- **WHEN** 系统编译并运行最新版本
- **THEN** 生产代码中不存在对 `ChatClientFactory` 的注入和调用
- **AND** Provider 生命周期完全由 DI 容器管理

### Requirement: ChatClientBuilder 中间件管道
系统 SHALL 继续在 `Program.cs` 中使用统一的 `ChatClientBuilder` 管道为所有 Provider 构建 `IChatClient`，并保持 `UseFunctionInvocation()`、`UseOpenTelemetry()`、`UseLogging()` 的官方用法。Telemetry 能力仅保持当前接入，不作为本次变更扩展目标。

#### Scenario: Provider 注册方式收敛
- **WHEN** 应用启动并注册多个 Provider
- **THEN** 各 Provider 通过统一的 Builder 组装辅助逻辑构建 `IChatClient`
- **AND** `ModelId`、`Tools`、流式与非流式路径使用一致的配置约定
- **AND** 不再在不同 Provider 之间保留明显分叉的注册模式

## ADDED Requirements

### Requirement: 业务层使用结构化消息与 `ChatOptions`
系统 SHALL 以 `IReadOnlyList<ChatMessage>` + `ChatOptions` 作为业务层调用 `IChatClient` 的主模型。字符串 Prompt 仅可作为一层便捷包装存在，不得再成为多轮对话与证据注入的主组织方式。

#### Scenario: 结构化消息成为主入口
- **WHEN** 业务层发起一次 Chat、Ask 或 Wiki 相关 LLM 调用
- **THEN** 主入口接收结构化消息列表与 `ChatOptions`
- **AND** `ChatOptions` 负责承载 `ModelId`、`MaxOutputTokens`、`Temperature` 与 `Tools`
- **AND** 多轮历史不再被强制折叠为单条用户消息

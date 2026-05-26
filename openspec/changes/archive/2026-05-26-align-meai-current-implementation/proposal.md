## Why

当前 `Heimdall` 的 MEAI 相关实现已经进入“可运行但尚未收口”的状态：文档与注释仍大量描述 `BM25 + pgvector`、混合检索和向量链路，但代码主实现已经回到 `BM25` 检索；`ChatClientFactory` 仍在 `ChatController` 和 `AskTaskService` 中保留调用，导致 Keyed DI 与工厂双轨并存；`Chat` 与 `Ask` 对话链路倾向于把多轮上下文压扁为一个大 Prompt，无法充分发挥 `Microsoft.Extensions.AI` 的多角色消息建模、Tool Call 与 `ChatOptions` 官方编程模型。

这类“不一致”会持续误导后续改造：后来者会以为当前系统仍然依赖向量检索，或继续沿用大 Prompt 拼装方式，从而放大架构债务。本次改造的核心目标不是增加新能力，而是以现有代码真实能力为准，完成一次 MEAI 官方用法的收口升级，为后续功能演进打稳基础。

## What Changes

- **按代码对齐文档与注释**：统一把架构文档、运行时说明、OpenSpec 规格和核心代码注释调整为当前真实实现，明确当前检索主链路为 `BM25`，本次不引入向量检索、不扩展 Telemetry。
- **彻底移除 `ChatClientFactory` 运行时依赖**：所有动态 Provider 获取统一使用 `IServiceProvider.GetRequiredKeyedService<IChatClient>(providerId)`，仅保留 Keyed DI 模式，不再保留工厂过渡层和默认客户端兜底。
- **重构 Chat / Ask 的消息建模**：把当前“历史对话 + 证据 + 约束”压成单条用户消息的实现，改为基于 `List<ChatMessage>` 的多角色消息链路，保留 `system / user / assistant / tool` 的职责边界。
- **统一 Tool Call 配置入口**：`ToolCallConfigurationService` 成为唯一的 `ToolCall.*` 配置读取与阶段判定入口，调用侧不再自行读取 `SystemSetting`。
- **按官方推荐收敛 MEAI 用法**：统一 Provider 注册、消息构建、`ChatOptions` 传递、流式与非流式调用方式；自定义后端适配器同步收敛到一致的角色映射和 `ChatOptions` 处理逻辑。
- **明确非目标**：本次不恢复 `pgvector` / Embedding / 向量召回，不继续扩展 OpenTelemetry / exporter / dashboard。

## Capabilities

### New Capabilities

- `chat-message-modeling`: 基于 `Microsoft.Extensions.AI.ChatMessage` 的多角色消息建模，覆盖 Chat、Ask 与工具调用前后的消息组织方式。

### Modified Capabilities

- `meai-abstractions`: 统一为 Keyed DI 获取 `IChatClient`，删除 `ChatClientFactory` 运行时依赖，并收敛 Provider 注册与 `ChatOptions` 用法。
- `function-invoking-client`: Tool Call 开关读取与阶段判断统一走 `ToolCallConfigurationService`，避免重复实现和配置漂移。
- `wiki-generation-pipeline`: 以当前代码实现为准，统一描述为 8 阶段主流程与 `BM25` 检索注入，不再把向量链路写成现状能力。
- `hybrid-code-retrieval`: 重新定义当前阶段的检索能力边界，明确现状为 `BM25` 主导检索，并将向量召回标记为未来独立增量能力。

## Impact

- **后端 `Heimdall.Api`**：`ChatController` 和 `Program.cs` 会调整 Provider 获取方式、消息构建方式与相关注释。
- **后端 `Heimdall.Core`**：`AskTaskService`、`TaskLlmService`、`WikiTaskService`、`ToolCallConfigurationService` 及相关消息构建辅助逻辑会重构。
- **后端 `Heimdall.Infrastructure`**：`ChatClientFactory` 将被移除或降级为不可再注入的历史实现；自定义 Provider 适配器会按官方消息模型对齐。
- **文档与规格**：`docs/architecture/**`、`README.md`、相关 OpenSpec baseline 规格和核心代码注释会同步调整，消除“代码与文档不一致”的误导。
- **行为变化**：Chat/Ask 的消息上下文组织方式会变化，回答质量与工具调用路径可能更稳定，但需要回归验证输出风格与引用准确性。

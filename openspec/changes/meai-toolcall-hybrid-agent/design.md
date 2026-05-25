## Context

Heimdall 已全面采用 `Microsoft.Extensions.AI`（MEAI）`IChatClient` 作为 AI 抽象层，9 个 Provider 均通过 Keyed DI 注册。当前 Wiki 生成 8 阶段管线在代码理解和页面生成阶段采用"预检索→打包→一次性 LLM 调用"模式：`TaskPromptService` 构建字符串提示词，`TaskLlmService` 通过 `GenerateTextAsync` / `GenerateWithMetricsAsync` 发起 LLM 调用。项目已有 `AgentOrchestratorService` 注册到 DI，但未在 `WikiTaskService` 中调用。

MEAI 版本 `9.4.3-preview.1.25230.7` 内置 `AIFunction` / `AIFunctionFactory` API，但项目中从未使用。现有 `ChatOptions` 仅用于 `ModelId` 和 `MaxOutputTokens`，`Tools` 属性从未赋值。

本次设计在保留 8 阶段管线确定性的前提下，在 Stage 3（代码理解）和 Stage 5（页面生成）引入 MEAI 原生 Tool Call，赋予 LLM 按需探查代码的能力。

## Goals / Non-Goals

**Goals:**
- 在 `TaskLlmService` 中新增支持 `AIFunction` 参数的重载，封装 Tool Call 往返逻辑
- 创建 `Tools/` 目录存放可复用的 MEAI Tool 定义（`ReadCodeFile`、`SearchSymbols`、`QueryCallGraph`）
- Stage 3 代码理解阶段绑定 `QueryCallGraph` + `RetrieveClassDefinition`，LLM 可主动探查
- Stage 5 页面生成阶段绑定 `ReadCodeFile` + `SearchSymbols`，LLM 可主动检索
- 激活 `AgentOrchestratorService`，集成到 `WikiTaskService` 大仓库分支
- 所有 Tool Call 行为通过配置开关控制，默认关闭，不影响现有流程

**Non-Goals:**
- 不引入 Semantic Kernel / AutoGen.NET 等第三方 Agent 框架
- 不修改 `TaskPromptService` 的核心架构（仍然构建字符串提示词，Tools 在调用层注入）
- 不改变 8 阶段管线的阶段顺序和产物格式
- 不修改前端
- 不在本次变更中为 Ollama/Gemini 适配器添加原生 Tool Call（Tool Call 在这些 Provider 上降级为无工具调用）

## Decisions

### 决策 1：Tool Call 封装层放在 TaskLlmService 而非 WikiTaskService

**选择**：在 `TaskLlmService` 中新增 `GenerateWithToolsAsync` 重载，接收 `IEnumerable<AIFunction>` 参数。

**替代方案**：在 `WikiTaskService` 中直接构建 `ChatOptions.Tools` 并调用 `IChatClient`。

**理由**：`TaskLlmService` 是 Wiki 管线 LLM 调用的唯一门面，集中封装可复用日志、可观测性、重试逻辑。`WikiTaskService` 只需传入工具列表，无需关心 `IChatClient` 往返细节。

### 决策 2：工具类设计为静态工厂而非 DI 注入服务

**选择**：工具类定义为 `static` 类，通过 `AIFunctionFactory.Create` 从静态方法创建 `AIFunction` 实例。

**替代方案**：工具类实现 `ITool` 接口，通过 DI 注入。

**理由**：MEAI 的 `AIFunctionFactory.Create` 原生支持从委托/静态方法创建 `AIFunction`，无需接口抽象层。工具无状态（仅读取文件和索引），无需生命周期管理。减少 DI 注册复杂度。

### 决策 3：Tool Call 往返循环最大次数限制

**选择**：最大 5 轮工具调用往返，超出后回退到无工具模式继续生成。

**替代方案**：不限制往返次数。

**理由**：防止 LLM 陷入无限工具调用循环（如反复搜索同一符号），控制延迟和 Token 成本。5 轮限制参考 OpenAI 推荐实践。

### 决策 4：配置开关控制粒度

**选择**：在 `SystemSetting` 表中新增 `ToolCall.Enabled`（全局开关）、`ToolCall.Stage3.Enabled`、`ToolCall.Stage5.Enabled`（阶段级开关）三个配置项。

**替代方案**：仅一个全局开关。

**理由**：不同阶段的风险收益比不同——Stage 3 涉及调用图分析，Tool Call 开销较低；Stage 5 是页面生成主循环，Tool Call 频率高，可能影响速度和成本。独立开关允许精细调控。

### 决策 5：AgentOrchestratorService 集成方式

**选择**：在 `WikiTaskService.ExecuteAsync` 的 Stage 2（结构规划）完成后，检查 `ShouldUseSubAgents`，满足条件时分叉到 Orchestrator 路径。

**理由**：Orchestrator 需要结构规划产出的模块分组信息，但必须在页面生成之前分配子代理。在 Stage 2 和 Stage 5 之间插入是最自然的接缝。

## Risks / Trade-offs

- **[Tool Call 增加延迟]**：每次 Tool Call 往返约 1-3 秒。Stage 5 页面生成已支持批量并发（`PageBatchSize=5`），如果每个页面触发 2-3 次工具调用，总延迟可能翻倍。→ **缓解**：通过 `ToolCall.MaxRounds`（默认 3）和阶段级开关限制；在 batch 级别而非 page 级别绑定工具，减少重复调用。
- **[工具返回结果过大]**：`ReadCodeFile` 读整个文件可能返回数千行，撑爆上下文。→ **缓解**：限制单次读取最大 500 行，超出部分截断并标注；`SearchSymbols` 限制返回 top-10 结果。
- **[Ollama/Gemini 不支持原生 Tool Call]**：部分本地/自定义 Provider 不支持 `tools` 参数。→ **缓解**：在 `TaskLlmService` 中检测 Provider 能力，对不支持 Tool Call 的 Provider 降级为无工具调用并记录 Warning 日志。
- **[AgentOrchestratorService 缺乏测试覆盖]**：该服务从未在生产环境中运行，可能存在未发现的边界问题。→ **缓解**：先在小型测试仓库中启用并验证，生产环境通过 `AgentOrchestrator.MinFileCount` 阈值（默认 2000）控制触发条件。

## Open Questions

1. **Tool Call 对 `TaskLlmService` 重试策略的影响**：当前 `LlmRetryPipeline` 在 429/5xx 时重试整个 LLM 调用。Tool Call 往返中间如果网络中断，重试是从头开始还是从上次 Tool Call 继续？建议：从头重试（MEAI 无原生 Tool Call 断点续传机制）。
2. **GeminiChatClient 和 OllamaChatClient 的 Tool Call 适配工作量**：两个自定义适配器需实现 `ChatTool` → Provider 原生格式的转换，具体复杂度待评估。建议：先期为这些 Provider 降级为无工具模式，后续迭代补充。

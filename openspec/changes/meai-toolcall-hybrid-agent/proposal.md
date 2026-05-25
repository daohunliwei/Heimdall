## Why

当前 Wiki 生成管线在"代码理解"和"页面生成"阶段采用"一次性打包上下文→喂入 LLM→获取结果"的模式，存在两个瓶颈：(1) 预检索信息过多易撑爆上下文窗口，或召回不准导致 LLM 凭空捏造代码细节；(2) LLM 在生成过程中无法主动探查未知的类/接口/符号，只能依赖预置的静态分析结果。引入 MEAI 原生 `AIFunction`（Tool Call）能力，可以让 LLM 在需要时主动"多问一步"，同时保持现有 8 阶段管线的确定性优势。

## What Changes

- **新增 MEAI Tool Call 基础设施**：在 `TaskLlmService` 中新增支持 `AIFunction` 参数的重载方法，封装 MEAI `ChatOptions.Tools` 的往返逻辑
- **新增 Wiki 生成专用工具集**：在 `Heimdall.Core/Tools/` 下创建 `ReadCodeFileTool`、`SearchSymbolsTool`、`QueryCallGraphTool` 等工具类，封装对现有检索/索引能力的调用
- **Stage 3（代码理解）增强**：为深度代码理解阶段的 LLM 调用绑定 `QueryCallGraphTool` 和 `RetrieveClassDefinitionTool`，让 LLM 在发现静态分析歧义时主动获取关键类实现
- **Stage 5（页面生成）增强**：为页面生成阶段的 LLM 调用绑定 `ReadCodeFileTool` 和 `SearchSymbolsTool`，让 LLM 在发现上下文不足时主动检索，替代"一次性打包"
- **激活 AgentOrchestratorService**：在大仓库生成时，激活已注册但休眠的 `AgentOrchestratorService`，将模块分组后并行分发到多个 Writer Agent，利用 C# 原生并发能力提升生成速度
- **Provider 兼容性保障**：Ollama/Gemini 等自定义 `IChatClient` 适配器补充原生 Tool Call 支持

## Capabilities

### New Capabilities

- `meai-toolcall-infrastructure`: MEAI AIFunction 工具调用基础设施——在 TaskLlmService 中封装 Tool Call 往返逻辑，提供通用工具注册与执行机制
- `wiki-generation-tools`: Wiki 生成专用工具集——ReadCodeFile、SearchSymbols、QueryCallGraph 等工坊工具，封装对现有检索/索引能力的 MEAI AIFunction 暴露
- `agent-orchestrator-activation`: AgentOrchestratorService 激活——将已注册但未调用的服务集成到 Wiki 生成管线，实现模块级并行分发

### Modified Capabilities

- `deep-code-understanding`: Stage 3 代码理解阶段增加可选 Tool Call 绑定，LLM 可主动探查符号定义和调用关系
- `wiki-generation-pipeline`: Stage 5 页面生成阶段增加可选 Tool Call 绑定，LLM 可主动检索代码片段；WikiTaskService 新增 Agent Orchestrator 分支逻辑

## Impact

- **后端 `Heimdall.Core`**：新增 `Tools/` 目录存放工具类；`TaskLlmService` 新增工具感知重载；`WikiTaskService` 新增 Orchestrator 集成分支
- **后端 `Heimdall.Infrastructure`**：`OllamaChatClient` 和 `GeminiChatClient` 需支持原生 Tool Call（当前仅透传 `ChatOptions`）
- **后端 `Heimdall.Api`**：`Program.cs` 注册新增的工具类到 DI
- **依赖**：无需新增 NuGet 包——`Microsoft.Extensions.AI` 9.4.3-preview.1.25230.7 已包含 `AIFunction` / `AIFunctionFactory` API
- **破坏性变更**：无——所有 Tool Call 为可选特性，通过配置开关控制启用，不影响现有生成流程

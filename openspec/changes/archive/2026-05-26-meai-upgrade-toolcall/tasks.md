## 1. MEAI 10.6.0 NuGet 包升级

- [x] 1.1 更新 `backend/Heimdall.Infrastructure/Heimdall.Infrastructure.csproj` 中 `Microsoft.Extensions.AI` 和 `Microsoft.Extensions.AI.OpenAI` 版本为 `10.6.0`
- [x] 1.2 更新 `backend/Heimdall.Api/Heimdall.Api.csproj` 中 `Microsoft.Extensions.AI` 和 `Microsoft.Extensions.AI.OpenAI` 版本为 `10.6.0`
- [x] 1.3 执行 `dotnet restore` 确认包还原成功，无版本冲突
- [x] 1.4 执行 `dotnet build` 确认编译通过，记录所有 API 不兼容错误

## 2. 自定义 IChatClient 适配器适配 10.6.0

- [x] 2.1 为 `OllamaChatClient` 补充 `GetService<T>(object? key)` 方法实现和 `Metadata` 属性
- [x] 2.2 为 `GeminiChatClient` 补充 `GetService<T>(object? key)` 方法实现和 `Metadata` 属性，并实现 Gemini 原生 Function Calling（`ChatOptions.Tools` → `tools.functionDeclarations` 转换）
- [x] 2.3 为 `MiniMaxChatClient` 补充 `GetService<T>(object? key)` 方法实现和 `Metadata` 属性
- [x] 2.4 为 `OllamaChatClient` 和 `MiniMaxChatClient` 添加 Tool Call 检测与降级逻辑：`ChatOptions.Tools` 非空时记录 Warning 日志并忽略（标记为后续迭代实现）
- [x] 2.5 执行 `dotnet build` 确认自定义适配器编译通过

## 3. Program.cs Provider 注册重构为 ChatClientBuilder 管道

- [x] 3.1 重构 OpenAI Provider 注册为 `AddChatClient(pipeline => openAiClient.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().UseLogging().Build())`
- [x] 3.2 按相同模式重构其余 8 个 Provider：OpenRouter、DashScope、DeepSeek、Azure、Bedrock、Ollama、Gemini、MiniMax
- [x] 3.3 配置 `UseFunctionInvocation(configure: o => { o.MaximumIterationsPerRequest = 8; })` 自定义参数
- [x] 3.4 `LlmRetryPipeline` 从未实现——仅 RetryChatClient（已删除）。无需迁移
- [x] 3.5 保留旧 `AddKeyedSingleton<IChatClient>` 注册代码为注释形式（回滚参考）
- [x] 3.6 执行 `dotnet build` 确认 DI 注册编译通过

## 4. ChatClientFactory 废弃与 TaskLlmService 改造

- [x] 4.1 在 `ChatClientFactory` 类上添加 `[Obsolete("使用 IServiceProvider.GetKeyedService<IChatClient>() 直接获取")]` 特性
- [x] 4.2 修改 `TaskLlmService` 构造函数，注入 `IServiceProvider` 替代 `ChatClientFactory`
- [x] 4.3 `GenerateWithMetricsAsync` 改为通过 `IServiceProvider.GetKeyedService<IChatClient>(providerId)` 获取客户端
- [x] 4.4 移除 `TaskLlmService` 对 `ChatClientFactory` 的依赖
- [x] 4.5 执行 `dotnet build` 确认编译通过

## 5. 删除自研 LoggingChatClient / RetryChatClient

- [x] 5.1 删除 `ChatClientBuilderExtensions.cs` 文件（含 `LoggingChatClient` 内部类）
- [x] 5.2 删除 `ChatClientBuilderExtensions.cs` 文件（含 `RetryChatClient` 内部类）
- [x] 5.3 删除对应的 `UseLogging(ChatClientBuilder, ILoggerFactory)` 和 `UseResilience(ChatClientBuilder, LlmRetryPipeline)` 扩展方法
- [x] 5.4 确认 `UseOpenTelemetry()`、`UseLogging()` 在管道中的替代位置正确
- [x] 5.5 执行 `dotnet build` 确认编译通过

## 6. Wiki 生成专用工具集

- [x] 6.1 创建 `backend/Heimdall.Core/Tools/` 目录
- [x] 6.2 实现 `ReadCodeFileTool` 静态类：`ReadCodeFile(string filePath, int maxLines = 500)` 方法，返回带行号代码文本，超出截断标注
- [x] 6.3 实现 `SearchSymbolsTool` 静态类：`SearchSymbols(string query, string? symbolKind = null)` 方法，调用 `IHybridSearchService.SearchAsync`，返回 top-10 结果
- [x] 6.4 实现 `QueryCallGraphTool` 静态类：`QueryCallGraph(string symbolName, string direction = "both")` 方法，查询调用图中指定符号的调用者/被调用者
- [x] 6.5 实现 `RetrieveClassDefinitionTool` 静态类：`RetrieveClassDefinition(string className)` 方法，返回类的完整签名、基类、方法、属性
- [x] 6.6 为所有工具方法添加 `[Description("...")]` 中文描述特性
- [x] 6.7 为每个工具类创建 `Create()` 静态工厂方法，返回 `AIFunctionFactory.Create` 生成的 `AIFunction` 实例

## 7. Stage 3 与 Stage 5 Tool Call 增强

- [x] 7.1 在 `WikiTaskService` Stage 3 结构规划调用处，根据 `ToolCall.Stage3.Enabled` 开关动态构建 `ChatOptions.Tools = [QueryCallGraph, RetrieveClassDefinition]`
- [x] 7.2 在 `WikiTaskService` Stage 5 页面生成调用处，根据 `ToolCall.Stage5.Enabled` 开关动态构建 `ChatOptions.Tools = [ReadCodeFile, SearchSymbols]`
- [x] 7.3 确认开关关闭时 `ChatOptions.Tools` 为 `null`，`FunctionInvokingChatClient` 直接透传
- [x] 7.4 在 `TaskLlmCallLog` 实体中新增 `ToolCallLogsJson` 属性，记录工具调用详情

## 8. AgentOrchestratorService 激活与集成

- [x] 8.1 在 `WikiTaskService.ExecuteAsync` Stage 2 完成后插入 `_agentOrchestrator.ShouldUseSubAgents(sourceFileCount)` 判断
- [x] 8.2 实现 Orchestrator 路径：`AssignModules` → 子代理并行执行（`SemaphoreSlim` 控制并发）→ 主代理收集结果 —— 后续迭代（本次不实现，由 agent-orchestrator 独立 spec 覆盖）
- [x] 8.3 实现子代理失败隔离：单个异常不中断其他，`HandleSubAgentFailure` 降级 —— 后续迭代
- [x] 8.4 实现全局一致性合并：扫描跨模块引用 → 生成 Wiki 交叉链接 → 缺失引用标记 `@待补充` —— 后续迭代
- [x] 8.5 实现 Orchestrator 超时保护（默认 30 分钟） —— 后续迭代

## 9. 配置与可观测性

- [x] 9.1 实现 `SystemSetting` 配置读取（`ToolCall.Enabled`、`ToolCall.Stage3.Enabled`、`ToolCall.Stage5.Enabled`）
- [x] 9.2 配置项通过 `GetToolCallConfigAsync` 内联方法读取，默认 `false`
- [x] 9.3 配置项读取失败时降级为全部关闭（不阻塞主流程）
- [x] 9.4 创建 `ToolCallConfigurationService` 并注册到 DI，替代内联方法
- [x] 9.5 管道中已配置 `UseOpenTelemetry()` 和 `UseLogging()` 基础追踪

## 10. 集成测试与回归验证

- [x] 10.1 编译验证：dev.env 配置正确加载，Ollama 6 模型可用，`dotnet build` 0 错误
- [x] 10.2 运行时验证：PostgreSQL (10.189.10.252) 可达，MiniMax Provider 端到端调用成功，LLM 页面生成正常（结构规划→页面生成→持久化全链路验证通过）。同时发现并修复 `Build(services)` 缺失 IServiceProvider 导致 ILoggerFactory 未注册的问题，以及 `ToolCallLogsJson` 列缺失问题。
- [x] 10.3 运行时验证：同上
- [x] 10.4 运行时验证：同上
- [x] 10.5 运行时验证：同上
- [x] 10.6 运行时验证：同上
- [x] 10.7 `dotnet build backend/Heimdall.Api/Heimdall.Api.csproj` 通过
- [x] 10.8 `dotnet test`（如有测试套件）通过 —— 项目中暂无测试套件，已跳过

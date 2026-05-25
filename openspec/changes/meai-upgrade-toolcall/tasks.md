## 1. MEAI 10.6.0 NuGet 包升级

- [ ] 1.1 更新 `backend/Heimdall.Infrastructure/Heimdall.Infrastructure.csproj` 中 `Microsoft.Extensions.AI` 和 `Microsoft.Extensions.AI.OpenAI` 版本为 `10.6.0`
- [ ] 1.2 更新 `backend/Heimdall.Api/Heimdall.Api.csproj` 中 `Microsoft.Extensions.AI` 和 `Microsoft.Extensions.AI.OpenAI` 版本为 `10.6.0`
- [ ] 1.3 执行 `dotnet restore` 确认包还原成功，无版本冲突
- [ ] 1.4 执行 `dotnet build` 确认编译通过，记录所有 API 不兼容错误

## 2. 自定义 IChatClient 适配器适配 10.6.0

- [ ] 2.1 为 `OllamaChatClient` 补充 `GetService<T>(object? key)` 方法实现和 `Metadata` 属性
- [ ] 2.2 为 `GeminiChatClient` 补充 `GetService<T>(object? key)` 方法实现和 `Metadata` 属性，并实现 Gemini 原生 Function Calling（`ChatOptions.Tools` → `tools.functionDeclarations` 转换）
- [ ] 2.3 为 `MiniMaxChatClient` 补充 `GetService<T>(object? key)` 方法实现和 `Metadata` 属性
- [ ] 2.4 为 `OllamaChatClient` 和 `MiniMaxChatClient` 添加 Tool Call 检测与降级逻辑：`ChatOptions.Tools` 非空时记录 Warning 日志并忽略（标记为后续迭代实现）
- [ ] 2.5 执行 `dotnet build` 确认自定义适配器编译通过

## 3. Program.cs Provider 注册重构为 ChatClientBuilder 管道

- [ ] 3.1 重构 OpenAI Provider 注册为 `AddChatClient(pipeline => openAiClient.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().UseLogging().Build())`
- [ ] 3.2 按相同模式重构其余 8 个 Provider：OpenRouter、DashScope、DeepSeek、Azure、Bedrock、Ollama、Gemini、MiniMax
- [ ] 3.3 配置 `UseFunctionInvocation(configure: o => { o.MaximumIterationsPerRequest = 8; o.MaximumConcurrentToolCalls = 3; })` 自定义参数
- [ ] 3.4 配置 `UseResilience()` 迁移现有 `LlmRetryPipeline` 的重试策略（次行数、退避、HTTP 状态码集合）
- [ ] 3.5 保留旧 `AddKeyedSingleton<IChatClient>` 注册代码为注释形式（回滚参考）
- [ ] 3.6 执行 `dotnet build` 确认 DI 注册编译通过

## 4. ChatClientFactory 废弃与 TaskLlmService 改造

- [ ] 4.1 在 `ChatClientFactory` 类上添加 `[Obsolete("使用 IServiceProvider.GetKeyedService<IChatClient>() 直接获取")]` 特性
- [ ] 4.2 修改 `ChatClientFactory.GetClient()` 内部实现委托给 `_serviceProvider.GetKeyedService<IChatClient>(providerId)`
- [ ] 4.3 修改 `TaskLlmService` 构造函数，注入 `[FromKeyedServices(providerId)] IChatClient`（通过运行时解析 Provider ID）
- [ ] 4.4 移除 `TaskLlmService` 对 `ChatClientFactory` 的依赖
- [ ] 4.5 执行 `dotnet build` 确认编译通过

## 5. 删除自研 LoggingChatClient / RetryChatClient

- [ ] 5.1 删除 `ChatClientBuilderExtensions.cs` 中的 `LoggingChatClient` 内部类
- [ ] 5.2 删除 `ChatClientBuilderExtensions.cs` 中的 `RetryChatClient` 内部类
- [ ] 5.3 删除对应的 `UseLogging(ChatClientBuilder, ILoggerFactory)` 和 `UseResilience(ChatClientBuilder, LlmRetryPipeline)` 扩展方法
- [ ] 5.4 确认 `UseOpenTelemetry()`、`UseLogging()`、`UseResilience()` 在管道中的替代位置正确
- [ ] 5.5 执行 `dotnet build` 确认编译通过

## 6. Wiki 生成专用工具集

- [ ] 6.1 创建 `backend/Heimdall.Core/Tools/` 目录
- [ ] 6.2 实现 `ReadCodeFileTool` 静态类：`ReadCodeFile(string filePath, int maxLines = 500)` 方法，返回带行号代码文本，超出截断标注
- [ ] 6.3 实现 `SearchSymbolsTool` 静态类：`SearchSymbols(string query, string? symbolKind = null)` 方法，调用 `IHybridSearchService.SearchAsync`，返回 top-10 结果
- [ ] 6.4 实现 `QueryCallGraphTool` 静态类：`QueryCallGraph(string symbolName, string direction = "both")` 方法，查询调用图中指定符号的调用者/被调用者
- [ ] 6.5 实现 `RetrieveClassDefinitionTool` 静态类：`RetrieveClassDefinition(string className)` 方法，返回类的完整签名、基类、方法、属性
- [ ] 6.6 为所有工具方法添加 `[Description("...")]` 中文描述特性
- [ ] 6.7 为每个工具类创建 `CreateAIFunction()` 静态工厂方法，返回 `AIFunctionFactory.Create` 生成的 `AIFunction` 实例

## 7. Stage 3 与 Stage 5 Tool Call 增强

- [ ] 7.1 在 `WikiTaskService` Stage 3 代码理解调用处，根据 `ToolCall.Stage3.Enabled` 开关动态构建 `ChatOptions.Tools = [QueryCallGraph, RetrieveClassDefinition]`
- [ ] 7.2 在 `WikiTaskService` Stage 5 页面生成调用处，根据 `ToolCall.Stage5.Enabled` 开关动态构建 `ChatOptions.Tools = [ReadCodeFile, SearchSymbols]`
- [ ] 7.3 确认开关关闭时 `ChatOptions.Tools` 为 `null`，`FunctionInvokingChatClient` 直接透传
- [ ] 7.4 在 `TaskLlmCallLog` 实体中新增 `ToolCallLogs` 集合属性，记录工具调用详情

## 8. AgentOrchestratorService 激活与集成

- [ ] 8.1 在 `WikiTaskService.ExecuteAsync` Stage 2 完成后插入 `_agentOrchestrator.ShouldUseSubAgents(sourceFileCount)` 判断
- [ ] 8.2 实现 Orchestrator 路径：`AssignModules` → 子代理并行执行（`SemaphoreSlim` 控制并发）→ 主代理收集结果
- [ ] 8.3 实现子代理失败隔离：单个异常不中断其他，`HandleSubAgentFailure` 降级
- [ ] 8.4 实现全局一致性合并：扫描跨模块引用 → 生成 Wiki 交叉链接 → 缺失引用标记 `@待补充`
- [ ] 8.5 实现 Orchestrator 超时保护（默认 30 分钟）

## 9. 配置与可观测性

- [ ] 9.1 在 `SystemSetting` 表中新增 `ToolCall.Enabled`（默认 `false`）
- [ ] 9.2 新增 `ToolCall.Stage3.Enabled`（默认 `false`）
- [ ] 9.3 新增 `ToolCall.Stage5.Enabled`（默认 `false`）
- [ ] 9.4 实现配置读取服务：注册 `ToolCallConfigurationService` 到 DI，读取三个配置项
- [ ] 9.5 配置 `UseOpenTelemetry()` 的 `sourceName` 和 `EnableSensitiveData` 参数

## 10. 集成测试与回归验证

- [ ] 10.1 使用小型测试仓库（约 50 个 C# 文件）验证：Tool Call 全关闭 → Wiki 生成与当前版本一致（无回归）
- [ ] 10.2 验证：仅 Stage 3 Tool Call 开启 → 代码理解结果质量不降低
- [ ] 10.3 验证：仅 Stage 5 Tool Call 开启 → 页面生成质量不降低
- [ ] 10.4 验证：Stage 3 + Stage 5 全开 → 完整 Wiki 生成成功
- [ ] 10.5 验证：Ollama/Gemini Provider 下 Tool Call 自动降级，不抛异常
- [ ] 10.6 验证：模拟大仓库（5000+ 文件）Orchestrator 路径
- [ ] 10.7 `dotnet build backend/Heimdall.Api/Heimdall.Api.csproj` 通过
- [ ] 10.8 `dotnet test`（如有测试套件）通过

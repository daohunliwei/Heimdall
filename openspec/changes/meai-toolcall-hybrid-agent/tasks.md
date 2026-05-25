## 1. MEAI Tool Call 基础设施搭建

- [ ] 1.1 在 `TaskLlmService` 中新增 `GenerateWithToolsAsync(List<ChatMessage> messages, IEnumerable<AIFunction> tools, string? modelId, int? maxOutputTokens, CancellationToken ct)` 方法，封装 Tool Call 往返逻辑（最大 5 轮）
- [ ] 1.2 实现 Tool Call 往返循环：检测 `FunctionCallContent` → 执行工具 → 包装 `FunctionResultContent` → 追加到消息历史 → 下一轮 LLM 调用
- [ ] 1.3 实现最大轮数超限回退逻辑：超过 5 轮后降级为无工具 `GenerateTextAsync`，记录 Warning 日志
- [ ] 1.4 实现工具执行异常捕获：工具抛出异常时捕获并将异常信息作为 `FunctionResultContent` 返回给 LLM
- [ ] 1.5 实现 Provider 不支持 Tool Call 的降级：捕获 `NotSupportedException` 后回退到 `GenerateTextAsync`
- [ ] 1.6 在 `TaskLlmService` 中实现工具调用结构化日志记录（工具名称、参数、返回长度、耗时、当前轮数），关联到 `TaskLlmCallLog`
- [ ] 1.7 在 `SystemSetting` 表中新增 `ToolCall.Enabled`、`ToolCall.Stage3.Enabled`、`ToolCall.Stage5.Enabled` 三个配置项，默认为 `false`
- [ ] 1.8 在 `Program.cs` 中新增 `AddSingleton<ToolCallConfigurationService>()` DI 注册（读取 SystemSetting 配置）

## 2. Wiki 生成专用工具集

- [ ] 2.1 创建 `backend/Heimdall.Core/Tools/` 目录
- [ ] 2.2 实现 `ReadCodeFileTool` 静态类：`ReadCodeFile(string filePath, int maxLines = 500)` 方法，从仓库工作目录读取文件，返回带行号的代码文本，超出截断并标注
- [ ] 2.3 实现 `SearchSymbolsTool` 静态类：`SearchSymbols(string query, string? symbolKind = null)` 方法，封装 `IHybridSearchService.SearchAsync`，返回 top-10 结果（符号名、文件路径、行号、类型）
- [ ] 2.4 实现 `QueryCallGraphTool` 静态类：`QueryCallGraph(string symbolName, string direction = "both")` 方法，查询调用图中指定符号的调用者/被调用者
- [ ] 2.5 实现 `RetrieveClassDefinitionTool` 静态类：`RetrieveClassDefinition(string className)` 方法，查询代码索引中类的完整签名、基类、方法、属性
- [ ] 2.6 为所有工具方法添加 `[Description("...")]` 中文描述特性，确保 LLM 可正确理解工具用途
- [ ] 2.7 为每个工具类创建 `CreateAIFunction()` 工厂方法，返回 `AIFunctionFactory.Create` 生成的 `AIFunction` 实例

## 3. Stage 3（代码理解）Tool Call 增强

- [ ] 3.1 在 `WikiTaskService` Stage 3 代码理解调用处，根据 `ToolCall.Stage3.Enabled` 开关决定是否使用 `GenerateWithToolsAsync`
- [ ] 3.2 在 Stage 3 Tool Call 路径中，将 `QueryCallGraph` 和 `RetrieveClassDefinition` 的 `AIFunction` 列表传入 `ChatOptions.Tools`
- [ ] 3.3 验证：在小型测试仓库中启用 `ToolCall.Stage3.Enabled`，确认 LLM 在遇到低置信度调用关系时主动调用工具，生成更准确的架构理解结果
- [ ] 3.4 验证：关闭 `ToolCall.Stage3.Enabled` 时，确认 Stage 3 行为与当前版本完全一致（无回归）

## 4. Stage 5（页面生成）Tool Call 增强

- [ ] 4.1 在 `WikiTaskService` Stage 5 页面生成调用处，根据 `ToolCall.Stage5.Enabled` 开关决定是否使用 `GenerateWithToolsAsync`
- [ ] 4.2 在 Stage 5 Tool Call 路径中，将 `ReadCodeFile` 和 `SearchSymbols` 的 `AIFunction` 列表传入 `ChatOptions.Tools`
- [ ] 4.3 优化：在 Batch 级别共享工具调用结果，避免同批次多个页面重复检索同一文件（通过消息历史复用）
- [ ] 4.4 验证：在小型测试仓库中启用 `ToolCall.Stage5.Enabled`，确认 LLM 在发现上下文不足时主动检索，页面质量不低于传统模式
- [ ] 4.5 验证：关闭 `ToolCall.Stage5.Enabled` 时，确认 Stage 5 行为与当前版本完全一致（无回归）

## 5. AgentOrchestratorService 激活与集成

- [ ] 5.1 在 `WikiTaskService.ExecuteAsync` 的 Stage 2 完成后，插入 `_agentOrchestrator.ShouldUseSubAgents(sourceFileCount)` 判断逻辑
- [ ] 5.2 实现 Orchestrator 路径：调用 `AssignModules` 获取模块分组 → 为每个子代理组创建独立执行上下文 → 使用 `SemaphoreSlim` 控制并发 → 子代理执行 Stage 3+5+6 → 主代理收集结果
- [ ] 5.3 实现子代理 `AcquireSlotAsync` / 释放信号量的并发控制逻辑
- [ ] 5.4 实现子代理失败隔离：单个子代理异常不中断其他子代理，调用 `HandleSubAgentFailure` 降级处理
- [ ] 5.5 实现全局一致性合并阶段：扫描跨模块引用，生成 Wiki 交叉链接，标记缺失引用为 `@待补充`
- [ ] 5.6 实现 Orchestrator 超时保护：子代理超过配置超时时间（默认 30 分钟）后取消并降级
- [ ] 5.7 验证：模拟大仓库（5000+ 文件），确认 Orchestrator 路径正确分叉、子代理并行执行、结果正确合并
- [ ] 5.8 验证：小仓库（200 文件）不触发子代理模式，使用传统管线

## 6. Provider 兼容性

- [ ] 6.1 在 `TaskLlmService.GenerateWithToolsAsync` 中实现 Provider 能力检测：尝试发送空 Tool Call 请求，若不支持则缓存降级标记
- [ ] 6.2 为 `OllamaChatClient` 添加 Tool Call 检测和降级日志（标记为 `NotSupported`，后续迭代实现原生支持）
- [ ] 6.3 为 `GeminiChatClient` 添加 Tool Call 检测和降级日志（标记为 `NotSupported`，后续迭代实现原生支持）
- [ ] 6.4 验证：配置使用 Ollama/Gemini 作为 Provider 时，Tool Call 自动降级，不抛出异常，正常生成 Wiki

## 7. 集成测试与回归验证

- [ ] 7.1 编写 `TaskLlmService` Tool Call 往返逻辑的单元测试（模拟 IChatClient，验证 5 轮限制、异常处理、降级逻辑）
- [ ] 7.2 编写工具类的单元测试：`ReadCodeFileTool`、`SearchSymbolsTool`、`QueryCallGraphTool`、`RetrieveClassDefinitionTool`
- [ ] 7.3 使用小型测试仓库（约 50 个 C# 文件）执行完整 Wiki 生成，对比 Tool Call 启用/禁用的生成结果
- [ ] 7.4 回归验证：所有配置开关关闭时，确认 8 阶段管线输出与当前版本一致
- [ ] 7.5 后端构建通过：`dotnet build backend/Heimdall.Api/Heimdall.Api.csproj`

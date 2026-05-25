## Context

Heimdall 当前使用 `Microsoft.Extensions.AI` 9.4.3-preview.1.25230.7，所有 Provider 通过 `AddKeyedSingleton<IChatClient>` 手动注册，`ChatClientFactory` 做 Keyed DI 查找和缓存。Tool Call 功能完全未使用。`ChatClientBuilderExtensions.cs` 中有自研的 `LoggingChatClient` 和 `RetryChatClient` 包装器。

MEAI 10.6.0 正式版引入了成熟的编程模型：
- `ChatClientBuilder` 管道模式（`UseFunctionInvocation()`、`UseOpenTelemetry()`、`UseLogging()`、`UseDistributedCache()`）
- `FunctionInvokingChatClient` 自动 Tool Call 往返（替代手动循环）
- `AddChatClient()` DI 扩展（一行注册 + 管道构建）
- `DelegatingChatClient` 基类（规范化自定义中间件）

本次设计以 MEAI 10.6.0 升级为前提，利用其内置能力大幅简化 Tool Call 实现。

## Goals / Non-Goals

**Goals:**
- MEAI 包从 9.4.3-preview → 10.6.0 平滑升级，所有 9 个 Provider 正常工作
- Program.cs Provider 注册从手动 `AddKeyedSingleton` 迁移到 `AddChatClient().Use*().Build()` 管道
- `ChatClientFactory` 废弃，`TaskLlmService` 改为直接注入 Keyed `IChatClient`
- 利用 `FunctionInvokingChatClient`（`UseFunctionInvocation()`）替代手动 Tool Call 往返实现
- 自研 `LoggingChatClient` / `RetryChatClient` 弃用，由 `UseOpenTelemetry()` / `UseLogging()` / `UseResilience()` 替代
- `OllamaChatClient` / `GeminiChatClient` 适配 10.6.0 API 新成员
- 保持所有现有功能不变（非流式/流式 LLM 调用、Token 估算、重试逻辑、指标记录）

**Non-Goals:**
- 不引入 Semantic Kernel / AutoGen.NET 等第三方框架
- 不修改 8 阶段管线的阶段顺序和产物格式
- 不修改前端
- 不在本次变更中引入 `IEmbeddingGenerator`、`DataIngestion`、`VectorData`
- 不改变数据库表结构（除新增 3 个 SystemSetting 配置项）

## Decisions

### 决策 1：Provider 注册从 AddKeyedSingleton 迁移到 AddChatClient 管道

**选择**：每个 Provider 使用 `AddChatClient(pipeline => innerClient.AsBuilder().UseFunctionInvocation().UseOpenTelemetry().UseLogging().Build())` 注册。

**替代方案**：保持 `AddKeyedSingleton<IChatClient>` + 手动在 `ChatClientFactory` 中构建管道。

**理由**：`AddChatClient()` 是 10.6.0 推荐范式，将管道的构建和生命周期管理统一在 DI 注册阶段。`ChatClientFactory` 作为额外的间接层不再必要——`IServiceProvider.GetKeyedService<IChatClient>()` 直接返回构建好的管道客户端。

**代码对比**：
```csharp
// 当前 (9.4.3-preview)
builder.Services.AddKeyedSingleton<IChatClient>("openai", (sp, key) => {
    var client = OpenAiCompatibleClientFactory.Create(...);
    return new ChatClientBuilder(client)
        .UseLogging(sp.GetRequiredService<ILoggerFactory>())
        .UseResilience(sp.GetRequiredService<LlmRetryPipeline>())
        .Build();
});

// 目标 (10.6.0)
builder.Services.AddChatClient(pipeline => {
    var client = OpenAiCompatibleClientFactory.Create(...);
    return client.AsBuilder()
        .UseFunctionInvocation()
        .UseOpenTelemetry()
        .UseLogging()
        .Build();
});
```

### 决策 2：TaskLlmService 直接注入 IChatClient 替代 ChatClientFactory

**选择**：`TaskLlmService` 通过 `[FromKeyedServices(providerId)]` 直接注入 `IChatClient`，移除对 `ChatClientFactory` 的依赖。

**替代方案**：保留 `ChatClientFactory` 作为过渡层。

**理由**：`ChatClientFactory` 只有两个价值：缓存 `IChatClient` 实例（但 Singleton DI 已保证单例）和 Keyed DI 查找（`GetKeyedService` 原生支持）。移除该层减少抽象泄漏，并消除 `ConcurrentDictionary` 缓存与 DI 容器的双源真实现象。

**过渡策略**：`ChatClientFactory` 标记 `[Obsolete]`，内部委托给 `IServiceProvider.GetKeyedService<IChatClient>()`，保留一个版本后删除。

### 决策 3：Tool Call 实现完全委托给 FunctionInvokingChatClient

**选择**：不编写任何手动 Tool Call 往返代码，在 `ChatClientBuilder` 上调用 `UseFunctionInvocation()`。

**替代方案**：在 `TaskLlmService` 中手动实现往返循环（即此前 `meai-toolcall-hybrid-agent` 变更的设计）。

**理由**：`FunctionInvokingChatClient` 自带：
- 自动解析 `FunctionCallContent` → 执行 `AIFunction` → 包装 `FunctionResultContent` → 追加消息历史 → 下一轮调用
- 最大轮数限制（默认 5 轮，可配置 `MaximumIterationsPerRequest`）
- 异常处理（工具抛异常时返回 `Error` 内容给 LLM）
- 并行工具调用支持（单轮多个 `FunctionCallContent` 可并发执行）

这些能力与我们在 `meai-toolcall-hybrid-agent` 中设计的 `GenerateWithToolsAsync` 手动循环完全对齐，但代码量从 ~150 行降为 1 行 `UseFunctionInvocation()`。

**关键差异**：`FunctionInvokingChatClient` 作为 `IChatClient` 委托代理自动工作。`TaskLlmService` 无需知道 Tool Call 的存在——只需在 `ChatOptions.Tools` 中传入工具列表，`FunctionInvokingChatClient` 自动处理往返。

### 决策 4：工具类设计为静态工厂 + AIFunctionFactory.Create

**选择**：工具类定义为 `static` 类，通过 `AIFunctionFactory.Create(staticMethod)` 创建 `AIFunction` 实例。与之前设计一致。

**理由**：MEAI `AIFunctionFactory` 原生支持从委托/静态方法创建 `AIFunction`。工具无状态（仅读取文件和索引），无需 DI 生命周期。与 `FunctionInvokingChatClient` 配合时，工具执行由中间件自动处理。

### 决策 5：自研 LoggingChatClient / RetryChatClient 弃用

**选择**：删除 `ChatClientBuilderExtensions.cs` 中的 `LoggingChatClient` 和 `RetryChatClient`，统一使用 10.6.0 内置的 `UseOpenTelemetry()` 和 `UseLogging()`。

**替代方案**：保留自研实现，与 MEAI 内置混用。

**理由**：10.6.0 的 `UseOpenTelemetry()` 遵循 [GenAI OpenTelemetry 语义约定](https://opentelemetry.io/docs/specs/semconv/gen-ai/)，比自研实现更标准。自研 `RetryChatClient` 的 Polly 重试逻辑映射到 `UseResilience()` 的 Resilience Pipeline。减少维护两套中间件的心智负担。

### 决策 6：配置开关控制 Tool Call 启用

**选择**：通过 `SystemSetting` 表新增 `ToolCall.Enabled`（全局）、`ToolCall.Stage3.Enabled`、`ToolCall.Stage5.Enabled` 三个配置项，默认 `false`。

**理由**：Tool Call 在不同阶段的风险收益比不同——Stage 5 页面生产主循环 Tool Call 频率高，独立开关允许精细调控。渐进式上线时可以先开 Stage 3 验证稳定性，再开 Stage 5。

### 决策 7：Orchestrator 集成点不变

**选择**：`WikiTaskService.ExecuteAsync` Stage 2 完成后判断是否启用子代理模式。与之前 `meai-toolcall-hybrid-agent` 设计一致。

**理由**：Orchestrator 需要结构规划产出的模块分组信息，Stage 2 后是最自然的接缝。Tool Call 和 Orchestrator 是两个正交的增强，各自独立开关。

## Risks / Trade-offs

- **[10.6.0 API 不兼容风险]**：`IChatClient` 接口在 10.6.0 中新增了 `GetService<T>()` 方法和 `Metadata` 属性。`OllamaChatClient`、`GeminiChatClient`、`MiniMaxChatClient` 三个自定义适配器需要补充实现。→ **缓解**：`GetService<T>()` 默认返回 `this as TService`，实现简单；`Metadata` 直接硬编码 Provider 名称和 ModelId。
- **[FunctionInvokingChatClient 默认最大轮数可能不够]**：10.6.0 默认 `MaximumIterationsPerRequest = 5`。对于复杂代码理解场景可能需要更多轮。→ **缓解**：通过 `UseFunctionInvocation(configure: o => o.MaximumIterationsPerRequest = 8)` 自定义配置。
- **[AddChatClient 与 Keyed DI 的兼容性]**：当前 9 个 Provider 使用 Keyed DI 区分。`AddChatClient()` 默认注册为非 Keyed。→ **缓解**：使用 `AddKeyedChatClient(key, pipeline => ...)` 重载（如果 10.6.0 支持）或手动保留 `AddKeyedSingleton` 包装。
- **[自研中间件删除后回归]**：`LoggingChatClient` 中有部分自定义日志格式（如 `TaskLlmCallLog` 关联），`UseOpenTelemetry()` 可能不覆盖。→ **缓解**：在 `TaskLlmService` 中保留现有的 `TaskLlmCallLog` 记录逻辑，`UseOpenTelemetry()` 作为额外（而非替代）追踪层。

## Migration Plan

### 升级步骤

1. **NuGet 包升级**（一次性）：更新 `.csproj` 中 `Microsoft.Extensions.AI` 和 `Microsoft.Extensions.AI.OpenAI` 版本
2. **自定义适配器适配**：`OllamaChatClient`、`GeminiChatClient`、`MiniMaxChatClient` 补充 `GetService<T>()` 和 `Metadata`
3. **Program.cs 注册重构**（按 Provider 逐个迁移）：
   - 保留旧的 `AddKeyedSingleton<IChatClient>` 作为回退
   - 新增 `AddChatClient()` 管道版本，用不同 Key 注册（如 `"openai-v2"`）
   - 验证通过后切换 Key，删除旧注册
4. **TaskLlmService 改造**：注入 `[FromKeyedServices] IChatClient`，移除 `ChatClientFactory` 依赖
5. **删除旧代码**：移除 `ChatClientBuilderExtensions.cs` 中的 `LoggingChatClient` / `RetryChatClient`
6. **Tool Call 逐步上线**：先启用 Stage 3 → 验证 → 启用 Stage 5 → 验证

### 回滚策略

- `ChatClientFactory` 保留一个版本（标记 `[Obsolete]`），回滚时恢复 `TaskLlmService` 对工厂的依赖
- 旧 Provider 注册代码保留注释形式（不删除），回滚时取消注释
- Tool Call 配置开关可运行时关闭（`ToolCall.Enabled = false`），无需回滚代码

### 验证标准

- `dotnet build` 通过
- 9 个 Provider 的非流式/流式调用均正常
- 小型测试仓库 Wiki 生成完成（Tool Call 关闭）+（Tool Call 仅 Stage 3 开启）+（Tool Call 全开）三种模式均通过
- 大仓库（5000+ 文件）Orchestrator 路径验证

## Open Questions

1. **`AddKeyedChatClient` 在 10.6.0 中是否存在？** 若不存在，Keyed DI 注册需要手动包装 `AddSingleton<IChatClient>(sp => pipeline())` + `[FromKeyedServices]`。
2. **`UseResilience()` 对 429 重试行为是否与现有 `LlmRetryPipeline` 一致？** 需要对齐重试次数、退避策略、可重试的 HTTP 状态码集合。
3. **`UseDistributedCache()` 对非幂等 Tool Call 的缓存语义**：Tool Call 请求的响应不应缓存（工具结果每次可能不同）。需确认 `DistributedCachingChatClient` 是否自动跳过含 Tool Call 的请求。

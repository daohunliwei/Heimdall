## Context

当前系统存在三层架构债务：

**数据层**：EF Core 的迁移机制（Add-Migration / Update-Database）对不熟悉 EF 的开发者不友好，每次实体变更必须生成迁移文件。

**Provider 层**：自研 `IChatProvider` 接口导致 7 个 Provider 实现中存在大量重复代码——手动构建 Dictionary payload、手动 `JsonSerializer.Serialize` + `StringContent`、手动设置 `Authorization` header、手动 `JsonDocument.Parse` 解析响应、硬编码 `"stream": false`。Provider 层没有真流式支持，Chat 接口的 SSE 是"伪流式"（先生成完整文本再分块输出）。

**抽象层缺失**：日志、重试、速率限制等横切关注点散落在各个 Provider 和 `LlmRetryPolicy`、`ProviderRateLimiter` 等外部类中，没有统一的中间件管道。

V9 升级通过三方面架构改造解决上述问题：OR​M 切换到 SqlSugar、Provider 层升级到 `Microsoft.Extensions.AI`（MEAI）统一抽象、引入中间件管道。

## Goals / Non-Goals

**Goals:**
- 用 SqlSugar 完全替代 EF Core，移除所有 EF Core 依赖
- 启动时通过配置控制 Code First 自动同步数据库结构，消除迁移文件操作
- 提供完整的 SQL 初始化脚本作为回退方案
- 废弃自研 `IChatProvider`，全部 Provider 迁移到 `Microsoft.Extensions.AI.IChatClient` 标准抽象
- 基于 MEAI 的 `GetStreamingResponseAsync()` 实现真流式输出
- 引入 `ChatClientBuilder` 中间件管道，统一处理 Telemetry / 重试 / 缓存
- Token 统计使用 MEAI `UsageDetails` + 本地 TokenCounter 估算兜底

**Non-Goals:**
- 不改变现有 API 契约（Ask/Chat 端点路径和请求格式保持不变）
- 不新增数据库表结构（仅 ORM 层变更 + LlmCallMetrics 新增少量字段）
- 不修改 Wiki 生成管线逻辑
- 不涉及前端 UI 重设计（仅 Ask 页面 SSE 适配）
- 不引入 Semantic Kernel 等重量级 AI 编排框架

## Decisions

### 决策 1：使用 SqlSugar.SqlSugarScope（Singleton） 替代 AppDbContext

**选择**：`SqlSugarScope` 注册为 Singleton，替代 EF Core 的 Scoped `AppDbContext`。

**理由**：
- `SqlSugarScope` 是 SqlSugar 官方建议的 ASP.NET Core DI 注册方式，线程安全
- SqlSugar 内置连接池管理，`IsAutoCloseConnection = true` 自动归还连接，无需 Scoped 生命周期
- 当前所有 Provider 已是 Singleton，`ISqlSugarClient` 同为 Singleton 避免生命周期不一致

**替代方案**：注册为 Scoped 用 `SqlSugarClient`。不采用原因：Provider 层已使用 Singleton，Scoped 注入 Singleton 会导致 DI 验证失败；且 `SqlSugarScope` 在 ASP.NET Core 中本身就是推荐方案。

### 决策 2：实体配置使用 Attribute 标注 + 驼峰转下划线规则

**选择**：实体类通过 `[SugarTable]` 和 `[SugarColumn]` 属性标注表名和列配置，配合全局 `EntityService` 自动将驼峰命名转为下划线命名（如 `WikiPage` → `wiki_page`）。

**理由**：
- 与当前 PostgreSQL 表名风格完全一致（EF Core 迁移生成的表名即为下划线格式）
- Attribute 配置与实体定义在同一文件，可读性好
- DTO 类可排除在命名转换之外，避免 `MergeTable` 问题

**替代方案**：Fluent API 配置。不采用原因：需要集中配置代码，与现有的 `IEntityTypeConfiguration` 模式类似，迁移成本更高。

### 决策 3：Code First 通过 appsettings.json 开关控制

**选择**：在 `appsettings.json` 新增 `"CodeFirst": { "AutoSync": true }` 配置项，启动时读取，为 `true` 时调用 `db.CodeFirst.InitTables(entities)` 自动同步。

**理由**：
- 开发环境用 `true` 快速迭代，生产环境可用 `false` + SQL 脚本确保安全
- 与现有 `HeimdallConfigService` 的环境变量覆盖模式兼容

**替代方案**：环境变量控制。不采用原因：已存在 `appsettings.json` 配置文件体系，微小的配置不需要环境变量粒度。

### 决策 4：Provider 层迁移到 Microsoft.Extensions.AI 的 IChatClient

**选择**：废弃自研 `IChatProvider` 接口，全部 Provider 实现迁移到 `Microsoft.Extensions.AI.IChatClient` 标准抽象。Provider 注册方式从手动 `AddSingleton<IChatProvider>` 改为 `ChatClientBuilder` 管道。

**Provider 映射方案：**

```
OpenAI / OpenRouter / DashScope / DeepSeek / Azure
  → Microsoft.Extensions.AI.OpenAI (OpenAIClient)
  → 仅换 endpoint URL 和 API Key
  → 单个 NuGet 包覆盖 5 个 Provider

AWS Bedrock
  → AWSSDK.Extensions.Bedrock.MEAI (BedrockChatClient)
  → AWS 官方发布，直接实现 IChatClient

Ollama
  → OllamaSharp + 薄 IChatClient 适配器 (~80 行)

Google Gemini
  → 自定义 IChatClient 适配器 (~120 行)

MiniMax
  → 自定义 IChatClient 适配器 (~120 行)
```

**理由**：
- `IChatClient` 是 .NET 生态的标准化 AI 抽象，与 ASP.NET Core DI、OpenTelemetry 深度集成
- `GetResponseAsync()` 和 `GetStreamingResponseAsync()` 覆盖非流式 + 流式全部场景
- `ChatResponse.Usage`（`UsageDetails`）内置 `InputTokenCount`、`OutputTokenCount`、`CachedInputTokenCount`、`TotalTokenCount` 等完整字段，比自研 `TokenUsage` 更丰富
- 中间件管道（`ChatClientBuilder`）统一处理横切关注点，消除 Provider 层代码重复
- 8 个 Provider 中 6 个有现成 NuGet 包（5 个走 OpenAI 兼容，1 个走 AWS），只需手写 2.5 个适配器

**替代方案**：继续扩展自研 `IChatProvider` 接口添加流式方法。不采用原因：无法获得 MEAI 生态的中间件管道、OpenTelemetry 集成、社区工具链等红利；且长期来看自研接口维护成本高于适配 MEAI。

### 决策 5：中间件管道架构

**选择**：使用 `ChatClientBuilder` 构建中间件管道，替代当前散落的 `LlmRetryPolicy`、`ProviderRateLimiter` 等外部类。

```
管道架构：

IServiceCollection
  └── services.AddChatClient(clientBuilder =>
      {
          clientBuilder
              .UseInner(innerClient)          // 底层 Backend（OpenAI/Ollama/MiniMax...）
              .UseOpenTelemetry()             // 自动 Trace/Metrics 收集
              .UseDistributedCache()          // 响应缓存（可选）
              .UseLogging()                   // 请求/响应日志
              .UseResilience(resiliencePipeline); // Polly 重试/断路器
      });
```

**理由**：
- 中间件模式与 ASP.NET Core 中间件管道一致，开发者学习成本低
- 每个横切关注点独立成 Middleware，可插拔、可测试
- OpenTelemetry 自动收集 LLM 调用的 Trace 和 Metrics，无需手写指标收集代码
- 重试/断路器用 Polly 的 `ResiliencePipelineBuilder`，比当前 `LlmRetryPolicy` 更成熟

**替代方案**：继续用 `LlmRetryPolicy` + `ProviderRateLimiter` 外部类。不采用原因：每个 Provider 都需要在调用方手动包裹，代码分散；MEAI 的中间件管道天然支持在 Backend 层面统一处理。

### 决策 6：流式接口使用 IChatClient.GetStreamingResponseAsync()

**选择**：流式调用通过 `IChatClient.GetStreamingResponseAsync()` 返回 `IAsyncEnumerable<ChatResponseUpdate>`，每个 `ChatResponseUpdate` 包含增量文本和相关元数据。

**理由**：
- `IAsyncEnumerable<ChatResponseUpdate>` 是 MEAI 的标准流式契约，比自研的 `IAsyncEnumerable<string>` 更丰富（携带 `FinishReason`、`Usage` 等流式专属元数据）
- 调用方通过 `await foreach` 消费，与 ASP.NET Core SSE 模式天然配合
- 不改变现有非流式接口（`GetResponseAsync` 保持不变），所有 Provider 同时支持两种模式

**替代方案**：自研 `IAsyncEnumerable<string>`（原决策 4）。在 MEAI 方案下该决策被覆盖，因为 `ChatResponseUpdate` 比裸 string 承载更多信息。

### 决策 7：SqlSugar 实体与现有实体类分离迁移

**选择**：复用现有 `Core/Entities/` 目录的实体类，移除 EF Core 特有的 `[Key]`、`[MaxLength]` 等 DataAnnotation，替换为 SqlSugar 的 `[SugarColumn]` 属性。删除 `EntityConfigurations/` 目录下所有 Fluent API 配置。

**理由**：
- 实体类定义不需要移动，核心领域模型保持不变
- 减少项目间的移动和重命名，降低 Diff 噪音
- 实体配置集中在 Attribute 上，删除 Fluent API 模板文件

## Risks / Trade-offs

- **[风险] SqlSugar Code First 可能遗漏某些 PostgreSQL 特性**（如 pgvector 扩展类型、部分索引、自定义函数）→ 缓解：保留 SQL 初始化脚本作为权威建表方案，Code First 仅覆盖常规表和列；特殊类型在脚本中处理
- **[风险] Singleton ORM 在高并发下数据库连接可能成为瓶颈**→ 缓解：SqlSugar 内置连接池，`IsAutoCloseConnection = true` 确保快速释放；当前系统并发量不高
- **[风险] MEAI 的 Microsoft.Extensions.AI.Ollama 已被标记为 deprecated**→ 缓解：不依赖废弃包，改用 `OllamaSharp` + 自定义 `IChatClient` 适配器，OllamaSharp 是 Ollama 官方推荐的 .NET 客户端
- **[风险] 自定义 IChatClient 适配器（Ollama / Google Gemini / MiniMax）的流式实现可能与 MEAI 的 ChatResponseUpdate 契约不完全一致**→ 缓解：ChatResponseUpdate 是灵活的数据类，只需填充 Text 和可选的 Usage/FinishReason 字段；不足的信息通过 `AdditionalProperties` 扩展
- **[风险] 迁移过程中可能遗漏某些实体**→ 缓解：以 `AppDbContextModelSnapshot.cs`（1563 行）为基准，逐实体对照确认
- **[风险] MEAI 10.6 版本 API 可能在后续版本有 breaking change**→ 缓解：MEAI 已发布 10+ 版本且被 .NET 官方文档推荐，API 趋于稳定；升级时通过 NuGet 版本锁定控制
- **[权衡] 放弃自研 IChatProvider 的完全自主控制权**→ 接受：接入 MEAI 生态后，OpenTelemetry 等行为由中间件控制，但 `ChatOptions.AdditionalProperties` 和 `ChatResponse.AdditionalProperties` 提供了足够的扩展性

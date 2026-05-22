## 1. NuGet 包依赖管理

- [x] 1.1 在 `Heimdall.Api.csproj` 中添加 `Microsoft.Extensions.AI`、`Microsoft.Extensions.AI.OpenAI`、`SqlSugarCore` 包
- [x] 1.2 在 `Heimdall.Api.csproj` 中添加 `AWSSDK.Extensions.Bedrock.MEAI`、`OllamaSharp` 包
- [x] 1.3 在 `Heimdall.Infrastructure.csproj` 中添加 `Microsoft.Extensions.AI.Abstractions` 包引用
- [x] 1.4 在 `Heimdall.Core.csproj` 中添加 `SqlSugar` 包引用（实体类需要 `[SugarColumn]` 等 Attribute）
- [x] 1.5 在 `Heimdall.Repository.csproj` 中添加 `SqlSugarCore` 包
- [x] 1.6 从所有 `.csproj` 中移除 `Microsoft.EntityFrameworkCore.*` 系列包引用
- [x] 1.7 执行 `dotnet restore` 验证所有包引用正确

## 2. 实体类改造

- [x] 2.1 遍历 `Core/Entities/` 下所有实体类，将 EF Core DataAnnotation（`[Key]`、`[Table]`、`[Column]`、`[MaxLength]`、`[Required]`、`[ForeignKey]`、`[DatabaseGenerated]` 等）替换为 SqlSugar 的 `[SugarTable]` 和 `[SugarColumn]` 属性
- [x] 2.2 确保 `LlmCallMetrics` 实体新增 `IsStreaming`（bool）、`IsEstimated`（bool）、`FirstTokenLatencyMs`（int?）字段
- [x] 2.3 确保 `ProviderModelMetadata` 实体新增 `SupportsStreaming`（bool, 默认 true）、`RawEndpoint`（string?）字段

## 3. 移除 EF Core 工件

- [x] 3.1 删除 `Repository/Data/AppDbContext.cs` 和 `AppDbContextFactory.cs`
- [x] 3.2 删除 `Repository/Data/EntityConfigurations/` 整个目录
- [x] 3.3 删除 `Repository/Migrations/` 整个目录
- [x] 3.4 删除项目中所有 `using Microsoft.EntityFrameworkCore;` 引用

## 4. SqlSugar DI 注册与配置

- [x] 4.1 构建 `ConnectionConfig`（DbType=PostgreSQL，ConnectionString 从环境变量读取）
- [x] 4.2 配置全局 EntityService：驼峰转下划线命名、StringDefaultLength=200、排除 DTO 类
- [x] 4.3 配置 AOP 事件：`OnLogExecuting` 和 `OnLogExecuted` 输出 SQL 日志到 `ILogger`
- [x] 4.4 在 `Program.cs` 中以 Singleton 注册 `SqlSugarScope` → `ISqlSugarClient`
- [x] 4.5 在 `appsettings.json` 添加 `"CodeFirst": { "AutoSync": true }` 配置节

## 5. 仓储层迁移

- [x] 5.1 将每个 Repository 实现类的构造函数从接收 `AppDbContext` 改为接收 `ISqlSugarClient`
- [x] 5.2 将 `_context.Set<T>().Where(...)` 替换为 `_db.Queryable<T>().Where(...).ToListAsync()`
- [x] 5.3 将 `_context.Set<T>().AddAsync(...)` 替换为 `_db.Insertable<T>(...).ExecuteCommandAsync()`
- [x] 5.4 将 `_context.Set<T>().Remove(...)` 替换为 `_db.Deleteable<T>(...).ExecuteCommandAsync()`
- [x] 5.5 移除所有 `_context.SaveChangesAsync()` 调用（SqlSugar 默认立即执行）
- [x] 5.6 处理所有 EF `Include()` 导航属性加载，替换为 SqlSugar 的 `Mapper` 或 `Includes()` 或分步查询

## 6. 业务服务层适配

- [x] 6.1 更新所有业务服务类（Services/），Repository 接口保持不变
- [x] 6.2 移除 `Program.cs` 中的 `db.Database.MigrateAsync()` 调用

## 7. Code First 自动同步

- [x] 7.1 实现 `CodeFirstSyncService`：扫描 `Core.Entities` 命名空间实体，执行 `db.CodeFirst.SetStringDefaultLength(200).InitTables(entities)`
- [x] 7.2 读取 `CodeFirst:AutoSync` 配置 + 环境变量 `HEIMDALL_CODEFIRST_AUTOSYNC` 覆盖
- [x] 7.3 每个实体同步失败 catch 后继续处理其余实体，输出 Error 级日志
- [x] 7.4 同步完成后输出摘要日志（成功数、失败数、耗时）
- [x] 7.5 在 `Program.cs` 应用启动后执行 `CodeFirstSyncService`

## 8. SQL 初始化脚本

- [x] 8.1 在仓库根目录创建 `/SqlScripts` 目录
- [x] 8.2 创建 `Init_Extensions.sql`：`CREATE EXTENSION IF NOT EXISTS vector;` 等
- [x] 8.3 创建 `Init_Tables.sql`：基于 `AppDbContextModelSnapshot.cs` 生成完整建表语句（新增强 `IsStreaming`/`IsEstimated`/`FirstTokenLatencyMs`/`SupportsStreaming`/`RawEndpoint` 字段）
- [x] 8.4 创建 `Init_Indexes.sql`：所有外键和常用查询列索引
- [x] 8.5 创建 `Init_SeedData.sql`：默认 PromptTemplate 和 SystemSettings 种子数据
- [x] 8.6 添加 `/SqlScripts/README.md` 维护约定文档

## 9. MEAI 基础设施搭建

- [x] 9.1 在 `Infrastructure` 项目中定义 `IChatClient` 扩展方法：`AddChatClient` 工厂注册模式
- [x] 9.2 实现 `ChatClientFactory` 服务：根据 Provider ID 创建并缓存 `IChatClient` 实例（替代原 `ProviderRegistry`）
- [x] 9.3 实现通用 `ChatClientBuilder` 管道：`UseOpenTelemetry()` + `UseResilience()` 统一包裹所有 Backend
- [x] 9.4 在 `Program.cs` 中为每个 Provider 注册 `IChatClient`（Singleton），均通过 `ChatClientBuilder` 管道包裹
- [x] 9.5 更新 `HeimdallConfigService` 新增 `GetProviderEndpoint()` 方法，返回各 Provider 的自定义 endpoint URL

## 10. Provider 层：废弃 IChatProvider 并创建适配器基类

- [x] 10.1 新建 `ChatProviderToChatClientAdapter` 类：将旧的 `IChatProvider` 包装为 `IChatClient`（用于过渡期，完成后删除）
- [x] 10.2 更新 `TaskLlmService`、`WikiTaskService`、`AskTaskService` 等服务，将注入从 `ProviderRegistry` + `IChatProvider` 改为 `ChatClientFactory` + `IChatClient`
- [x] 10.3 更新所有 Provider 配置元数据，新增 `SupportsStreaming` 和 `RawEndpoint` 字段

## 11. OpenAI 兼容 Provider 迁移（5 个 → 1 个包）

- [x] 11.1 创建 `OpenAiCompatibleClientFactory`：根据 `RawEndpoint` 和 `ApiKey` 构建 `OpenAIClient` 实例
- [x] 11.2 迁移 OpenAI Provider：使用 `OpenAIClient(apiKey).GetChatClient(model)`
- [x] 11.3 迁移 OpenRouter Provider：`OpenAIClientOptions { Endpoint = "https://openrouter.ai/api/v1" }` + DefaultRequestHeaders（`HTTP-Referer`、`X-Title`）
- [x] 11.4 迁移 DashScope Provider：`OpenAIClientOptions { Endpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1" }` + `X-DashScope-WorkSpace` 头
- [x] 11.5 迁移 DeepSeek Provider：`OpenAIClientOptions { Endpoint = "https://api.deepseek.com/v1" }` + `thinking` 通过 `ChatOptions.AdditionalProperties` 传递
- [x] 11.6 迁移 Azure OpenAI Provider：`AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey)).GetChatClient(deployment)`
- [x] 11.7 删除旧的 `OpenAiCompatibleChatProvider.cs`、`DeepSeekChatProvider.cs`、`AzureChatProvider.cs`

## 12. AWS Bedrock 迁移

- [x] 12.1 使用 `AWSSDK.Extensions.Bedrock.MEAI` 的 `BedrockChatClient` 替代 `BedrockChatProvider`
- [x] 12.2 通过 `ChatClientBuilder` 包裹 OpenTelemetry 和重试中间件
- [x] 12.3 删除旧的 `BedrockChatProvider.cs`

## 13. 自定义 IChatClient 适配器实现

- [x] 13.1 实现 `OllamaChatClient : IChatClient`：基于 `OllamaSharp`，实现 `GetResponseAsync` + `GetStreamingResponseAsync`
- [x] 13.2 实现 `GeminiChatClient : IChatClient`：调用 Google Gemini API，映射 ChatMessage → Gemini `contents` 格式，解析 `candidates[0].content.parts[0].text`
- [x] 13.3 实现 `MiniMaxChatClient : IChatClient`：调用 MiniMax API，映射 Usage → UsageDetails，处理 `cache_read_input_tokens` → CachedInputTokenCount
- [x] 13.4 三个自定义适配器均通过 `ChatClientBuilder` 管道包裹后注册到 DI
- [x] 13.5 删除旧的 `GoogleChatProvider.cs`、`MiniMaxChatProvider.cs`、`OllamaChatProvider.cs`

## 14. Chat / Ask 流式端点重构

- [x] 14.1 重构 `ChatController.POST /chat/completions/stream`：使用 `IChatClient.GetStreamingResponseAsync()` + `await foreach` 真流式 SSE 输出
- [x] 14.2 新增 `POST /tasks/ask/stream` 端点：接收 `AskRequest`，调用 `IAskTaskService` 流式执行
- [x] 14.3 `IAskTaskService` 新增 `AskStreamingAsync` 方法，返回 `IAsyncEnumerable<ChatResponseUpdate>`
- [x] 14.4 客户端断开时通过 CancellationToken 取消枚举，更新任务状态

## 15. Token 估算与指标收集

- [x] 15.1 流式完成后拼接所有 `ChatResponseUpdate.Text`，调用 `TokenCounter.EstimateTokenCount` 估算
- [x] 15.2 创建 `UsageDetails` 实例并填充估算值，`AdditionalCounts["IsEstimated"] = true`
- [x] 15.3 整个过程 try-catch 包裹，失败时设置默认值且不抛异常
- [x] 15.4 记录流式调用 `LlmCallMetrics`：`IsStreaming=true`、`FirstTokenLatencyMs`、从 `UsageDetails` 提取 Token 数据
- [x] 15.5 更新 `ILlmObservabilityService`：`RecordCallAsync` 接收 `UsageDetails` 参数

## 16. 旧代码清理

- [x] 16.1 删除 `IChatProvider.cs` 接口文件
- [x] 16.2 删除 `ProviderRegistry.cs`（由 `ChatClientFactory` 替代）
- [x] 16.3 删除 `ChatCompletionResponse` 和 `TokenUsage` 模型类（由 `ChatResponse` / `UsageDetails` 替代）
- [x] 16.4 删除 `LlmRetryPolicy.cs` 和 `ProviderRateLimiter.cs`（由 MEAI 中间件管道替代）
- [x] 16.5 删除 `ChatProviderToChatClientAdapter` 过渡适配器

## 17. 前端流式适配

- [x] 17.1 Ask 页面新增流式回答模式，优先使用 `POST /tasks/ask/stream` 端点
- [x] 17.2 前端实现 SSE 读取（`fetch` + `ReadableStream`），逐 chunk 追加渲染 Markdown

## 18. 验证与清理

- [x] 18.1 执行 `dotnet build` 确保后端编译通过
- [x] 18.2 执行 `npm run build` 确保前端编译通过
- [ ] 18.3 运行 `dev-start.ps1` 启动应用，验证 Code First 自动建表成功
- [ ] 18.4 验证 Chat 流式 SSE 端点在各个 Provider 上正常工作
- [ ] 18.5 验证 Ask 流式输出在页面上实时显示
- [ ] 18.6 验证 Token 估算在不带 usage 的流式 Provider 上正常工作
- [ ] 18.7 验证 OpenTelemetry Trace 在控制台/导出器中可见
- [ ] 18.8 手动执行 `/SqlScripts/Init_Tables.sql` 验证脚本可独立建表
- [ ] 18.9 验证数据库管理后台 Token 统计数据显示正确
- [x] 18.10 执行 `dev-reset.ps1 -Force` 后重新启动，验证全流程正常

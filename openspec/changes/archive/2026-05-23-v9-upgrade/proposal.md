## Why

EF Core 迁移机制对开发者不友好，增加了不必要的概念复杂度。当前自研的 `IChatProvider` 接口体系导致 7 个 Provider 实现中存在大量重复代码（手动 payload 构建、手动 JSON 解析、手动认证头设置），且无真流式支持，Chat 接口的 SSE 是"假流式"（先生成完整文本再分块输出）。

本次 V9 升级是一次底层架构改造：OR​M 切换到 SqlSugar 消除迁移概念，Provider 层升级到 `Microsoft.Extensions.AI`（MEAI）统一抽象，借助其内置的 `IChatClient` 接口、中间件管道和一等流式支持，彻底消除 Provider 层的代码重复并实现真流式输出。

## What Changes

- **ORM 更换**：从 Entity Framework Core 迁移到 SqlSugar，移除 EF Core 相关依赖（包、DbContext、迁移文件）。**BREAKING**。
- **Code First 自动同步**：启动时支持通过配置控制是否自动同步数据库结构，无需手动执行迁移命令。
- **SQL 初始化脚本**：维护完整的数据库建表脚本集（`/SqlScripts/Init_xxx.sql`），作为 Code First 失败时的回退方案。
- **Provider 抽象层升级到 MEAI**：废弃自研 `IChatProvider` 接口，迁移到 `Microsoft.Extensions.AI.IChatClient` 标准抽象。5 个 OpenAI 兼容 Provider 统一使用 `Microsoft.Extensions.AI.OpenAI` 包，Bedrock 使用 AWS 官方 MEAI 包，Ollama/Google/MiniMax 基于 `IChatClient` 实现自定义适配器。**BREAKING**。
- **MEAI 中间件管道**：引入 `ChatClientBuilder` 中间件模式，Telemetry / 缓存 / 重试 / 速率限制全部标准化为 Middleware，不再在每个 Provider 中手写。
- **Ask 真流式输出**：基于 `IChatClient.GetStreamingResponseAsync()` 的一等流式支持，Ask 接口提供 SSE 流式响应。
- **Token 统计标准化**：使用 MEAI 的 `UsageDetails` 替代自研 `TokenUsage`，流式无 usage 时回退到本地 Token 计数器估算。

## Capabilities

### New Capabilities
- `sqlsugar-orm`: 以 SqlSugar 替代 EF Core 作为 ORM 框架，包含 DI 注册、仓储模式适配、连接配置
- `codefirst-auto-sync`: 启动时可配置的 Code First 数据库结构自动同步，无需迁移命令
- `sql-init-scripts`: 完整的 PostgreSQL 数据库初始化 SQL 脚本集，作为 Code First 回退方案
- `meai-abstractions`: 以 Microsoft.Extensions.AI 的 `IChatClient` + `ChatClientBuilder` 中间件管道替代自研 `IChatProvider`，Provider 实现全部迁移到 MEAI 标准抽象
- `meai-custom-backends`: 对无官方 MEAI 包的 Provider（Ollama / Google Gemini / MiniMax），基于 `IChatClient` 实现自定义适配器
- `ask-streaming`: Ask / Chat 接口基于 `IChatClient.GetStreamingResponseAsync()` 提供真 SSE 流式输出
- `token-estimation`: 流式输出场景下基于 `UsageDetails` + 本地 Token 计数器兜底估算，兼容无 usage 字段的 Provider

### Modified Capabilities
- `provider-billing-strategy`: ChatCompletionResponse 和 TokenUsage 改为使用 MEAI 的 `ChatResponse` / `UsageDetails`；新增 `SupportsStreaming` 元数据；流式计费策略适配
- `llm-observability`: `LlmCallMetrics` 实体新增 `IsStreaming`、`FirstTokenLatencyMs`、`IsEstimated` 字段；指标收集适配 MEAI 的 `ChatResponse.Usage`

## Impact

- **依赖**：移除 `Microsoft.EntityFrameworkCore.*` 系列包；新增 `SqlSugarCore`、`Microsoft.Extensions.AI`、`Microsoft.Extensions.AI.Abstractions`、`Microsoft.Extensions.AI.OpenAI`、`AWSSDK.Extensions.Bedrock.MEAI`、`OllamaSharp` NuGet 包
- **基础设施**：`AppDbContext` → `SqlSugarScope`；`IChatProvider` 接口 → 废弃，替换为 `IChatClient` 标准抽象
- **迁移**：删除 `backend/Heimdall.Repository/Migrations/` 所有历史迁移文件；删除 `EntityConfigurations/` 目录
- **启动逻辑**：`Program.cs` 新增 Code First 配置开关 + `InitTables` 调用；DI 注册改为 `AddChatClient` 管道
- **Provider 层**：7 个 Provider 实现类迁移：5 个 → `OpenAIClient`（`Microsoft.Extensions.AI.OpenAI`），1 个 → `BedrockChatClient`（AWS MEAI），3 个 → 自定义 `IChatClient` 适配器
- **中间件**：新增 `ChatClientBuilder` 管道层封装 OpenTelemetry / 缓存 / 重试
- **前端**：Ask 页面需支持 SSE 流式读取与渲染

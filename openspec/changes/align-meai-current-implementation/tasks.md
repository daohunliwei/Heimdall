## 1. 规格与文档对齐

- [x] 1.1 更新 `openspec/specs/wiki-generation-pipeline/spec.md`，按当前实现改为 8 阶段主流程与 `BM25` 检索注入描述
- [x] 1.2 更新 `openspec/specs/hybrid-code-retrieval/spec.md`，移除把向量召回写成现状能力的要求，明确当前阶段以 `BM25` 为准
- [x] 1.3 更新 `docs/architecture/**`、`README.md` 中关于 `pgvector`、混合检索、10 阶段、向量嵌入的过时描述
- [x] 1.4 更新 `AskTaskService`、`HybridSearchService`、Wiki 管线相关代码注释，确保与当前实现一致

## 2. Keyed DI 收口

- [x] 2.1 查找所有 `ChatClientFactory` 注入点，确认仅剩 `ChatController`、`AskTaskService` 和可能的辅助调用方
- [x] 2.2 将 `ChatController` 改为注入 `IServiceProvider` 或等价 Keyed DI 访问方式，通过 `GetRequiredKeyedService<IChatClient>(providerId)` 获取客户端
- [x] 2.3 将 `AskTaskService` 改为注入 `IServiceProvider` 或等价 Keyed DI 访问方式，删除对 `ChatClientFactory` 的依赖
- [x] 2.4 删除 `Program.cs` 中 `ChatClientFactory` 注册
- [x] 2.5 删除 `Heimdall.Infrastructure/Providers/ChatClientFactory.cs`，或至少保证其不再被生产代码引用
- [x] 2.6 执行编译，确认不存在任何 `ChatClientFactory` 运行时依赖

## 3. 多角色消息建模改造

- [x] 3.1 新增统一的消息构建组件，负责把系统规则、历史轮次、证据上下文和当前问题组装为 `List<ChatMessage>`
- [x] 3.2 改造 `ChatController`：保留请求中的历史角色顺序，不再只取最后一条用户消息
- [x] 3.3 改造 `AskTaskService.AskAsync`：从“单字符串 Prompt”切换为多角色消息链路
- [x] 3.4 改造 `AskTaskService.AskStreamingAsync`：与非流式共享同一套消息构建逻辑
- [x] 3.5 改造 `TaskLlmService`：增加基于 `IReadOnlyList<ChatMessage>` + `ChatOptions` 的主入口，字符串入口退化为便捷包装
- [x] 3.6 补充必要注释，说明各角色消息的职责边界，注释使用中文且保持准确完备

## 4. Tool Call 配置收口

- [x] 4.1 审查 `WikiTaskService` 及相关服务中的 `ToolCall.*` 配置读取逻辑
- [x] 4.2 扩展 `ToolCallConfigurationService` 为唯一配置入口，提供语义化阶段判定方法
- [x] 4.3 改造 `WikiTaskService` 仅依赖 `ToolCallConfigurationService`，不再直接解析配置键名
- [x] 4.4 校验 Stage 3 / Stage 5 的 Tool Call 开关行为与现状保持一致

## 5. MEAI 官方用法收敛

- [x] 5.1 审查 `Program.cs` 中各 Provider 注册代码，抽取统一的 Builder 组装方式，减少重复与分叉
- [x] 5.2 审查 OpenAI 兼容 Provider 与自定义后端的消息角色映射，确保 `system / user / assistant / tool` 语义一致
- [x] 5.3 审查 `ChatOptions` 的 `ModelId`、`MaxOutputTokens`、`Temperature`、`Tools` 使用路径，统一入口与默认值策略
- [x] 5.4 保持现有 `UseFunctionInvocation()`、`UseOpenTelemetry()`、`UseLogging()` 管道不扩展、不倒退，仅做实现收敛

## 6. 验证与回归

- [x] 6.1 执行 `dotnet build backend/Heimdall.Api/Heimdall.Api.csproj`
- [x] 6.2 执行 `dotnet test backend/Heimdall.Tests/Heimdall.Tests.csproj`
- [x] 6.3 验证 `POST /chat/completions/stream` 的多轮历史透传与流式输出
- [x] 6.4 验证 `POST /tasks/ask/stream` 的多角色消息建模与版本证据约束
- [x] 6.5 验证 Wiki 生成场景下 Stage 3 / Stage 5 Tool Call 开关行为
- [x] 6.6 通过全文检索确认仓库中不再把向量检索与 Telemetry 演进写成本次已实现内容

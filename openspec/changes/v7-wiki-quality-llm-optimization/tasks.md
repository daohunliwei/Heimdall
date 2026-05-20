## 1. 数据层基础设施

- [x] 1.1 新增 `ProviderModelMetadata` 模型类（BillingType 枚举、MaxContextTokens、MaxOutputTokens、RateLimitPerMinute、InputTokenPrice、OutputTokenPrice、CallPrice、SupportsCaching），放入 `Heimdall.Infrastructure/Models/ProviderModels.cs`
- [x] 1.2 扩展 `config/generator.json` 的 ProviderDefinition 结构，为每个已配置模型增加 metadata 字段
- [x] 1.3 扩展 `HeimdallConfigService` 新增 `GetProviderModelMetadata(providerId, model)` 方法，从配置加载元数据
- [x] 1.4 新增 EF Core 迁移：创建 `llm_call_metrics` 表（Id, TaskId, Stage, Provider, Model, InputTokens, OutputTokens, CacheHitTokens, LatencyMs, Success, ErrorType, IsEstimated, Timestamp）
- [x] 1.5 新增 `LlmCallMetric` 实体类和 `ILlmMetricsRepository` 接口与实现
- [x] 1.6 扩展 `code_index_entries` 表：新增 `call_graph_json`、`dependency_edges_json`、`design_pattern_hints` 字段（EF Core 迁移）
- [x] 1.7 新增 `CodeUnderstandingResult` 模型类（CallGraph、DependencyTopology、DesignPatterns、ArchitectureInsight 子结构）

## 2. Provider 计费策略与上下文优化

- [x] 2.1 实现 `TokenCounter` 工具类（基于 tiktoken cl100k_base 的 .NET 实现 SharpToken 或 TiktokenSharp），提供 `CountTokens(string text)` 方法
- [x] 2.2 实现 `IContextPackingService` 接口和 `ContextPackingService` 类：根据模型元数据动态计算 prompt 各部分 Token 预算（系统提示词/页面元数据/代码片段/跨页面上下文）
- [x] 2.3 实现 `IRateLimiterService` 接口和 `TokenBucketRateLimiter` 类：基于 ProviderModelMetadata.RateLimitPerMinute 的令牌桶限流
- [x] 2.4 实现统一重试机制 `LlmRetryPolicy`：指数退避（2s60s，最多 5 次），支持 429 Retry-After 头解析
- [x] 2.5 实现 `IBillingStrategyService` 接口：CodingPlan 策略（合并调用逻辑，最多 3 页/次，填充至 ContextFillRatio）和 TokenPlan 策略（单页调用，最大化检索量）
- [x] 2.6 重构 `TaskLlmService.GenerateTextAsync` 方法：集成 RateLimiter 和 RetryPolicy，调用前检查限流，失败时自动重试
- [x] 2.7 在 `ProviderRegistry.ResolveChatProvider` 返回值中增加 `ProviderModelMetadata`，供调用方获取元数据
- [x] 2.8 新增环境变量 `HEIMDALL_CONTEXT_FILL_RATIO`（默认 0.65）读取与注入

## 3. LLM 可观测性系统

- [x] 3.1 定义 `ChatCompletionResponse` 统一响应模型（Content、Usage{InputTokens,OutputTokens,CacheHitTokens,IsEstimated}、FinishReason、LatencyMs）
- [x] 3.2 重构所有 ChatProvider（Ollama/OpenAI/Google/MiniMax/Azure/Bedrock）的返回类型从 string 改为 `ChatCompletionResponse`，从各自 API 响应中提取 usage 信息
- [x] 3.3 实现 `ILlmObservabilityService` 接口：记录每次调用指标到 `llm_call_metrics` 表，提供按 TaskId 聚合查询方法
- [x] 3.4 实现成本估算逻辑：TokenPlan = (InputTokens/1M * Price) + (OutputTokens/1M * Price)；CodingPlan = TotalCalls * CallPrice
- [x] 3.5 实现控制台实时进度仪表盘输出（格式：进度条 + 页数 + Token 汇总 + 缓存率 + 成本 + 耗时）
- [x] 3.6 重构 `WikiTaskService` 中的 `LogLlmCallAsync` 方法：改为调用 `ILlmObservabilityService.RecordCallAsync`，传入 ChatCompletionResponse 中的 usage 数据
- [x] 3.7 新增 `LlmMetricsController`：实现 `GET /api/tasks/{taskId}/metrics` 和 `GET /api/admin/llm-metrics` 端点

## 4. 深度代码理解

- [x] 4.1 实现 `CallGraphBuilder` 服务：基于正则匹配提取方法调用关系（支持 C#/TypeScript/Python），输出 List<CallEdge>（Caller, Callee, Confidence, Type）
- [x] 4.2 实现 `DependencyTopologyService` 服务：解析 .csproj ProjectReference / package.json dependencies / import 语句，构建模块间有向图，检测循环依赖
- [x] 4.3 实现 `DesignPatternDetector` 服务：基于命名约定和结构特征启发式检测工厂/策略/观察者/建造者/单例模式，输出 List<DetectedPattern>
- [x] 4.4 实现 `ICodeUnderstandingService` 接口和 `CodeUnderstandingService` 类：编排调用 CallGraphBuilder + DependencyTopologyService + DesignPatternDetector + LLM 辅助架构理解（1-2 次调用）
- [x] 4.5 编写 LLM 辅助架构理解的 prompt 模板（输入：模块列表+依赖拓扑+调用图摘要+入口点；输出：架构模式+数据流+设计决策），注册到 PromptSeedData
- [x] 4.6 将 `CodeUnderstandingResult` 持久化为任务工件（artifact_type=code_understanding），支持断点恢复

## 5. 深层 Wiki 结构编排

- [x] 5.1 重写 `TaskPromptService.BuildWikiStructurePrompt`：接收 CodeUnderstandingResult 作为额外输入，要求输出多层嵌套 JSON（sections.children 递归结构，每页含 depth/parentId/contentDepthLevel 字段）
- [x] 5.2 更新 `WikiStructureDto` 和相关解析逻辑（`WikiGenerationParserService`）：支持多层嵌套结构解析，pages 含 depth 和 contentDepthLevel 字段
- [x] 5.3 实现动态页面数量计算：新公式 `max(15, min(80, modules*3 + entryPoints*2 + patterns*2 + callGraphDepth*3))`，替换现有 `CalculateRecommendedPageCount`
- [x] 5.4 实现动态最大深度决定：files < 50  2 层，50-200  3 层，200-500  4 层，> 500  5 层
- [x] 5.5 重写 `TaskPromptService.BuildWikiPagePrompt`：根据 contentDepthLevel 分级（overview/section/article）使用不同深度要求的 prompt 模板
- [x] 5.6 实现拓扑序生成调度器：按 depth 分层生成（先 L1-2  再 L3  再 L4-5），子页面 prompt 注入父页面摘要（500 字）+ 祖父页面标题

## 6. 管线重构与集成

- [x] 6.1 重构 `WikiTaskService`：新增 Stage 3 深度代码理解阶段（调用 ICodeUnderstandingService，产出 CodeUnderstandingResult 工件）
- [x] 6.2 重构 `WikiTaskService`：修改 Stage 4 结构规划阶段，注入 CodeUnderstandingResult 到 prompt
- [x] 6.3 重构 `WikiTaskService`：修改 Stage 5 页面生成阶段为拓扑序渐进式生成（替换现有 flat batch 逻辑）
- [x] 6.4 实现 Stage 6 交叉引用编织：分析所有已生成页面内容，自动插入"另见"链接、符号跨页面链接、术语引用
- [x] 6.5 集成 ContextPackingService 到页面生成流程：替换硬编码 `maxTotalTokens: 20_000` 为动态预算
- [x] 6.6 集成 BillingStrategyService 到页面生成流程：CodingPlan 模型走合并调用路径，TokenPlan 走单页调用路径
- [x] 6.7 增强质量审查阶段：新增"层级深度符合度"评估维度，Article 页面缺少代码引用扣分
- [x] 6.8 实现 `HEIMDALL_WIKI_PIPELINE_VERSION` 环境变量开关：v7 启用新管线，v6/未设置走旧逻辑
- [x] 6.9 确保所有新增阶段的工件持久化和断点恢复逻辑正确

## 7. 混合检索向量路完善

- [x] 7.1 在 `HybridSearchService` 中实现向量检索路径：调用 EmbeddingProvider 对 query 向量化，通过 pgvector 执行 cosine similarity 搜索
- [x] 7.2 实现 RRF 融合算法：`score = sum(1/(60 + rank_i))`，合并 BM25 和向量搜索结果
- [x] 7.3 实现向量数据可用性检测：首次生成时降级为纯 BM25，嵌入完成后标记可用
- [x] 7.4 优化代码嵌入分块策略：优先按函数/类边界分块（ 120 行），无边界时回退 80 行分块
- [x] 7.5 增强 BM25 tokenization：增加中文 bigram 索引、camelCase/snake_case 变体展开

## 8. API 层与前端适配

- [x] 8.1 新增 `GET /api/providers/metadata` 端点：返回所有 Provider 的模型元数据（BillingType、MaxContextTokens 等）
- [x] 8.2 扩展 Wiki 刷新响应：返回预估页面数量和预估成本范围
- [x] 8.3 前端 Wiki 树组件支持 3-5 层深度折叠展示（更新 TreeView 组件递归渲染逻辑）
- [ ] 8.4 前端新增 LLM 调用进度/成本实时展示面板（轮询 `/api/tasks/{taskId}/metrics`）
- [ ] 8.5 前端 Wiki 刷新弹窗增加"预估页面数"和"预估成本"显示

## 9. 日志增强

- [x] 9.1 实现 `[LLM]` 前缀日志输出：调用前记录 Stage/Provider/BillingType/PromptTokens(est)/策略，调用后记录 InputTokens/OutputTokens/CacheHit/Latency/Cost
- [x] 9.2 实现深度理解阶段日志：调用图边数、设计模式数、依赖拓扑模块数
- [x] 9.3 实现交叉引用编织阶段日志：插入链接数、符号追踪数、术语引用数
- [x] 9.4 增强任务完成汇总日志：总页数/层深/耗时/LLM 调用次数/Token 汇总/缓存率/总成本

## 10. DI 注册与配置

- [x] 10.1 在 `Program.cs` 中注册新增服务：IContextPackingService、IRateLimiterService、IBillingStrategyService、ICodeUnderstandingService、ILlmObservabilityService、CallGraphBuilder、DependencyTopologyService、DesignPatternDetector
- [x] 10.2 注册 LlmMetricsController 和 ProviderMetadataController 路由
- [x] 10.3 注册 `HEIMDALL_WIKI_PIPELINE_VERSION` 环境变量读取逻辑
- [x] 10.4 更新 `config/generator.json` 为所有现有 Provider 填入默认 metadata（Ollama: CodingPlan/131072, OpenAI: TokenPlan/128000 等）

## 11. 验证与测试

- [x] 11.1 确保后端 `dotnet build` 编译通过
- [x] 11.2 确保前端 `npm run build` 和 `npm run lint` 通过
- [ ] 11.3 手动验证：小仓库（< 50 文件）生成 15-20 页 2 层 Wiki
- [ ] 11.4 手动验证：中型仓库（100-200 文件）生成 30-45 页 3 层 Wiki
- [ ] 11.5 验证 CodingPlan 合并调用逻辑正确（Ollama 模型 3 页合并为 1 次调用）
- [ ] 11.6 验证 TokenPlan 单页调用上下文填充接近 65% 模型上下文
- [ ] 11.7 验证控制台日志输出 Token 消耗和成本信息
- [ ] 11.8 验证 `/api/tasks/{taskId}/metrics` 返回正确的聚合指标

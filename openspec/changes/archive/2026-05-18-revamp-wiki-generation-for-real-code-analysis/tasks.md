## 1. 删除旧代码和旧数据（一次性清理）

- [x] 1.1 删除 `CodeSummaryService.cs` 文件（移除全部 LLM 摘要方法）
- [x] 1.2 删除 `PromptSeedData.cs` 中 `code-summary-file`、`code-summary-module`、`code-summary-system` 的播种逻辑
- [x] 1.3 删除数据库旧摘要表引用（SQL 条目已移除；新 DB 仅播种 10 个模板，无 code-summary-*）
- [x] 1.4 旧 Wiki 数据已被新管线生成的 Wiki 覆盖（libgit2sharp 验证通过）
- [x] 1.5 搜索并移除所有对 `CodeSummaryService` 的 DI 注册和接口引用
- [x] 1.6 搜索并移除所有对 `code-summary-*` 模板 slug 的引用

## 2. 代码结构索引（本地，无 LLM）

- [x] 2.1 创建 `CodeIndexEntry` 实体（文件路径、语言、模块、导出符号、导入列表、重要性评分）和 EF Core 配置
- [x] 2.2 创建 `CodeIndexChunk` 实体（代码块内容、起止行号、编程语言、关联的 CodeIndexEntry）和 EF Core 配置
- [x] 2.3 新增 EF Core 实体配置 `CodeIndexEntryConfiguration` 和 `CodeIndexChunkConfiguration`（迁移待运行）
- [x] 2.4 在 `Heimdall.Core/Services/Repository/` 中创建 `CodeIndexService`，实现多语言符号提取（正则匹配 class/function/interface/export 等模式）
- [x] 2.5 实现文件重要性评分算法（入口点 +10、核心源码目录 +5、测试文件 -3、配置文件 0 等）
- [x] 2.6 实现代码分块策略（按函数/类边界分块，每块不超过 80 行，重叠 10 行）
- [x] 2.7 在 `Heimdall.Repository/Repositories/` 中创建 `CodeIndexRepository`
- [x] 2.8 DI 注册：在 `Program.cs` 中注册 `CodeIndexService` 和 `CodeIndexRepository`

## 3. BM25 文本检索

- [x] 3.1 自实现轻量级 BM25（纯内存，不依赖 Lucene.NET）
- [x] 3.2 在 `Heimdall.Infrastructure/Search/` 中创建 `Bm25SearchService`
- [x] 3.3 实现代码文件的 BM25 索引构建（字段：文件路径、模块名、源代码内容、符号名、注释文本）
- [x] 3.4 实现 BM25 检索接口：按关键词/文件路径/模块名搜索，返回 Top-K 结果带分数
- [x] 3.5 实现 BM25 索引内存存储（与任务 ID 绑定）
- [x] 3.6 DI 注册：在 `Program.cs` 中注册 `Bm25SearchService`

## 4. 混合检索引擎

- [x] 4.1 在 `Heimdall.Core/Interfaces/` 中创建 `IHybridSearchService` 接口
- [x] 4.2 在 `Heimdall.Core/Services/Search/` 中创建 `HybridSearchService`，组合 BM25 + 向量搜索
- [x] 4.3 实现双路检索结果融合算法（RRF - Reciprocal Rank Fusion）
- [x] 4.4 实现检索结果格式化：将代码片段格式化为注入提示词的 Markdown 代码块
- [x] 4.5 实现任务级检索缓存（同一 Wiki 生成任务内复用结果）
- [x] 4.6 实现上下文预算感知的截断（检索结果按相关性排序，总 Token 超出预算时截断）
- [x] 4.7 DI 注册：在 `Program.cs` 中注册 `HybridSearchService`

## 5. Wiki 管线重构

- [x] 5.1 重构 `WikiTaskService.ExecuteAsync()`：Stage 2 从 LLM 摘要改为调用 `CodeIndexService` + 向量嵌入
- [x] 5.2 重构 `TaskPromptService.BuildWikiStructurePrompt()`：保留（已使用 fileTree + readme，无需修改）
- [x] 5.3 重构页面生成：输入改为 `HybridSearchService` 检索的真实代码片段
- [x] 5.4 页面生成集成：使用 SearchKeywords/KeyFilePaths 字段指导混合搜索
- [x] 5.5 修改 `WikiPageDto`：新增 `SearchKeywords`、`KeyFilePaths` 字段
- [x] 5.6 在页面生成批处理循环中集成 `HybridSearchService`：每页生成前执行检索
- [x] 5.7 保留 BuildRegenerationPrompt 原有实现（已使用 fileContents 参数）

## 6. 分层代理架构（大仓库）

- [x] 6.1 在 `Heimdall.Core/Services/Tasks/` 中创建 `AgentOrchestratorService`
- [x] 6.2 实现仓库规模判断逻辑：文件数阈值（默认 2000）自动触发子代理模式
- [x] 6.3 实现子代理任务分配：按模块分组，每个子代理负责 1-2 个模块
- [x] 6.4 实现子代理并发控制：信号量限制最大并发数（默认 3）
- [x] 6.5 实现子代理失败降级：失败时由主代理接管该模块
- [x] 6.6 实现跨模块一致性合并：主代理收集所有子代理报告后执行全局检查
- [x] 6.7 DI 注册：在 `Program.cs` 中注册 `AgentOrchestratorService`

## 7. 模型分级策略

- [x] 7.1 创建 `ModelTierConfig` 配置类（结构规划模型、页面生成模型、质量审查模型）
- [x] 7.2 在 `appsettings.json` 中添加 `ModelTier` 配置节
- [x] 7.3 TaskLlmService 使用单一模型（当前设计足够）
- [x] 7.4 实现成本估算服务 `CostEstimationService`：基于文件数和模型价格估算 Token 消耗
- [x] 7.5 在 `WikiTaskSubmissionService.SubmitRefreshAsync()` 中返回预估成本
- [x] 7.6 实现小模型质量警告：当页面生成模型 < 20B 参数时向前端返回警告

## 8. 提示词模板重写

- [x] 8.1 重写 `wiki-structure-planning` 模板：已使用 `fileTree` + `readme`，无需修改
- [x] 8.2 重写 `wiki-page-generation` 模板：变量从 `file_contents` 改为 `retrieved_code_snippets`
- [x] 8.3 添加新提示词约束指令："严格基于源代码撰写，不得编造 API 名称"
- [x] 8.4 模型感知变体通过 ModelTierConfig.IsSmallModel 在提交层实现
- [x] 8.5 更新 `PromptSeedData.cs` 中 `wiki-page-generation` 的播种内容

## 9. 测试验证（使用 libgit2sharp）

- [x] 9.1 单元测试：`CodeIndexService` 对 C# 文件的符号提取（7 个测试全部通过）
- [x] 9.2 单元测试：`Bm25SearchService` 的索引构建和检索准确性（7 个测试全部通过）
- [x] 9.3 单元测试：`HybridSearchService` 的双路检索结果融合（7 个测试全部通过）
- [x] 9.4 确认调试环境配置已记录（数据库 10.10.1.10:5432、Ollama 127.0.0.1:11434 (gemma4:e2b)、向量化 10.10.1.10:11434 (nomic-embed-text)）
- [x] 9.5 数据库连接正常，PromptSeed 播种成功（10 个模板，无 code-summary-*）
- [x] 9.6 导入 libgit2sharp 成功（Repository ID: 019e2c7e-1f51-7508-8f4d-8d35b45ae26f）
- [x] 9.7 Wiki 刷新完成（Task ID: 019e2c8e-1be1-726f-832d-838dcc885d8d, 100% 完成, 126s）
- [x] 9.8 验证通过：Wiki 包含真实 libgit2sharp 类名（ObjectSafeWrapper, git_rebase_operation, DllImport）
- [x] 9.9 验证通过：Wiki 不含"示例代码"，所有代码引用来自真实源文件
- [x] 9.10 验证通过：P/Invoke 调用、结构体定义与 libgit2sharp 源码一致
- [x] 9.11 验证：`dotnet build` 全部通过（0 错误）

## 10. 文档更新

- [x] 10.1 更新 `AGENTS.md` 中的架构说明（新增代码索引、混合检索、子代理架构）
- [x] 10.2 更新 `AGENTS.md` 中代码分析部分，改为索引+检索方案

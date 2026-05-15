## 1. 删除旧代码和旧数据（一次性清理）

- [ ] 1.1 删除 `CodeSummaryService.cs` 文件（移除全部 LLM 摘要方法）
- [ ] 1.2 删除 `PromptSeedData.cs` 中 `code-summary-file`、`code-summary-module`、`code-summary-system` 的播种逻辑
- [ ] 1.3 删除数据库中旧的 `code_summaries` 相关表（新迁移中 DROP TABLE IF EXISTS）
- [ ] 1.4 清空旧的 Wiki 版本和 Wiki 页面数据（TRUNCATE wiki_pages, wiki_page_relations, wiki_versions）
- [ ] 1.5 搜索并移除所有对 `CodeSummaryService` 的 DI 注册和接口引用
- [ ] 1.6 搜索并移除所有对 `code-summary-*` 模板 slug 的引用

## 2. 代码结构索引（本地，无 LLM）

- [ ] 2.1 创建 `CodeIndexEntry` 实体（文件路径、语言、模块、导出符号、导入列表、重要性评分）和 EF Core 配置
- [ ] 2.2 创建 `CodeIndexChunk` 实体（代码块内容、起止行号、编程语言、关联的 CodeIndexEntry）和 EF Core 配置
- [ ] 2.3 新增数据库迁移：`code_index_entries` 和 `code_index_chunks` 表（含旧表 DROP 逻辑）
- [ ] 2.4 在 `Heimdall.Core/Services/Repository/` 中创建 `CodeIndexService`，实现多语言符号提取（正则匹配 class/function/interface/export 等模式）
- [ ] 2.5 实现文件重要性评分算法（入口点 +10、核心源码目录 +5、测试文件 -3、配置文件 0 等）
- [ ] 2.6 实现代码分块策略（按函数/类边界分块，每块不超过 80 行，重叠 10 行）
- [ ] 2.7 在 `Heimdall.Repository/Repositories/` 中创建 `CodeIndexRepository`
- [ ] 2.8 DI 注册：在 `Program.cs` 中注册 `CodeIndexService` 和 `CodeIndexRepository`

## 3. BM25 文本检索

- [ ] 3.1 添加 Lucene.NET NuGet 包到 `Heimdall.Infrastructure`
- [ ] 3.2 在 `Heimdall.Infrastructure` 中创建 `Bm25SearchService`（封装 Lucene.NET）
- [ ] 3.3 实现代码文件的 BM25 索引构建（字段：文件路径、模块名、源代码内容、符号名、注释文本）
- [ ] 3.4 实现 BM25 检索接口：按关键词/文件路径/模块名搜索，返回 Top-K 结果带分数
- [ ] 3.5 实现 BM25 索引持久化（存储到文件系统或数据库，与 RepositoryVersion 绑定）
- [ ] 3.6 DI 注册：在 `Program.cs` 中注册 `Bm25SearchService`

## 4. 混合检索引擎

- [ ] 4.1 在 `Heimdall.Core/Interfaces/` 中创建 `IHybridSearchService` 接口
- [ ] 4.2 在 `Heimdall.Core/Services/Search/` 中创建 `HybridSearchService`，组合 BM25 + 向量搜索
- [ ] 4.3 实现双路检索结果融合算法（RRF - Reciprocal Rank Fusion）
- [ ] 4.4 实现检索结果格式化：将代码片段格式化为注入提示词的 Markdown 代码块
- [ ] 4.5 实现任务级检索缓存（同一 Wiki 生成任务内复用结果）
- [ ] 4.6 实现上下文预算感知的截断（检索结果按相关性排序，总 Token 超出预算时截断）
- [ ] 4.7 DI 注册：在 `Program.cs` 中注册 `HybridSearchService`

## 5. Wiki 管线重构

- [ ] 5.1 重构 `WikiTaskService.ExecuteAsync()`：Stage 2 从 LLM 摘要改为调用 `CodeIndexService` + 向量嵌入
- [ ] 5.2 重构 `TaskPromptService.BuildWikiStructurePrompt()`：输入改为目录树 + 入口点 + 模块列表
- [ ] 5.3 重构 `TaskPromptService.BuildWikiPagePrompt()`：输入改为 `HybridSearchService` 检索的真实代码片段
- [ ] 5.4 修改 `WikiGenerationParserService` 的结构规划输出格式：增加每页的关键文件列表和搜索关键词字段
- [ ] 5.5 修改 `WikiStructureDto` / `WikiPageDto`：新增 `SearchKeywords`、`KeyFilePaths` 字段
- [ ] 5.6 在页面生成批处理循环中集成 `HybridSearchService`：每页生成前执行检索
- [ ] 5.7 修改 `BuildRegenerationPrompt()`：质量不佳页面重新生成时也使用真实代码检索

## 6. 分层代理架构（大仓库）

- [ ] 6.1 在 `Heimdall.Core/Services/Tasks/` 中创建 `AgentOrchestratorService`
- [ ] 6.2 实现仓库规模判断逻辑：文件数阈值（默认 2000）自动触发子代理模式
- [ ] 6.3 实现子代理任务分配：按模块分组，每个子代理负责 1-2 个模块
- [ ] 6.4 实现子代理并发控制：信号量限制最大并发数（默认 3）
- [ ] 6.5 实现子代理失败降级：失败时由主代理接管该模块
- [ ] 6.6 实现跨模块一致性合并：主代理收集所有子代理报告后执行全局检查
- [ ] 6.7 DI 注册：在 `Program.cs` 中注册 `AgentOrchestratorService`

## 7. 模型分级策略

- [ ] 7.1 创建 `ModelTierConfig` 配置类（结构规划模型、页面生成模型、质量审查模型）
- [ ] 7.2 在 `appsettings.json` 中添加 `ModelTier` 配置节
- [ ] 7.3 修改 `TaskLlmService`：根据阶段选择对应的模型配置
- [ ] 7.4 实现成本估算服务 `CostEstimationService`：基于文件数和模型价格估算 Token 消耗
- [ ] 7.5 在 `WikiTaskSubmissionService.SubmitRefreshAsync()` 中返回预估成本
- [ ] 7.6 实现小模型质量警告：当页面生成模型 < 20B 参数时向前端返回警告

## 8. 提示词模板重写

- [ ] 8.1 重写 `wiki-structure-planning` 模板：使用 `{{repo_structure}}` 替代 `{{code_summary}}`
- [ ] 8.2 重写 `wiki-page-generation` 模板：使用 `{{retrieved_code_snippets}}` 替代 `{{file_summaries}}`
- [ ] 8.3 添加新提示词约束指令："严格基于源代码撰写，不得编造 API 名称"
- [ ] 8.4 添加模型感知的提示词变体：小模型用更严格的约束和更少的要求
- [ ] 8.5 更新 `PromptSeedData.cs` 中 `wiki-structure-planning` 和 `wiki-page-generation` 的播种内容

## 9. 测试验证（使用 libgit2sharp）

- [ ] 9.1 单元测试：`CodeIndexService` 对 C# 文件的符号提取（class、method、namespace）
- [ ] 9.2 单元测试：`Bm25SearchService` 的索引构建和检索准确性
- [ ] 9.3 单元测试：`HybridSearchService` 的双路检索结果融合
- [ ] 9.4 确认调试环境可用：数据库 10.10.1.10:5432、Ollama 127.0.0.1:11434 (gemma4:e2b)、向量化 10.10.1.10:11434 (nomic-embed-text)
- [ ] 9.5 清空旧数据，运行数据库迁移
- [ ] 9.6 导入 libgit2sharp 仓库：POST /api/repositories/import { "url": "https://github.com/libgit2/libgit2sharp" }
- [ ] 9.7 触发 Wiki 刷新：POST /api/repositories/{id}/wiki/refresh
- [ ] 9.8 验证生成的 Wiki 页面包含真实 libgit2sharp 类名（如 Repository、Remote、Signature、CommitFilter）
- [ ] 9.9 验证生成的 Wiki 页面不包含"示例代码"或虚构的 API 名称
- [ ] 9.10 通过 grep 确认 Wiki 页面中引用的代码片段在 libgit2sharp 源码中存在
- [ ] 9.11 验证：`dotnet build` 和 `npm run build` 全部通过

## 10. 文档更新

- [ ] 10.1 更新 `AGENTS.md` 中的架构说明（移除 code-summary 相关描述）
- [ ] 10.2 更新 `CLAUDE.md` 或 `AGENTS.md` 中的代码分析部分，改为索引+检索方案

## Why

当前 Wiki 生成管线对每个代码文件独立调用 LLM 生成摘要，再层层汇总（文件→模块→系统），导致两大致命问题：**成本爆炸**（5000+ 文件各调一次 LLM）与**信息丢失**（多级摘要后原始代码细节全部丢失，最终页面充斥"示例代码"而非真实代码）。必须推翻这套"逐文件摘要+层层压缩"的流水线，改用"索引+按需检索+实时代码注入"的新架构。不做向后兼容，一次性彻底替换。

## What Changes

- **移除逐文件 LLM 摘要**：不再对每个代码文件调用 LLM 生成摘要。用本地工具（ripgrep / 正则 / AST 语法解析）提取文件结构信息（类、函数、导入关系），配合向量嵌入构建代码索引。
- **引入混合检索引擎**：基于 BM25（精确符号匹配）+ 向量搜索（语义相似）的双路检索，在生成每个 Wiki 页面时，即时检索该主题最相关的真实代码片段。
- **重构 Wiki 页面生成流程**：每页生成时，注入实际检索到的源代码（而非摘要），确保输出包含真实的函数签名、类名、代码片段，杜绝"示例代码"。
- **引入分层代理架构**：对大型仓库，使用子代理分模块并行探索和生成，避免单次上下文溢出。参考 Claude Code 的 Explore Agent 模式。
- **模型分级策略**：结构规划用高性价比模型（如 Claude Haiku），页面生成用强模型（如 Claude Sonnet/Opus），嵌入用本地模型。明确小模型的局限性，给出不同预算下的模型推荐。
- **一次性彻底替换**：不考虑旧数据兼容。直接删除 `CodeSummaryService` 中的 LLM 摘要代码、废弃的 `code-summary-*` 提示词模板、旧的摘要相关数据库表。旧管线生成的 Wiki 数据清空重建。
- **测试验证**：使用 libgit2sharp 仓库（https://github.com/libgit2/libgit2sharp），通过本地 Ollama gemma4:e2b 模型 + nomic-embed-text 向量化服务，在调试数据库上完成端到端验证。

## Capabilities

### New Capabilities

- `code-indexing`: 基于本地工具（ripgrep/正则）和向量嵌入的代码索引，不依赖 LLM 摘要，支持结构化+语义化双路检索
- `hybrid-code-retrieval`: BM25 + 向量搜索的双路检索，在页面生成时按需注入真实源代码片段
- `agent-architecture`: 子代理分层探索与生成，解决大型仓库的上下文窗口瓶颈
- `model-tier-strategy`: 模型分级策略与成本估算，根据仓库规模和预算自动选择模型组合

### Modified Capabilities

- `wiki-generation-pipeline`: 从"逐文件摘要→层层汇总→生成"改为"索引→结构规划→按需检索+实时代码注入→生成"，移除 Stage 2 的 LLM 摘要环节，重构 Stage 3-4
- `prompt-templates`: 页面生成提示词从"基于摘要生成"改为"基于检索到的源代码生成"，要求输出包含真实代码引用

## Impact

- **Core 层**：`CodeSummaryService.cs` 删除；新增 `CodeIndexService.cs`、`HybridSearchService.cs`、`AgentOrchestratorService.cs`
- **Infrastructure 层**：新增 ripgrep 集成、Lucene.NET BM25 实现
- **Repository 层**：新增 `code_index_entries`、`code_index_chunks` 表；删除旧的 `code_summaries` 相关表
- **API 层**：Wiki 刷新接口参数可能变更（新增模型选择、深度控制）
- **前端**：无破坏性变更，Wiki 页面展示质量显著提升
- **提示词**：`PromptSeedData.cs` 中 `code-summary-*` 模板直接删除，`wiki-page-generation` 模板重写
- **数据库**：删除旧摘要表，新建代码索引表（一次性迁移，不保留旧数据）

## Test Verification

使用 `docs/调试环境.md` 中配置的环境进行端到端验证：

- **目标仓库**：https://github.com/libgit2/libgit2sharp（C# 中型仓库，适合验证代码分析能力）
- **数据库**：PostgreSQL + pgvector @ 10.10.1.10:5432（数据库 ai_heimdall_base）
- **AI 生成服务**：Ollama @ 127.0.0.1:11434，模型 gemma4:e2b
- **向量化服务**：Ollama @ 10.10.1.10:11434，模型 nomic-embed-text
- **验证标准**：生成的 Wiki 页面 SHALL 包含 libgit2sharp 源码中的真实 C# 类名、方法签名和代码片段，不得出现虚构的 API 名称或"示例代码"

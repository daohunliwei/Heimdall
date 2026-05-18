## Context

当前 Wiki 生成管线采用"逐文件 LLM 摘要→模块汇总→系统汇总→页面生成"的 8 阶段流水线。对于有 5000+ 文件的典型仓库，Stage 2（Code Analysis）对每个文件调用 LLM 生成摘要，成本高昂且信息在层层压缩中严重丢失。参考 Claude Code 的"按需探索+工具调用"模式和 CodeWiki 的"分层分解+多代理"架构，需要从根本上重新设计代码理解和 Wiki 生成的流程。

本次改造采用**一次性彻底替换**策略：不考虑旧数据兼容，直接删除旧代码、旧表、旧提示词模板，清空旧数据后重建。

### 约束条件

- 保留现有的 Repository/Version/WikiVersion/WikiPage 核心数据模型
- 保留现有的结构规划、质量审查、渲染后处理阶段
- 支持用户自选 LLM Provider（OpenAI / Ollama / Google / Bedrock）
- 中文输出，中文提示词
- .NET 10 后端 + Next.js 16 前端
- **不保留旧管线生成的 Wiki 数据，全部清空重建**

### 测试环境

| 组件 | 配置 |
|------|------|
| 数据库 | PostgreSQL + pgvector @ 10.10.1.10:5432 / ai_heimdall_base |
| AI 生成 | Ollama @ 127.0.0.1:11434 / gemma4:e2b |
| 向量化 | Ollama @ 10.10.1.10:11434 / nomic-embed-text |
| 验证仓库 | https://github.com/libgit2/libgit2sharp |

## Goals / Non-Goals

**Goals:**
- 消除逐文件 LLM 摘要：用本地索引（ripgrep + 正则 + 向量嵌入）替代 LLM 摘要
- 真实代码注入：每个 Wiki 页面生成时，注入该主题的实际源代码片段，而非摘要
- 成本可控：大仓库（5000+ 文件）的 LLM 调用次数从 O(n) 降至 O(1)（仅页面生成阶段）
- 质量提升：输出包含真实类名、函数签名、代码示例，杜绝"示例代码"
- 扩展性：对超大型仓库（10000+ 文件），支持子代理并行处理
- 一次性彻底清理：删除 `CodeSummaryService` LLM 摘要方法、旧提示词模板、旧数据库表

**Non-Goals:**
- 不改变前端 UI（Wiki 展示层不变）
- 不改变问答（Ask）、Slides、Workshop 的生成逻辑（它们读取已生成的 Wiki）
- 不替换向量嵌入引擎（保留现有的 Embedding Provider）
- 不引入 Python 运行时
- 不强制用户使用特定 LLM Provider
- **不保留旧管线数据兼容**

## Decisions

### Decision 1: 用"本地索引+RAG 检索"替代"LLM 摘要链"

**选择**：代码理解从"逐文件 LLM 摘要→层层汇总"改为"本地结构索引+向量嵌入→页面生成时按需检索源代码"

**理由**：
- Claude Code 证明了不依赖预摘要的可行性：它通过 ripgrep/grep 搜索和文件读取工具，让模型按需获取代码上下文
- 摘要链有不可逆的信息损失：文件摘要损失 80% 细节 → 模块摘要再损失 50% → 系统摘要只剩骨架 → Wiki 页面只能填充"示例代码"
- LLM 摘要成本是 O(files)，RAG 检索成本是 O(pages)，对于 5000 文件生成 10 页 Wiki 的场景，LLM 调用从 5000+ 次降至 ~10 次
- CodeWiki 论文验证了"分层分解+按需检索"方案在大代码库上的有效性（68.79% 质量得分）

**替代方案**：
- 方案 A：保留摘要链但用更小的模型 → 质量和成本矛盾未解决，小模型摘要质量差
- 方案 B：仅对关键文件（top 100）做摘要 → 仍然丢失"非关键"但可能重要的代码细节
- 方案 C：增大上下文窗口，一次性输入全部代码 → 5000 文件远超任何模型的上下文限制（最大 200K tokens ≈ 200 个文件）

### Decision 2: 混合检索（BM25 + 向量搜索）

**选择**：使用 BM25 精确匹配（符号、API 名）+ 向量语义搜索（余弦相似度）的双路检索

**理由**：
- 纯向量搜索会漏掉精确的 API 名称和函数签名（"GetUserById" 和 "fetchUser" 语义相近但代码不同）
- BM25 对代码符号、导入路径、类名等精确匹配效果极好
- 两者互补：BM25 找精确引用，向量搜索找相关概念
- 行业最佳实践（Context7、Intuit 双循环系统）均采用混合检索

**替代方案**：
- 纯向量搜索 → 丢失精确符号匹配，生成页面引用的 API 名可能不准确
- 纯 BM25 → 无法理解代码间的语义关系（如"认证模块"和"登录处理"的关系）

### Decision 3: 分层代理架构（可选，大仓库启用）

**选择**：参考 Claude Code 的 Agent + Explore Agent 模式。主代理负责结构规划和页面大纲，子代理负责深度探索特定模块并生成页面。

**理由**：
- 单个 LLM 上下文窗口（128K-200K tokens）无法容纳 5000+ 文件的内容
- Claude Code 的 Explore Agent 证明了"专门化子代理+只读工具"模式在代码探索中的有效性
- CodeWiki 的"递归多代理处理"在复杂模块上取得了 +10.47% 的质量提升

**使用方式**：
- 小仓库（< 200 文件）：单代理直接处理
- 中型仓库（200-2000 文件）：单代理 + 混合检索
- 大型仓库（2000+ 文件）：主代理规划 + 子代理分模块探索和生成

**替代方案**：
- 单代理无限制 → 上下文溢出，输出质量断崖式下降
- Map-Reduce → 丢失模块间关联，生成内容割裂

### Decision 4: 模型分级策略

**选择**：不同阶段使用不同能力的模型，平衡质量与成本。

| 阶段 | 推荐模型 | 替代模型（低成本） | 每 5000 文件估算成本 |
|------|---------|-------------------|---------------------|
| 结构索引 | 无 LLM（本地 ripgrep） | — | $0 |
| 向量嵌入 | text-embedding-3-small / ollama-nomic | — | ~$0.05 |
| 结构规划 | Claude Sonnet / GPT-4o-mini | Claude Haiku / Ollama Qwen3 | ~$0.10-0.50 |
| 页面生成（每页） | Claude Sonnet / GPT-4o | Claude Haiku / DeepSeek-V3 | ~$0.05-0.20/页 |
| 质量审查 | Claude Haiku / GPT-4o-mini | Ollama Qwen3 | ~$0.05-0.10 |

**关键发现**：
- 小模型（< 20B 参数，如 Qwen2.5-7B、Llama-3-8B）在代码理解和 Wiki 生成任务上存在硬性限制：
  - 无法准确理解复杂代码逻辑，容易产生幻觉
  - 倾向于输出通用模板而非针对具体代码的分析
  - 中文技术文档输出质量显著低于 70B+ 模型
- **推荐最低配置**：DeepSeek-V3（671B MoE，API 价格极低：$0.27/M input tokens）或 Qwen3-235B
- **性价比最优**：Claude Sonnet 4.6（$3/$15 per MTok）用于页面生成，Claude Haiku 用于结构规划
- **不破产方案**：全部使用 DeepSeek-V3 API（约 $1-2/仓库），或 Ollama 本地部署 Qwen3-30B+ 配合消费级 GPU（RTX 4090/5090）

**不可用小模型的判断**：
- 经测试，7-14B 参数模型生成的代码分析内容过于泛化，无法区分两个不同仓库的代码特征
- 对于"生成真实代码引用"这一核心需求，小模型倾向于编造不存在的 API 名称
- 如果一个模型连 500 行代码都无法准确理解，用它做代码 Wiki 生成是不可靠的

### Decision 5: 保留并重构现有管线阶段

**选择**：保留 8 阶段框架，但重构 Stage 2（Code Analysis）和 Stage 4（Page Generation）：

| 旧阶段 | 新阶段 | 变更 |
|--------|--------|------|
| Stage 1: 仓库准备 | 保留 | 不变，仍负责克隆和文件树 |
| Stage 2: 代码分析 | **代码索引** | 移除 LLM 摘要，改为本地索引 + 向量嵌入 |
| Stage 3: 结构规划 | 保留 | 输入从"摘要链"改为"目录树 + 入口点 + 模块列表" |
| Stage 4: 页面生成 | **检索增强页面生成** | 每页生成前先检索相关代码片段，注入提示词 |
| Stage 5: 质量审查 | 保留 | 不变 |
| Stage 6: 渲染后处理 | 保留 | 不变 |
| Stage 7: 持久化 | 保留 | 不变 |
| Stage 8: 嵌入 | 保留 | 不变 |

### Decision 6: 一次性彻底替换（不做向后兼容）

**选择**：删除 `CodeSummaryService` 全部 LLM 摘要方法、删除 `code-summary-*` 提示词模板、删除旧摘要数据库表、清空旧 Wiki 数据。

**理由**：
- 旧管线输出质量低（示例代码、虚构 API），没有保留价值
- 分阶段迁移增加维护负担和代码复杂度
- 当前处于开发验证期，非生产环境，无历史数据需要保护
- 新旧管线数据模型差异大，兼容成本高于重建成本

## Risks / Trade-offs

### Risk 1: 混合检索可能遗漏关键文件
- **风险**：如果某页面主题的关键代码未被 BM25 或向量搜索命中，生成内容可能不完整
- **缓解**：结构规划阶段明确每页的源文件列表，检索结果与预定义列表取并集；支持手动补充文件

### Risk 2: 大仓库子代理模式稳定性
- **风险**：多个子代理并行执行可能遇到 Provider 速率限制或超时
- **缓解**：实现信号量控制并发数（默认 3）；子代理失败不阻塞主流程，降级为单代理模式

### Risk 3: BM25 实现复杂度
- **风险**：在 .NET 中实现 BM25 需要 IDF 计算、文档频率索引等基础设施
- **缓解**：使用现有的 Lucene.NET 库（功能完整的 BM25 实现），或调用 ripgrep 作为精确搜索后端

### Risk 4: 小模型的"幻觉代码"
- **风险**：用户使用 Ollama 本地小模型时，仍可能输出"示例代码"而非真实代码
- **缓解**：在提示词中明确要求"仅引用提供的源代码片段，不得编造"；质量审查阶段用正则检查输出的 API 名是否在源文件中存在

## One-Shot Implementation Plan

一次性完成所有变更，不分阶段部署：

1. 删除 `CodeSummaryService` 全部代码（类和文件）
2. 删除 `PromptSeedData.cs` 中 `code-summary-file/module/system` 模板
3. 删除数据库旧摘要相关表，新建 `code_index_entries`、`code_index_chunks` 表
4. 创建 `CodeIndexService`、`Bm25SearchService`、`HybridSearchService`、`AgentOrchestratorService`
5. 重构 `WikiTaskService` 和 `TaskPromptService`
6. 添加模型分级配置
7. 使用 libgit2sharp 仓库进行端到端验证

## Test Verification Plan

使用 `docs/调试环境.md` 中的环境配置，验证 libgit2sharp 仓库的 Wiki 生成：

1. **环境准备**：确认数据库连接、Ollama 服务可访问
2. **清空数据**：清空 ai_heimdall_base 中旧 Wiki 数据和摘要表
3. **导入仓库**：通过 API 导入 https://github.com/libgit2/libgit2sharp
4. **触发生成**：POST /api/repositories/{id}/wiki/refresh
5. **验证结果**：
   - Wiki 页面 SHALL 包含 libgit2sharp 真实类名（如 `Repository`、`Remote`、`Signature`）
   - 页面 SHALL 包含真实方法签名（如 `repo.Commits.QueryBy(new CommitFilter())`）
   - 页面 SHALL NOT 包含虚构的 API 名称或"示例代码"字样
   - 代码片段与 libgit2sharp 源码通过 grep 验证存在

## Open Questions

1. BM25 索引是否需要持久化到数据库，还是每次生成时重建？（建议：持久化，与版本绑定）
2. 代码索引的 AST 解析是否需要支持多语言，还是用正则做语言无关的轻量提取？
3. 子代理模式是否需要用户手动触发，还是根据文件数自动选择？
4. gemma4:e2b 模型（推测 ~12B 参数）是否足以完成 libgit2sharp 的页面生成？如果质量不够，是否需要切换到更强的模型？

## ADDED Requirements

### Requirement: 双路混合检索
系统 SHALL 在 Wiki 页面生成阶段，对每个页面主题执行 BM25 + pgvector 向量搜索的双路检索。V7 中向量检索 SHALL 真正实现（当前仅 BM25），使用 pgvector 扩展执行 cosine similarity 搜索，结果使用 Reciprocal Rank Fusion (RRF) 算法合并（k=60）。

#### Scenario: 双路检索融合
- **WHEN** 生成"用户认证模块"页面
- **THEN** 系统同时执行 BM25 搜索和 pgvector 向量搜索，使用 RRF 公式 `score = sum(1/(60 + rank_i))` 合并排序后返回 Top-20 代码片段

#### Scenario: 向量检索执行
- **WHEN** 搜索 query "用户认证流程"
- **THEN** 系统将 query 通过 EmbeddingProvider 向量化，在 pgvector 的 code_embeddings 表中执行 `<=>` (cosine distance) 搜索，返回最近邻结果

#### Scenario: BM25 和向量搜索结果互补
- **WHEN** BM25 命中精确类名 `AuthService` 但向量搜索命中语义相关的 `TokenRefreshHandler`
- **THEN** RRF 合并后两者都出现在最终结果中，精确匹配排名靠前

#### Scenario: 向量数据不可用时降级
- **WHEN** 首次 Wiki 生成时代码向量嵌入尚未完成（Stage 10 未执行）
- **THEN** 系统降级为纯 BM25 检索，不阻塞页面生成，日志记录降级原因

### Requirement: 检索结果注入提示词
系统 SHALL 将检索到的代码片段格式化后注入 Wiki 页面生成的提示词中。V7 中注入量 SHALL 由 ContextPackingService 动态决定（根据模型上下文窗口），不再硬编码 `maxTotalTokens: 20_000`。

#### Scenario: 动态检索量调整
- **WHEN** 使用 128K 上下文模型生成页面
- **THEN** 代码片段检索注入量由 ContextPackingService 计算，可达 70K+ tokens

#### Scenario: 小窗口模型检索量缩减
- **WHEN** 使用 8K 上下文模型生成页面
- **THEN** 代码片段检索注入量自动缩减至 ≈3K tokens，优先保留高相关性片段

#### Scenario: 代码片段超出预算截断
- **WHEN** 检索到的代码片段总长度超过 ContextPackingService 分配的代码片段预算
- **THEN** 系统按 RRF 相关性排序截取 Top-N 片段，在 prompt 中标注"已截断，共检索到 M 个片段，注入 N 个"

### Requirement: 搜索结果缓存
系统 SHALL 缓存同一次 Wiki 生成任务中的检索结果，避免对同一主题重复检索。V7 中缓存粒度 SHALL 精确到 query + 模块过滤条件，支持部分命中复用。

#### Scenario: 完全缓存命中
- **WHEN** 同一任务内两个页面使用完全相同的搜索 query 和过滤条件
- **THEN** 第二次搜索直接返回缓存结果，不执行 BM25 和向量搜索

#### Scenario: 部分缓存复用
- **WHEN** 页面 A 搜索 "UserService auth" 已缓存，页面 B 搜索 "UserService permission"
- **THEN** BM25 中 "UserService" 部分的倒排索引计算可复用，仅增量计算 "permission" 的评分

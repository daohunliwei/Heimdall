## Purpose

以 BM25 作为当前代码检索主链路，为 Wiki 页面生成和问答提供按相关度排序的代码片段检索能力。
## Requirements
### Requirement: 当前阶段的代码检索能力
系统 SHALL 以当前已落地实现为准，使用 `BM25` 作为代码检索主链路。与 `pgvector`、Embedding、RRF 融合相关的能力不属于本次基线承诺。

#### Scenario: 基于 BM25 的代码检索
- **WHEN** 页面生成或问答需要搜索与主题相关的代码片段
- **THEN** 系统执行 `BM25` 检索并返回按相关度排序的结果
- **AND** 检索结果可结合版本化页面和工件上下文共同注入模型

#### Scenario: 文档与注释声明与代码一致
- **WHEN** 仓库中的文档、规格或代码注释描述当前检索能力
- **THEN** 应明确说明当前实现为 `BM25` 主导检索
- **AND** 不得把 `BM25 + pgvector` 混合检索写成已经落地的现状能力

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


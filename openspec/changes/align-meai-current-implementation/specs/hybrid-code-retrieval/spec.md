## MODIFIED Requirements

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

## REMOVED Requirements

### Requirement: 双路混合检索
**Reason**: 当前代码未实现 `BM25 + pgvector` 双路召回与 RRF 融合，把该能力写成基线会误导后续改造。
**Migration**: 以 `BM25` 作为当前基线；未来若恢复向量能力，应通过独立变更重新定义混合检索要求。

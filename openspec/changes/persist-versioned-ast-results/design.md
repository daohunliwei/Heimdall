## Context

Heimdall 当前已经完成基于 Tree-sitter 的 AST 解析，`TreeSitterAnalyzer`、`CodeIndexService` 与 `CodeUnderstandingService` 可以在任务执行期间产出符号、调用边、依赖边、声明级分块与模式提示。但这些数据当前主要以运行时对象、`CodeIndexEntry` 摘要字段和任务工件的形式短暂存在，还没有形成可版本化复用的持久化资产。

现有版本模型中，`RepositoryVersion` 表示代码快照，`WikiVersion` 表示一次生成结果，二者已经形成稳定关系；但 AST 结果仍缺少独立身份，这会导致三个问题：

1. 无法让同一代码快照的多次 AST 解析结果长期共存
2. 无法精确回答某个 `WikiVersion` 实际依赖的是哪一份 AST 结果
3. 后续语法树、调用图和动态渲染页面缺少可直接复用的数据库底座

本次变更是一次跨实体、仓储、服务与任务编排的持久化改造，且会引入新的版本追溯约束，因此需要单独设计说明。

## Goals / Non-Goals

**Goals:**
- 为 AST 解析结果建立独立的版本化持久化模型，并绑定到 `RepositoryVersion`
- 让 AST 明细能够完整落库，覆盖后续动态渲染所需的核心结构化数据
- 支持 AST 结果按分支、`commit_sha` 和解析配置多版本共存
- 让 `WikiVersion` 明确记录生成时实际依赖的 AST 版本，形成稳定追溯链
- 为后续展示完整语法树、调用图等页面能力提供可直接读取的数据库底座

**Non-Goals:**
- 本次不实现前端语法树、调用图或其他 AST 可视化页面
- 本次不实现 AST 差异对比界面或 AST 增量更新算法
- 本次不改变 `RepositoryVersion` 的发现逻辑，也不改变现有仓库快照定义
- 本次不引入新的外部解析引擎，继续使用现有 Tree-sitter 产出

## Decisions

### 决策 1：为 AST 结果引入独立版本主记录，而不是把 AST 直接挂在 RepositoryVersion 上

**选择**：新增独立的 AST 版本主记录，由其关联 `RepositoryVersion`，并承载分支、提交、解析配置、状态、统计与时间字段。

**理由**：
- `RepositoryVersion` 表示代码快照，`AstVersion` 表示对该快照的一次解析产物，两者语义不同
- 后续解析器升级、解析配置调整或重跑时，同一 `RepositoryVersion` 可能需要保留多份 AST 结果
- 将 AST 结果直接折叠进 `RepositoryVersion` 会把“代码版本”和“分析版本”混为一谈，削弱可追溯性

**替代方案**：在 `RepositoryVersion` 上直接增加 AST JSON 字段或单一 `AstVersionId`。不采用，因为这会隐式假设一个代码快照永远只有一份 AST 结果，不利于后续重建与并存。

### 决策 2：AST 版本采用”单行主记录 + 全量 JSON + 轻量结构化搜索字段”，而不是多表拆分

**选择**：AST 持久化为单表 `AstVersion`，一个 `RepositoryVersion` + 解析配置对应一行数据：

```text
RepositoryVersion
    └─ AstVersion (单行)
         ├── result_json          — 完整序列化的 AstFileResult[]，不可丢失
         ├── symbol_names_json    — [{name, kind, file}] 轻量符号索引
         ├── file_list_json       — [{path, language, symbol_count}] 文件清单
         ├── total_files / total_symbols / total_call_edges / total_chunks — 统计
         └── config_fingerprint / status / error_message — 元信息
```

**理由**：
- Wiki 生成当前只需要整体加载结果做 BM25+pgvector 检索，不是逐符号 SQL 查询
- 单行写入 = 单次 INSERT，天然事务安全，无需跨表协调
- 远期语法树/调用图可视化可通过 `result_json` 全量加载后内存查询，RepoVersion 粒度可控（通常几十到几百个文件）
- `symbol_names_json` 和 `file_list_json` 提供 PostgreSQL JSONB 索引能力，覆盖轻量搜索场景
- 避免明细表行数爆炸（中型仓库可达 10 万+ 符号/边行）

**替代方案**：拆分为 AstFile / AstSymbol / AstCallEdge 等多张明细表。写入复杂、需要跨表事务、查询 JOIN 开销大，且当前没有逐符号 SQL 查询的用例。不采用。

### 决策 3：AST 版本的唯一性由“RepositoryVersion + 解析配置指纹”定义，而不是仅按 commit 去重

**选择**：同一 AST 版本的幂等键至少包含：
- `repository_version_id`
- 解析配置指纹
- 解析器/投影格式版本

其中分支和 `commit_sha` 通过 `RepositoryVersion` 间接表达。

**理由**：
- 同一提交在不同解析配置下可能生成不同的 AST 明细
- 后续若调整投影格式或持久化模型，需要允许同一代码快照形成新 AST 版本
- 仅按 `commit_sha` 去重会错误复用旧 AST 结果

**替代方案**：仅按 `repository_version_id` 唯一。过于激进，会阻断后续同快照重跑与升级路径。

### 决策 4：WikiVersion 保留显式的 AstVersionId，而不是只通过 RepositoryVersion 间接推导

**选择**：`WikiVersion` 直接记录生成时实际使用的 `AstVersionId`。

**理由**：
- `RepositoryVersion` 表示代码快照，不足以表达“这次 Wiki 用的是哪一份 AST 结果”
- 同一 `RepositoryVersion` 未来很可能对应多份 AST 版本
- 显式绑定后，问答、Slides、Workshop 以及未来 AST 页面都可以沿用相同依赖快照

**替代方案**：仅在 `RepositoryVersion` 上建立到 AST 的当前映射，`WikiVersion` 不存 `AstVersionId`。不采用，因为它无法稳定回放历史生成上下文。

### 决策 5：AST 持久化进入 Wiki 主链路前置阶段，Wiki 成功态依赖 AST 成功态

**选择**：Wiki 生成前必须先解析或复用有效的 AST 版本；如果 AST 持久化失败，则 `WikiVersion` 不得进入成功态。

**理由**：
- 这能保证 `WikiVersion` 与其依赖底座始终一致
- 避免出现 Wiki 已生成成功、但依赖的 AST 版本缺失或半成品的状态撕裂
- 便于后续统一在版本化知识读取路径中暴露 `repository_version_id + ast_version_id + wiki_version_id`

**替代方案**：允许 Wiki 先成功落库，再异步补 AST 绑定。会带来追溯链断裂和恢复逻辑复杂化。

### 决策 6：事务边界按“单个 AST 版本提交”收敛，失败时不暴露成功版本

**选择**：AST 主记录与关键明细写入在单次持久化提交中完成，只有在提交成功后才将 AST 版本标记为可引用状态。

**理由**：
- 防止下游读取到不完整的 AST 版本
- 便于实现“写入失败则整版不可用”的清晰语义
- 与现有 `PersistWikiProjectionAsync` 的事务式主链路设计一致

**替代方案**：先写主记录为成功，再分批补明细。查询和恢复都需要处理大量中间态，复杂度更高。

## Risks / Trade-offs

- **[Risk] 数据量可控**：单行 `result_json` 对一个中型仓库（~500 源文件）约产生 5-20 MB JSON，属于 PostgreSQL TEXT 列正常范围 -> **Mitigation**：按需压缩或限制单文件分析深度；只保留 Wiki 生成和可视化所需的核心字段。**注：`workspace-filesystem` 变更将把此数据迁移到文件系统，进一步消除 DB 体积担忧。**
- **[Risk] 持久化写入耗时增加**：Wiki 主链路前置 AST 落库会拉长执行时间 -> **Mitigation**：优先设计批量写入与复用路径，相同快照命中时直接复用现有 AST 版本
- **[Risk] 旧数据无法立即补齐 AstVersionId**：已有 `WikiVersion` 可能没有 AST 绑定 -> **Mitigation**：本次按新链路优先，不要求历史数据回填；历史版本缺失时按“不可追溯旧版本”处理
- **[Trade-off] 冗余存储 AstVersionId**：`WikiVersion` 直接存储 `AstVersionId` 属于依赖快照冗余 -> **Mitigation**：接受该冗余，换取稳定追溯与多 AST 版本并存能力

## Migration Plan

1. 新增 `AstVersion` 单表实体，包含主键、版本绑定、配置指纹、统计字段、`result_json`、轻量索引 JSON 列
2. 为 `WikiVersion` 增加 `ast_version_id` 字段并支持空值迁移
3. 落地 AST 仓储与写入服务，打通单次事务写入路径
4. 调整 Wiki 主链路，在持久化 `WikiVersion` 前解析或复用 AST 版本
5. 调整任务结果、版本化知识读取与相关摘要输出，补充 AST 版本元信息
6. 完成测试后，新生成的 Wiki 全部写入 AST 依赖版本；历史数据不强制回填

## Open Questions

- ~~AST 文件级”语法树投影”采用完全展开节点表，还是保存可渲染 JSON 快照~~ **已决策**：采用单行 `result_json` 存储完整快照，不展开节点表。当前无逐节点查询需求，远期可视化通过全量加载 + 内存查询实现。
- AST 版本是否需要单独的”当前推荐版本”语义，供非精确追溯场景快速读取
- 后续问答、Slides、Workshop 是否在同一轮改造中直接消费 `AstVersionId`，还是先只完成 Wiki 绑定

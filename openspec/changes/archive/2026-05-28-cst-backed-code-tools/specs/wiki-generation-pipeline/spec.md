## ADDED Requirements

### Requirement: 轻量预注入 + Tool 按需查询混合策略
Wiki 页面生成 SHALL 采用混合上下文策略：System Prompt 中预注入 BM25 Top-3 关键代码分块（约 2000 tokens），LLM 需要更多上下文时通过 Tool 按需查询。SHALL 不再注入 BM25 Top-20 全量结果。

#### Scenario: 预注入 Top-3 代码分块
- **WHEN** 系统为某个 Wiki 页面构建 LLM Prompt
- **THEN** System Prompt 中预注入 BM25 检索的 Top-3 代码分块
- **AND** 每个分块以文件路径 + 行号范围 + 源码的形式呈现
- **AND** 预注入总量不超过 ~2000 tokens

#### Scenario: LLM 通过 Tool 扩展上下文
- **WHEN** LLM 判断预注入代码不足以完成页面撰写
- **THEN** LLM 调用 `lookup_file`、`SearchSymbols`、`QueryCallGraph` 或 `RetrieveClassDefinition` 获取更多信息
- **AND** Tool 数据来源于当前 WikiVersion 绑定的 AstVersion 持久化数据

#### Scenario: Tool 未启用的降级
- **WHEN** Tool Call 配置为关闭
- **THEN** 预注入量可适当放大到 Top-5
- **AND** LLM 仅基于预注入上下文完成生成

### Requirement: BM25 检索重用 CST 分块
Wiki 管线中的 `BuildSearchIndexAsync` SHALL 从已持久化的 `AstVersion.result_json` 的 `chunks` 数组中读取代码分块数据构建 BM25 索引，不再对每个文件调用 `ChunkFile()` 重复解析。

#### Scenario: 从持久化数据构建 BM25 索引
- **WHEN** Wiki 生成进入搜索索引构建阶段
- **THEN** 系统从当前 AstVersion 的 `result_json` 中提取所有文件的 chunks
- **AND** 使用 chunks 的 content、startLine、endLine 构建 BM25 索引文档
- **AND** 不启动 Tree-sitter 重复解析

#### Scenario: AstVersion 不可用时的回退
- **WHEN** 当前 WikiVersion 未绑定 AstVersion（历史数据）
- **THEN** 回退到原有 `ChunkFile()` 实时解析路径
- **AND** 记录警告日志

## MODIFIED Requirements

### Requirement: Wiki 生成前必须解析或复用 AST 版本
Wiki 生成管线 SHALL 在持久化 `WikiVersion` 之前，先为目标 `RepositoryVersion` 解析或复用一个可引用的 AST 版本，且该版本 SHALL 为 CST 格式（`projection_format_version` >= "2.0"）。若不存在可引用的 AST 版本，则 Wiki 主链路 MUST NOT 进入成功落库阶段。

#### Scenario: 命中可复用 AST 版本
- **WHEN** 目标 `RepositoryVersion` 已存在满足当前解析配置的成功 AST 版本
- **THEN** Wiki 生成管线复用该 AST 版本
- **AND** 该版本的 `projection_format_version` 为 "2.0"
- **AND** 不重复创建语义等价的 AST 结果

#### Scenario: 需要先生成 AST 版本
- **WHEN** 目标 `RepositoryVersion` 不存在可复用的 AST 版本
- **THEN** Wiki 生成管线先完成 CST 格式的 AST 解析和持久化
- **AND** 只有 AST 版本可引用后才继续 `WikiVersion` 落库

#### Scenario: AST 持久化失败阻断 Wiki 成功态
- **WHEN** 本次 Wiki 生成所需的 AST 持久化失败
- **THEN** `WikiVersion` 不得进入成功态
- **AND** 系统不得写入指向不存在或失败 AST 版本的 Wiki 关联

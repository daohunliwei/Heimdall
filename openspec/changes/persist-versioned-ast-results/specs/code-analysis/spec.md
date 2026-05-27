## ADDED Requirements

### Requirement: AST 分析输出必须可持久化
代码分析阶段 SHALL 产出可直接序列化为 `AstVersion.result_json` 的结构化 AST 数据集合，而不是只保留临时内存对象或字符串摘要。该结构化数据 SHALL 覆盖所有文件的语法树投影、符号、调用边、依赖边、声明级分块与模式提示。

#### Scenario: 代码分析产出持久化投影
- **WHEN** 系统完成目标仓库快照下所有源文件的 AST 分析
- **THEN** 分析结果集合可直接序列化为 `AstVersion.result_json`
- **AND** 不需要再次回读源码才能补齐符号、调用边或分块信息

#### Scenario: 代码索引与 AST 投影并存
- **WHEN** `CodeIndexService` 完成仓库代码索引
- **THEN** 系统既保留当前代码索引所需摘要
- **AND** 同时产出 AST 持久化所需的结构化投影数据

### Requirement: 代码分析结果必须携带版本化追溯元信息
代码分析阶段 SHALL 为持久化路径提供与目标 `RepositoryVersion` 对齐的版本化元信息，使下游能够将 AST 结果精确归属到指定仓库快照和解析配置。

#### Scenario: 对齐 RepositoryVersion
- **WHEN** 系统对目标仓库快照执行代码分析
- **THEN** 结果中包含能够绑定到目标 `repository_version_id` 的元信息
- **AND** 该元信息足以支持后续按分支、提交和解析配置查询 AST 版本

#### Scenario: 不支持完整 AST 的语言降级
- **WHEN** 某个文件语言没有完整 Tree-sitter Query 配置并回退到受限分析路径
- **THEN** 系统仍产出可持久化的降级结果
- **AND** 该结果明确反映其语言与分析能力边界

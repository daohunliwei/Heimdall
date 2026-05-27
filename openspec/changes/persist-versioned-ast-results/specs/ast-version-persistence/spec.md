## ADDED Requirements

### Requirement: AST 解析结果形成独立版本
系统 SHALL 为每次成功完成的 AST 解析结果创建独立的 AST 版本记录，并将其绑定到对应的 `RepositoryVersion`。AST 版本记录 SHALL 包含仓库、分支、提交、解析配置指纹、投影格式版本、状态、统计信息与时间戳。

#### Scenario: 首次解析某个仓库快照
- **WHEN** 某个 `RepositoryVersion` 尚无可复用的 AST 版本且系统完成一次完整解析
- **THEN** 系统创建新的 AST 版本记录
- **AND** 该记录关联目标 `repository_version_id`
- **AND** 记录中包含 `branch_name`、`commit_sha`、解析配置指纹和状态信息

#### Scenario: 相同快照重复解析命中复用条件
- **WHEN** 同一 `RepositoryVersion` 使用相同解析配置和相同投影格式再次执行 AST 解析
- **THEN** 系统复用已有成功的 AST 版本
- **AND** 不创建语义重复的有效 AST 版本

### Requirement: AST 结果以单行全量 JSON 落库并暴露轻量搜索字段
系统 SHALL 将一次完整 AST 解析的全量结果序列化到单行 `AstVersion` 记录的 `result_json` 字段中，覆盖所有文件的语法树投影、符号、调用边、依赖边、声明级分块与模式提示。同时 SHALL 提供 `symbol_names_json`、`file_list_json` 等轻量结构化字段，用于无需反序列化全量 JSON 的快速搜索。

#### Scenario: 完整解析结果落库
- **WHEN** 系统完成目标 `RepositoryVersion` 下所有源码文件的 AST 解析
- **THEN** 系统将全量 `AstFileResult[]` 序列化写入单条 `AstVersion` 记录的 `result_json`
- **AND** 同步写入 `symbol_names_json`（符号名/类型/文件清单）和 `file_list_json`（文件路径/语言/符号数）
- **AND** 同步写入统计字段：`total_files`、`total_symbols`、`total_call_edges`、`total_chunks`

#### Scenario: AST 版本进入成功态
- **WHEN** 某个 AST 版本被标记为成功
- **THEN** 该版本 `result_json` 包含所有文件的完整解析结果
- **AND** 无需重新解析源码即可从 `result_json` 恢复任意文件的符号、调用边、分块和模式提示

### Requirement: AST 结果支持按分支和提交多版本共存
系统 SHALL 支持同一仓库在不同分支、不同提交以及不同解析配置上的 AST 版本长期共存，并能稳定定位到目标版本。

#### Scenario: 同一分支出现新提交
- **WHEN** 同一仓库同一分支出现新的 `commit_sha`
- **THEN** 系统为新的 `RepositoryVersion` 生成新的 AST 版本
- **AND** 原提交对应的 AST 版本继续保留且可查询

#### Scenario: 不同分支分别解析
- **WHEN** 同一仓库的不同分支分别解析各自提交
- **THEN** 系统分别保存各自的 AST 版本
- **AND** 各分支上的 AST 结果互不覆盖

#### Scenario: 同一提交因解析配置变化产生新 AST 版本
- **WHEN** 同一 `RepositoryVersion` 使用不同解析配置或不同投影格式重新解析
- **THEN** 系统允许创建新的 AST 版本
- **AND** 查询时可以按 `repository_version_id` 加解析配置条件解析到目标版本

### Requirement: 失败的 AST 版本不得被下游引用
系统 SHALL 将 AST 版本的可引用状态与关键明细写入成功状态绑定。只要主记录或关键明细写入失败，该 AST 版本 MUST NOT 被 Wiki 或其他下游能力作为有效依赖引用。

#### Scenario: 持久化过程失败
- **WHEN** AST 主记录或任一关键明细写入失败
- **THEN** 系统不会暴露可被下游引用的成功 AST 版本
- **AND** 失败状态和错误信息可被任务链路感知

#### Scenario: 查询可用 AST 版本
- **WHEN** 下游能力请求某个 `RepositoryVersion` 的可用 AST 版本
- **THEN** 系统只返回成功且可引用的 AST 版本
- **AND** 不返回失败或未完成的版本

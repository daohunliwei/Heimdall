# AST 解析结果版本化落库 Spec

## Why
当前 Tree-sitter AST 解析已经能够产出符号、调用边、依赖关系、声明级分块和设计模式提示，但这些结果尚未形成完整、稳定、可追溯的持久化资产。后续无论是页面上动态展示完整语法树、调用图，还是让 Wiki、问答与其他衍生能力复用同一份代码理解底座，都需要先把 AST 结果按版本完整落库，并能明确某个 Wiki 版本依赖的是哪一份语法树结果。

## What Changes
- 新增 AST 分析版本主记录，绑定 `RepositoryVersion`，记录分支、提交、解析配置、状态与统计信息
- 新增 AST 明细持久化模型，完整保存文件级语法树投影、符号、调用图边、依赖边、声明级分块与设计模式提示
- Wiki 生成链路在目标 `RepositoryVersion` 上先解析或复用 AST 版本，再将所依赖的 AST 版本写入 `WikiVersion`
- 增加按 `RepositoryVersion`、`branch`、`commitSha` 与解析配置查询 AST 版本的读取路径，供后续动态渲染与分析能力复用
- 明确 AST 持久化失败、重跑、复用、同版本重复执行时的状态规则，避免 Wiki 绑定到半成品语法树版本

## Impact
- Affected specs: 代码索引与 AST 理解、版本化 Wiki 生成、版本化知识追溯
- Affected code: `backend/Heimdall.Core/Entities`、`backend/Heimdall.Core/Interfaces/Repositories`、`backend/Heimdall.Repository/Repositories`、`backend/Heimdall.Core/Services/Repository/CodeIndexService.cs`、`backend/Heimdall.Core/Services/Repository/CodeUnderstandingService.cs`、`backend/Heimdall.Core/Services/Tasks/WikiTaskService.cs`、`backend/Heimdall.Core/Services/Tasks/VersionedKnowledgeService.cs`

## ADDED Requirements
### Requirement: AST 分析结果必须形成独立版本
系统 SHALL 为每次成功完成的 AST 解析结果创建独立的 AST 版本记录，并将其绑定到对应的 `RepositoryVersion`

#### Scenario: 首次解析某个仓库快照
- **WHEN** 某个 `RepositoryVersion` 尚无可复用的 AST 版本，且系统完成一次完整 AST 解析
- **THEN** 系统创建新的 AST 版本主记录
- **THEN** 主记录中包含 `repository_id`、`repository_version_id`、`branch_name`、`commit_sha`、解析配置摘要、状态、文件统计与时间戳

#### Scenario: 同一仓库快照重复解析
- **WHEN** 同一 `RepositoryVersion` 使用相同解析配置再次执行 AST 解析
- **THEN** 系统复用现有成功版本或以幂等方式更新该版本
- **THEN** 不会产生语义完全重复的有效 AST 版本

### Requirement: AST 解析明细必须完整落库
系统 SHALL 持久化未来动态渲染和下游分析所需的完整 AST 结果，而不是只保存摘要信息

#### Scenario: 单文件解析结果持久化
- **WHEN** 系统完成某个源码文件的 AST 解析
- **THEN** 系统保存该文件的语法树投影结果
- **THEN** 系统同时保存该文件关联的符号、调用边、依赖边、声明级分块和模式提示

#### Scenario: 单次解析结果提交
- **WHEN** 某个 AST 版本进入成功状态
- **THEN** 该版本的文件数量、符号数量、调用边数量、依赖边数量与分块数量可被查询
- **THEN** 后续无需重新解析源码即可重建语法树展示和调用图展示所需的数据

#### Scenario: 持久化过程失败
- **WHEN** AST 主记录或任一关键明细写入失败
- **THEN** 系统不会留下可被下游引用的成功版本
- **THEN** 失败状态、错误信息与恢复锚点可以被任务链路感知

### Requirement: AST 结果必须支持按分支和提交共存多版本
系统 SHALL 支持同一仓库在不同分支、不同提交上的 AST 版本长期共存，并能稳定定位到目标版本

#### Scenario: 同一分支产生新提交
- **WHEN** 同一仓库同一分支出现新的 `commit_sha`
- **THEN** 系统为新的 `RepositoryVersion` 生成新的 AST 版本
- **THEN** 原有提交对应的 AST 版本仍然保留且可被查询

#### Scenario: 不同分支指向不同提交
- **WHEN** 同一仓库的不同分支分别解析各自的提交
- **THEN** 系统分别保存各自的 AST 版本
- **THEN** 各分支上的语法树结果互不覆盖

#### Scenario: 不同分支偶然指向相同提交
- **WHEN** 同一仓库的两个分支在某一时刻指向相同 `commit_sha`
- **THEN** 系统仍能基于 `RepositoryVersion` 区分两条版本记录
- **THEN** 查询时可以按分支上下文解析到正确的 AST 版本

### Requirement: Wiki 版本必须记录所依赖的 AST 版本
系统 SHALL 在 `WikiVersion` 上记录当前生成所依赖的 AST 版本标识，以保证可追溯性

#### Scenario: Wiki 生成前解析或复用 AST
- **WHEN** 系统开始为目标 `RepositoryVersion` 生成 Wiki
- **THEN** 系统先解析或复用该仓库快照对应的 AST 版本
- **THEN** 之后创建的 `WikiVersion` 必须记录该 AST 版本标识

#### Scenario: 查询某个 Wiki 版本的依赖链路
- **WHEN** 调用版本化知识读取或任务结果摘要能力
- **THEN** 返回结果中包含 `repository_version_id`、`wiki_version_id` 与 `ast_version_id`
- **THEN** 可以追溯该 Wiki 版本依赖的是哪一份语法树结果

#### Scenario: AST 版本缺失或不可用
- **WHEN** 目标 `RepositoryVersion` 无可用 AST 版本且本次 AST 持久化失败
- **THEN** Wiki 版本不得以成功态落库
- **THEN** 系统不得写入指向不存在或失败 AST 版本的关联关系

## MODIFIED Requirements
### Requirement: 版本化 Wiki 生成
系统在创建 `WikiVersion` 时不仅要绑定 `RepositoryVersion`，还必须绑定生成时实际使用的 AST 版本，并在任务结果、工件摘要和后续读取路径中保持一致

#### Scenario: Wiki 主数据落库
- **WHEN** Wiki 主链路进入主数据落库阶段
- **THEN** `WikiVersion` 写入的 `repository_version_id` 与 `ast_version_id` 必须来自同一次有效版本解析
- **THEN** 任务结果摘要、工件摘要与后续读取接口中的版本信息必须一致

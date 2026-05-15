## ADDED Requirements

### Requirement: 结构索引服务
系统 SHALL 在 Wiki 生成任务的"仓库准备"阶段之后、"结构规划"阶段之前，执行结构索引。索引 SHALL 纯本地计算（无 LLM 调用），产出 `CodeIndexEntry` 列表，包含：file_path、module_name、file_type（source/config/test/doc/asset/generated）、size_bytes、dependency_hints。

#### Scenario: 识别项目类型与技术栈
- **WHEN** 结构索引服务分析仓库文件
- **THEN** SHALL 基于标志性文件（package.json → Node.js, *.csproj → .NET, Cargo.toml → Rust 等）输出项目类型与主技术栈标签

#### Scenario: 大仓库自动分区
- **WHEN** 仓库文件数量超过 500
- **THEN** 系统 SHALL 按顶层目录自动划分模块分区，每个分区独立进行后续分析

#### Scenario: 过滤无意义文件
- **WHEN** 文件匹配预定义的排除规则（lock 文件、node_modules、dist/build 目录、二进制文件）
- **THEN** 该文件 SHALL 被标记为 `generated` 或跳过后续 LLM 分析

### Requirement: 分层摘要服务
系统 SHALL 对有意义的源代码文件执行三层摘要：文件级 → 模块级 → 系统级。摘要结果 SHALL 持久化为 `code_analysis_artifact`（类型为 `file_summaries`、`module_summaries`、`system_summary`）。

#### Scenario: 文件级摘要批量生成
- **WHEN** 系统进入分层摘要阶段
- **THEN** SHALL 对每个标记为 `source` 或 `config` 的关键文件（按重要性排序取 top-N），以 batch_size=10 并行调用 LLM 生成 1-3 句摘要

#### Scenario: 模块级摘要聚合
- **WHEN** 所有文件级摘要完成
- **THEN** 系统 SHALL 将同一模块分区内的文件摘要聚合为输入，调用 LLM 生成模块职责描述（3-5 句）

#### Scenario: 系统级摘要生成
- **WHEN** 所有模块级摘要完成
- **THEN** 系统 SHALL 将所有模块摘要聚合，生成系统架构概述（包含核心组件关系、数据流、关键设计决策）

#### Scenario: 增量更新已有摘要
- **WHEN** 仓库新版本中仅部分文件发生变更
- **THEN** 系统 SHALL 仅重新生成变更文件的摘要，并向上冒泡更新受影响的模块级与系统级摘要

### Requirement: 语义驱动 Wiki 结构规划
结构规划阶段 SHALL 消费代码分析结果（系统级摘要 + 模块级摘要 + 文件索引）作为上下文注入规划 prompt，而非仅依赖 file tree 与 README。

#### Scenario: 基于模块数量动态决定页面数
- **WHEN** 代码分析识别出 N 个有意义模块
- **THEN** 结构规划 SHALL 生成 `max(8, min(60, N*2 + entry_point_count))` 页的 Wiki 结构

#### Scenario: 规划引用模块摘要
- **WHEN** 结构规划 prompt 生成
- **THEN** prompt SHALL 包含每个模块的摘要与关键文件列表，使规划结果基于实际代码语义而非猜测

### Requirement: 代码分析结果持久化与缓存
代码分析结果 SHALL 作为 `task_artifact`（type=`code_analysis_artifact`）持久化，与 `RepositoryVersion` 关联。后续同版本的 Wiki 重生成 SHALL 复用已有分析结果而非重新计算。

#### Scenario: 恢复已有分析结果
- **WHEN** 相同 RepositoryVersion 下再次触发 Wiki 生成
- **THEN** 系统 SHALL 检测已有 `code_analysis_artifact`，跳过分析阶段直接进入结构规划

#### Scenario: 分析阶段失败后断点续跑
- **WHEN** 文件摘要阶段在第 3 批次失败
- **THEN** 系统 SHALL 在重试时从第 3 批次继续，不重复处理已完成的批次

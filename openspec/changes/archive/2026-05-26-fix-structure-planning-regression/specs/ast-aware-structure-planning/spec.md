## ADDED Requirements

### Requirement: LLM 结构规划整合 AST 代码理解数据
`LlmJson` 策略的结构规划提示词 SHALL 注入 Tree-sitter AST 产出的 deep code understanding 数据，包括调用图摘要、模块依赖拓扑、检测到的设计模式、架构层次分析。系统 SHALL 通过 `BuildWikiStructurePromptV7` 已有的 `codeUnderstanding` 参数传递这些数据。

#### Scenario: 调用图数据注入提示词
- **WHEN** 系统构建结构规划提示词且 `CodeUnderstandingResult` 可用
- **THEN** 提示词包含调用图摘要（节点数、边数、最大深度）
- **AND** 包含模块依赖拓扑（模块名、文件数、依赖边）
- **AND** LLM 基于调用图数据做出模块分组决策

#### Scenario: 设计模式数据注入提示词
- **WHEN** AST 分析检测到设计模式（如 Singleton、Factory、Observer）
- **THEN** 提示词列出检测到的模式名称、置信度和参与类
- **AND** LLM 在设计文档结构时优先为设计模式相关代码创建专题页面

#### Scenario: CodeUnderstanding 不可用时不阻塞
- **WHEN** CodeUnderstandingResult 为 null（代码理解阶段失败或跳过）
- **THEN** 提示词不包含 deep code understanding 段
- **AND** 结构规划正常进行（降级为仅基于文件树和 README）

### Requirement: LLM 结构规划整合代码索引统计摘要
`LlmJson` 策略的提示词 SHALL 注入 `CodeIndexResult` 的模块文件分布摘要，包括每个模块的文件数量、推荐页面数（`recommendedPageCount`）、入口文件列表。

#### Scenario: 模块分布摘要注入
- **WHEN** 系统构建结构规划提示词
- **THEN** 提示词包含每个模块的文件数量（如 "LibGit2Sharp: 485 files, LibGit2Sharp.Tests: 876 files"）
- **AND** 提示词包含推荐页面数作为分组参考（如 "建议生成约 80 个页面"）
- **AND** LLM 据此合理分配每个模块的页面配额

#### Scenario: 入口文件列表注入
- **WHEN** CodeIndexResult 包含入口文件列表
- **THEN** 提示词列出入口文件路径
- **AND** LLM 将入口文件相关的类/方法优先放入 Overview Section

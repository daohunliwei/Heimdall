## Purpose

Wiki 结构规划——涵盖三种可配置策略（LlmJson / Deterministic / LlmEnhanced）、AST 代码理解数据与代码索引统计注入 LLM 提示词，以及策略对页面生成阶段的透明性。
## Requirements
### Requirement: 三种可配置的结构规划策略
系统 SHALL 提供三种结构规划策略：`LlmJson`（LLM 生成 JSON，默认）、`Deterministic`（确定性算法，降级方案）、`LlmEnhanced`（算法骨架 + LLM 润色），通过环境变量 `HEIMDALL_STRUCTURE_PLANNING_STRATEGY` 或 `appsettings.json` 的 `StructurePlanning.Strategy` 配置切换。

#### Scenario: 未配置时默认使用 LlmJson
- **WHEN** 未设置 `StructurePlanning.Strategy` 配置项且未设置环境变量
- **THEN** 系统使用 `LlmJson` 策略进行结构规划

#### Scenario: 策略配置切换
- **WHEN** 管理员在 `appsettings.json` 中设置 `StructurePlanning.Strategy` 为 `Deterministic`
- **THEN** 所有 Wiki 生成任务使用确定性算法，运行时可通过环境变量覆盖

#### Scenario: 变更策略不重启生效
- **WHEN** 运行时修改 appsettings.json 的策略值
- **THEN** 下一个 Wiki 生成任务使用新策略（无需重启服务）

### Requirement: Deterministic 策略——代码索引数据聚合映射
系统 SHALL 基于 CodeIndexResult 通过目录级聚合算法生成 WikiStructureDto，不调用 LLM。聚合规则：同一目录文件数 ≤ 3 时合并为一页；> 3 时按重要性分数排序，top-3 独立成页、其余合并；测试目录合并为单页；配置文件跳过。最终页数 SHALL 不超过 `recommendedPageCount × 1.5`。

#### Scenario: 模块映射为 Section
- **WHEN** CodeIndexResult 包含多个模块
- **THEN** 系统为每个模块创建一个 Section，Depth 基于依赖拓扑排序

#### Scenario: 目录级聚合为 Page
- **WHEN** 模块包含 50 个源文件分布在 10 个目录中
- **THEN** 系统按目录分组聚合，产出页面数远小于文件数

#### Scenario: 测试目录合并
- **WHEN** 代码索引包含大量测试文件
- **THEN** 每个测试子目录合并为单页

#### Scenario: 配置文件跳过
- **WHEN** 文件类型为 config（*.json, *.xml, *.config, *.csproj）
- **THEN** 不为其创建独立 Page

#### Scenario: 入口文件生成 Overview Section
- **WHEN** CodeIndexResult 包含入口文件
- **THEN** 系统创建 Overview Section 包含 Welcome Page

### Requirement: LlmJson 策略——LLM 生成 JSON（默认）
系统 SHALL 使用 LLM → JSON → WikiStructureDto 的完整路径作为结构规划默认策略。提示词 SHALL 注入 Tree-sitter AST 产出的 deep code understanding 数据和代码索引统计摘要。保留全部 JSON 解析、重试、回退逻辑。

#### Scenario: LlmJson 为默认行为
- **WHEN** 未配置策略（默认）
- **THEN** 系统使用 LlmJson 策略，提示词额外包含 AST 数据作为分组参考

#### Scenario: 结构规划页面数合理
- **WHEN** 对大型仓库使用 LlmJson 策略
- **THEN** 结构规划产出页面数在合理范围内，非逐文件映射的结果

### Requirement: AST 代码理解数据注入 LlmJson 提示词
`LlmJson` 策略的提示词 SHALL 注入 Tree-sitter AST 产出的 deep code understanding 数据，包括调用图摘要、模块依赖拓扑、检测到的设计模式、架构层次分析。同时注入 `CodeIndexResult` 的模块文件分布摘要（每个模块的文件数量、推荐页面数、入口文件列表）。

#### Scenario: 调用图数据注入提示词
- **WHEN** 系统构建结构规划提示词且 `CodeUnderstandingResult` 可用
- **THEN** 提示词包含调用图摘要（节点数、边数、最大深度）和模块依赖拓扑
- **AND** LLM 基于调用图数据做出模块分组决策

#### Scenario: 设计模式数据注入提示词
- **WHEN** AST 分析检测到设计模式（如 Singleton、Factory、Observer）
- **THEN** 提示词列出检测到的模式名称、置信度和参与类
- **AND** LLM 优先为设计模式相关代码创建专题页面

#### Scenario: 模块分布摘要注入
- **WHEN** 系统构建结构规划提示词
- **THEN** 提示词包含每个模块的文件数量和推荐页面数作为分组参考

#### Scenario: 入口文件列表注入
- **WHEN** CodeIndexResult 包含入口文件列表
- **THEN** 提示词列出入口文件路径，LLM 将入口文件相关内容优先放入 Overview Section

#### Scenario: CodeUnderstanding 不可用时不阻塞
- **WHEN** CodeUnderstandingResult 为 null（代码理解阶段失败或跳过）
- **THEN** 提示词不包含 deep code understanding 段，结构规划正常进行（降级为仅基于文件树和 README）

### Requirement: LlmEnhanced 策略——算法骨架 + LLM 润色
系统 SHALL 先用确定性聚合算法生成 Section/Page 骨架（id、depth、pages 列表），再逐 Section 调用 LLM 生成人性化的 title/description/navTitle。骨架页面数 SHALL 受目录级聚合规则约束。

#### Scenario: 算法生成骨架
- **WHEN** 策略为 `LlmEnhanced`
- **THEN** 系统先运行聚合算法生成合理的 Section/Page 结构，title/description 使用临时占位值

#### Scenario: LLM 润色文案
- **WHEN** 骨架生成完成
- **THEN** 系统对每个 Section 调用 LLM（~500 tokens input），仅要求返回 title/description
- **AND** LLM 调用失败时使用占位文案，不阻塞流程

### Requirement: 策略不影响页面生成
三种策略的最终产物均为 `WikiStructureDto`，页面生成阶段 SHALL 无感知。

#### Scenario: 页面生成不关心策略来源
- **WHEN** 结构规划完成（无论使用何种策略）
- **THEN** 下游页面生成阶段使用相同的 `WikiStructureDto` 数据，不需要任何代码改动

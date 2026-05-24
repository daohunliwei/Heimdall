## ADDED Requirements

### Requirement: 三种可配置的结构规划策略
系统 SHALL 提供三种结构规划策略：`Deterministic`（确定性算法，默认）、`LlmJson`（LLM 生成 JSON，当前行为）、`LlmEnhanced`（算法骨架 + LLM 润色），通过 `appsettings.json` 的 `StructurePlanning.Strategy` 配置切换。

#### Scenario: 策略配置切换
- **WHEN** 管理员在 `appsettings.json` 中设置 `StructurePlanning.Strategy` 为 `Deterministic`
- **THEN** 所有 Wiki 生成任务使用确定性算法进行结构规划
- **AND** 运行时可通过环境变量 `HEIMDALL_STRUCTURE_PLANNING_STRATEGY` 覆盖

#### Scenario: 变更策略不重启生效
- **WHEN** 运行时修改 appsettings.json 的策略值
- **THEN** 下一个 Wiki 生成任务使用新策略（无需重启服务）

### Requirement: Deterministic 策略——代码索引数据直接映射
系统 SHALL 基于 CodeIndexResult 的模块列表、调用图、依赖拓扑、入口文件，通过确定性算法直接生成 WikiStructureDto，不调用 LLM。

#### Scenario: 模块映射为 Section
- **WHEN** CodeIndexResult 包含 11 个模块（ModuleNames），每个模块有文件数量（ModuleFileCounts）
- **THEN** 系统为每个模块创建一个 Section，Id 基于模块名，Title 使用模块名
- **AND** Depth 基于依赖拓扑中该模块被依赖次数排序（核心模块优先）

#### Scenario: 源文件映射为 Page
- **WHEN** CodeIndexResult 包含 73 个条目（Entries）
- **THEN** 每个 source 类型的条目映射为一个 Page
- **AND** Page 的 filePaths 关联该条目对应的文件路径
- **AND** Page depth 由文件路径目录层级决定

#### Scenario: 入口文件生成 Overview Section
- **WHEN** CodeIndexResult 包含 10 个入口文件（EntryPointFiles）
- **THEN** 系统创建一个 Overview Section 包含 Welcome Page
- **AND** Welcome Page 关联入口文件的 filePaths

### Requirement: LlmJson 策略——LLM 生成 JSON（当前行为保留）
系统 SHALL 保留当前 LLM → JSON → WikiStructureDto 的完整路径作为 `LlmJson` 策略。

#### Scenario: LlmJson 等同于当前行为
- **WHEN** 策略配置为 `LlmJson`
- **THEN** 系统行为与 V9 完全一致，包含 JSON 解析、重试、回退逻辑

### Requirement: LlmEnhanced 策略——算法骨架 + LLM 润色
系统 SHALL 先用确定性算法生成 Section/Page 骨架（id、depth、pages 列表），再逐 Section 调用 LLM 生成人性化的 title/description/navTitle。

#### Scenario: 算法生成骨架
- **WHEN** 策略为 `LlmEnhanced`
- **THEN** 系统先运行确定性算法生成完整的 Section/Page 结构（id、depth、filePaths 已填充）
- **AND** title/description 使用临时占位值

#### Scenario: LLM 润色文案
- **WHEN** 骨架生成完成
- **THEN** 系统对每个 Section 调用 LLM（~500 tokens input），仅要求返回 `{ "title": "...", "description": "..." }`
- **AND** LLM 调用失败时使用占位文案，不阻塞流程

### Requirement: 策略不影响页面生成
三种策略的最终产物均为 `WikiStructureDto`，页面生成阶段 SHALL 无感知。

#### Scenario: 页面生成不关心策略来源
- **WHEN** 结构规划完成（无论使用何种策略）
- **THEN** 下游页面生成阶段使用相同的 `WikiStructureDto` 数据
- **AND** 不需要任何代码改动

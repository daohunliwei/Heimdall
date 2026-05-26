## MODIFIED Requirements

### Requirement: 三种可配置的结构规划策略
系统 SHALL 提供三种结构规划策略：`LlmJson`（LLM 生成 JSON，默认）、`Deterministic`（确定性算法，降级方案）、`LlmEnhanced`（算法骨架 + LLM 润色），通过环境变量 `HEIMDALL_STRUCTURE_PLANNING_STRATEGY` 或 `appsettings.json` 的 `StructurePlanning.Strategy` 配置切换。默认策略为 `LlmJson`。

#### Scenario: 未配置时默认使用 LlmJson
- **WHEN** 未设置 `StructurePlanning.Strategy` 配置项且未设置环境变量 `HEIMDALL_STRUCTURE_PLANNING_STRATEGY`
- **THEN** 系统使用 `LlmJson` 策略进行结构规划
- **AND** 行为与 V9 完全一致

#### Scenario: 运行时通过环境变量覆盖
- **WHEN** 管理员设置环境变量 `HEIMDALL_STRUCTURE_PLANNING_STRATEGY=Deterministic`
- **THEN** 下一个 Wiki 生成任务使用确定性算法（无需重启服务）

### Requirement: Deterministic 策略——代码索引数据聚合映射
系统 SHALL 基于 CodeIndexResult 通过目录级聚合算法生成 WikiStructureDto，不调用 LLM。聚合规则：同一目录文件数 ≤ 3 时合并为一页；> 3 时按重要性分数排序，top-3 独立成页、其余合并；测试目录（*Tests*/*test*/*Test*）合并为单页；配置文件（*.json/*.xml/*.config/*.csproj）跳过。最终页数 SHALL 不超过 `recommendedPageCount × 1.5`。

#### Scenario: 模块映射为 Section（保持）
- **WHEN** CodeIndexResult 包含多个模块（ModuleNames）
- **THEN** 系统为每个模块创建一个 Section，Id 基于模块名，Title 使用模块名
- **AND** Depth 基于依赖拓扑中该模块被依赖次数排序（核心模块优先）

#### Scenario: 目录级聚合为 Page
- **WHEN** 模块包含 50 个源文件分布在 10 个目录中
- **THEN** 系统按目录分组，同目录 ≤ 3 文件合并为一页
- **AND** 同目录 > 3 文件按重要性 top-3 独立、其余合并
- **AND** 产出页面数在 10-30 页范围（而非 50 页）

#### Scenario: 测试目录合并
- **WHEN** 代码索引包含 LibGit2Sharp.Tests 模块下有 876 个测试文件
- **THEN** 该模块的所有文件合并为不超过 20 页
- **AND** 每个测试子目录合并为单页（如 "Remote 测试"、"Clone 测试"）

#### Scenario: 配置文件跳过
- **WHEN** 文件类型为 config（*.json, *.xml, *.config, *.csproj）
- **THEN** 不为其创建独立 Page
- **AND** 不影响其他文件的 Page 创建

#### Scenario: 入口文件生成 Overview Section（保持）
- **WHEN** CodeIndexResult 包含入口文件（EntryPointFiles）
- **THEN** 系统创建一个 Overview Section 包含 Welcome Page
- **AND** Welcome Page 关联入口文件的 filePaths

### Requirement: LlmJson 策略——LLM 生成 JSON（默认）
系统 SHALL 使用 LLM → JSON → WikiStructureDto 的完整路径作为结构规划默认策略。提示词 SHALL 注入 Tree-sitter AST 产出的 deep code understanding 数据和代码索引统计摘要。保留全部 JSON 解析、重试、回退逻辑。

#### Scenario: LlmJson 为默认行为
- **WHEN** 未配置策略（默认）
- **THEN** 系统使用 LlmJson 策略，行为与 V9 一致
- **AND** 提示词额外包含 AST 数据作为分组参考

#### Scenario: 结构规划页面数合理
- **WHEN** 对 libgit2sharp（1520 文件）使用 LlmJson 策略
- **THEN** 结构规划产出页面数在 30-100 页范围内
- **AND** 非 1453 页（Deterministic 逐文件映射的结果）

### Requirement: LlmEnhanced 策略——算法骨架 + LLM 润色
系统 SHALL 先用修复后的确定性聚合算法生成 Section/Page 骨架（id、depth、pages 列表），再逐 Section 调用 LLM 生成人性化的 title/description/navTitle。骨架页面数 SHALL 受目录级聚合规则约束。

#### Scenario: 算法生成骨架
- **WHEN** 策略为 `LlmEnhanced`
- **THEN** 系统先运行聚合算法生成合理的 Section/Page 结构（page 数贴近 recommendedPageCount）
- **AND** title/description 使用临时占位值

#### Scenario: LLM 润色文案（保持）
- **WHEN** 骨架生成完成
- **THEN** 系统对每个 Section 调用 LLM（~500 tokens input），仅要求返回 `{ "title": "...", "description": "..." }`
- **AND** LLM 调用失败时使用占位文案，不阻塞流程

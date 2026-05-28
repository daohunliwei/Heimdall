## Purpose

定义 Wiki 生成的 8 阶段管线主流程，包括结构规划策略、页面生成、质量审查与版本管理。
## Requirements
### Requirement: Wiki 生成管线流程
系统 SHALL 按当前实现描述 Wiki 生成的 8 阶段主流程：仓库准备 → 代码索引 → 代码理解 → 结构规划 → 页面生成 → 质量审查 → 渲染后处理 → 持久化。与向量嵌入、独立向量阶段相关的描述不属于当前已落地能力。

#### Scenario: 标准仓库 Wiki 生成
- **WHEN** 用户触发 Wiki 刷新
- **THEN** 系统按 8 阶段主流程执行
- **AND** Stage 2 基于 Tree-sitter 和索引构建代码检索底座
- **AND** Stage 5 基于当前可用的检索证据生成页面
- **AND** 不要求执行独立的向量嵌入阶段才能完成主流程

### Requirement: 检索增强页面生成
页面生成阶段 SHALL 使用当前已落地的 `BM25` 检索与版本化工件上下文获取真实代码片段，注入提示词后由 LLM 生成页面。输出 SHALL 包含真实代码引用（类名、方法签名、关键实现片段），不得包含虚构的示例代码。

#### Scenario: 页面生成含真实代码
- **WHEN** 生成用户认证 Wiki 页面
- **THEN** 页面内容包含从源代码中检索到的真实类名和方法签名

#### Scenario: 页面生成不得虚构 API
- **WHEN** LLM 生成页面内容
- **THEN** 提示词中明确要求"仅使用提供的源代码片段，如代码片段不足以解释某个概念，请注明'未在代码中找到对应实现'"

### Requirement: 差异化提示词
Stage 5 页面生成 SHALL 根据页面的 ContentDepthLevel 使用差异化提示词：`article` 级侧重代码深挖、逐方法分析、时序图；`overview` 级侧重架构全景、模块关系。

#### Scenario: article 级页面使用深度提示词
- **WHEN** 页面 ContentDepthLevel=article
- **THEN** 使用 article 级提示词（侧重代码深挖、逐方法分析、时序图）

#### Scenario: overview 级页面使用全景提示词
- **WHEN** 页面 ContentDepthLevel=overview
- **THEN** 使用 overview 级提示词（侧重架构全景、模块关系）

### Requirement: 条件化页面数量
系统 SHALL 根据代码分析阶段的输出（模块数量、入口点数量、设计模式数量、调用图深度、文件总数）动态决定目标页面数量，范围为 15-80 页。

#### Scenario: 小项目生成精简 Wiki
- **WHEN** 项目模块数 ≤ 3 且文件数 < 50
- **THEN** 系统 SHALL 规划 15-20 页 Wiki，最大嵌套深度 2 层

#### Scenario: 中型项目生成中等 Wiki
- **WHEN** 项目模块数 4-8 且文件数 50-200
- **THEN** 系统 SHALL 规划 25-45 页 Wiki，最大嵌套深度 3 层

#### Scenario: 大型项目生成深度 Wiki
- **WHEN** 项目模块数 ≥ 10 且文件数 > 200
- **THEN** 系统 SHALL 规划 45-80 页 Wiki，支持 4-5 层目录嵌套

### Requirement: 自动质量评估（算法评分）
全局收敛阶段 SHALL 通过 `WikiGlobalConvergenceService.CalculatePageQualityScore()` 对每个生成页面执行算法质量评分（起始 50 分，根据内容长度加分 ≤15、代码块 +10、表格 +5、结构化标题 +5、关联页面 ≤8、源文件 ≤7）。弱页面（评分 < 60）标记为 `needs_regeneration` 触发一轮重生成。

#### Scenario: 识别弱页面
- **WHEN** 某页面算法评分 < 60
- **THEN** 系统 SHALL 标记该页面为 `needs_regeneration` 并记录扣分原因（内容过短、缺少代码块/表格等）

#### Scenario: 内容深度不符检测
- **WHEN** Article 类型页面缺少代码引用、内容过于概括
- **THEN** 质量评分偏低（代码块和内容长度维度扣分），标注"内容深度不符合 article 页面要求"

#### Scenario: 质量审查执行
- **WHEN** Stage 7 执行质量审查
- **THEN** 系统使用 `WikiGlobalConvergenceService.CalculatePageQualityScore()` 进行算法评分

### Requirement: 弱页面自动重生成
系统 SHALL 在质量评估后对标记为 `needs_regeneration` 的页面自动触发一轮重生成，重生成 prompt 包含原始内容、质量评估反馈与改进指导，并增加 30% 检索 token 预算。

#### Scenario: 重生成后质量提升
- **WHEN** 弱页面触发重生成
- **THEN** 重生成的 prompt SHALL 包含"原始内容摘要"、"质量评估反馈"和"额外 30% 代码片段"

#### Scenario: 重生成上限控制
- **WHEN** 弱页面重生成后 quality_score 仍 < 60
- **THEN** 系统 SHALL 保留重生成结果、记录警告日志，不再触发进一步重生成（最多 1 轮）

### Requirement: Wiki 版本号递增保证
系统 SHALL 确保每次 Wiki 刷新生成严格递增的新版本号。版本号 SHALL 从已有最大版本号 + 1 计算，不复写任何已有版本。

#### Scenario: 新版本递增
- **WHEN** 仓库已有 v1-v4 四个 Wiki 版本，用户触发刷新
- **THEN** 系统创建 v5，不复写 v1-v4 中任何版本

#### Scenario: 首次生成
- **WHEN** 仓库无任何 Wiki 版本
- **THEN** 系统创建 v1

### Requirement: Stage 5 Tool Call 增强
系统 SHALL 在页面生成阶段的 LLM 调用中，根据配置开关 `ToolCall.Stage5.Enabled` 决定是否在 `ChatOptions.Tools` 中注入 `ReadCodeFile` 和 `SearchSymbols` 的 `AIFunction` 列表。`FunctionInvokingChatClient` SHALL 自动处理工具调用往返。

#### Scenario: 主动检索缺失的代码上下文
- **WHEN** `ToolCall.Stage5.Enabled` 为 `true`，LLM 正在生成《数据访问层设计》页面
- **THEN** `ChatOptions.Tools` 包含 `ReadCodeFile` 和 `SearchSymbols`
- **AND** LLM 发现预置上下文中缺少 `DbSession` 类定义
- **AND** LLM 调用 `SearchSymbols("DbSession")` 找到文件路径
- **AND** `FunctionInvokingChatClient` 自动执行搜索并将结果返回给 LLM
- **AND** LLM 基于搜索结果继续撰写页面

#### Scenario: Tool Call 未启用时的降级
- **WHEN** `ToolCall.Stage5.Enabled` 为 `false`
- **THEN** `ChatOptions.Tools` 为 `null`
- **AND** `FunctionInvokingChatClient` 直接透传请求/响应
- **AND** 行为与当前版本完全一致

### Requirement: WikiTaskService Orchestrator 分支（已规划）
系统 SHALL 在 WikiTaskService.ExecuteAsync 中判断是否启用 Orchestrator 路径。当前检测逻辑已就绪（`ShouldUseSubAgents` 输出日志标记），但完整 Orchestrator 分发（`AssignModules` + 子代理并行执行）尚未激活——所有任务仍走传统 8 阶段串行管线。

#### Scenario: Orchestrator 路径分叉（已规划）
- **WHEN** `ShouldUseSubAgents` 返回 `true`（当前仅输出日志标记）
- **THEN** 传统管线路径继续执行，不创建子代理

#### Scenario: 传统管线路径（当前实际路径）
- **WHEN** 任意 Wiki 任务执行
- **THEN** 系统按 Stage 1→2→3→4→5→6→7→8 顺序执行，不创建子代理

### Requirement: 结构规划阶段
Wiki 生成管线 SHALL 在结构规划阶段根据 `StructurePlanning.Strategy` 配置选择策略：`LlmJson`（LLM 生成 JSON，默认）使用 LLM 生成 JSON 后解析为 WikiStructureDto；`Deterministic` 使用代码索引数据通过目录级聚合算法直接生成 WikiStructureDto；`LlmEnhanced` 使用算法骨架 + LLM 润色。最终产物均为 `WikiStructureDto`，页面生成阶段无感知。结构规划完成后，若满足子代理触发条件，系统 SHALL 可选择使用 Orchestrator 路径进行后续阶段。

#### Scenario: LlmJson 策略执行（默认行为）
- **WHEN** 未配置策略（默认）或策略为 `LlmJson`
- **THEN** 系统使用 LLM 生成 JSON，解析为 WikiStructureDto
- **AND** 保留全部 JSON 解析、重试和回退逻辑
- **AND** 提示词包含 Tree-sitter AST 数据和代码索引统计摘要

#### Scenario: Deterministic 策略执行
- **WHEN** 策略为 `Deterministic` 且结构规划阶段开始
- **THEN** 系统调用 `DeterministicStructurePlanner.BuildStructure(CodeIndexResult)` 直接返回 WikiStructureDto
- **AND** 不发起任何 LLM 调用
- **AND** 耗时 < 100ms

#### Scenario: LlmEnhanced 策略执行
- **WHEN** 策略为 `LlmEnhanced`
- **THEN** 系统先运行聚合算法生成 Section/Page 骨架（id、depth、pages 列表）
- **AND** 再逐 Section 调用 LLM 生成人性化的 title/description/navTitle
- **AND** LLM 调用失败时使用占位文案，不阻塞流程

#### Scenario: 结构规划完成后触发 Orchestrator 评估
- **WHEN** 结构规划阶段完成并产出 `WikiStructureDto`
- **THEN** 系统调用 `AgentOrchestratorService.ShouldUseSubAgents(sourceFileCount)` 评估是否启用子代理
- **AND** 若启用，后续阶段使用 Orchestrator 路径并行执行
- **AND** 若不启用，后续阶段使用传统 8 阶段管线顺序执行

#### Scenario: 策略变更不影响已运行任务
- **WHEN** 某任务已开始执行结构规划
- **THEN** 该任务使用开始时的策略配置完成，中途变更不影响

### Requirement: 页面生成使用当前已落地的证据检索能力
页面生成阶段 SHALL 按当前实现使用 `BM25` 检索、版本化页面与工件上下文注入提示词，输出基于真实代码与版本证据的内容。未实现的向量召回不得写成当前流程的默认步骤。

#### Scenario: 页面生成注入 BM25 与版本化证据
- **WHEN** 系统生成某个 Wiki 页面
- **THEN** 提示词证据来自当前可用的 `BM25` 检索结果、版本化页面内容和任务工件摘要
- **AND** 如果当前代码未提供向量召回，则文档与注释不得声称已执行 `pgvector` 搜索

### Requirement: Stage 3 与 Stage 5 Tool Call 描述保持现状
系统 SHALL 继续允许 Stage 3 / Stage 5 通过 `ChatOptions.Tools` 增强代码理解与页面生成，但相关说明必须明确其前提是配置开启，而非默认强制执行。

#### Scenario: Tool Call 关闭时主流程仍可运行
- **WHEN** `ToolCallConfigurationService` 返回 Stage 3 或 Stage 5 关闭
- **THEN** 系统仍按主流程继续执行
- **AND** 仅跳过对应阶段的工具增强

### Requirement: 多层嵌套 Wiki 目录结构
系统 SHALL 支持生成 3-5 层嵌套的 Wiki 目录结构。每个节点 SHALL 具有有效的 parentId 指向其父节点，根节点 parentId 为 null。解析器 SHALL 在后处理阶段验证并自动修正无效引用。

#### Scenario: 大型项目 4 层嵌套
- **WHEN** 仓库包含 15+ 模块，500+ 文件
- **THEN** 结构规划输出 4 层嵌套结构，每个页面条目包含合法的 parentId

#### Scenario: 小型项目 2 层嵌套
- **WHEN** 仓库包含 ≤3 模块，< 50 文件
- **THEN** 结构规划输出 2 层嵌套结构

#### Scenario: parentId 无效引用自动修正
- **WHEN** LLM 输出 JSON 中某页面的 parentId 指向不存在的页面
- **THEN** 解析器将该页面的 parentId 设为 null（提升为根节点）并记录 Warning

### Requirement: 拓扑序渐进式页面生成
系统 SHALL 按树形拓扑序生成 Wiki 页面：先生成顶层页面，再逐层生成子页面。子页面生成时 SHALL 继承父页面摘要作为上下文。BFS 遍历从根节点开始，同层页面可并行，子页面必须等父页面完成后才能开始。

#### Scenario: 父页面先于子页面生成
- **WHEN** 页面 A 是页面 B 的父页面
- **THEN** 系统确保 A 在 B 之前生成完成，B 的 prompt 包含 A 的标题和摘要

#### Scenario: 上下文继承链
- **WHEN** 页面 E（L4）的父页面为 D（L3），D 的父页面为 B（L2）
- **THEN** E 的生成 prompt 包含 B 的摘要（祖父级）和 D 的摘要（父级）

### Requirement: 前端树形组件层级渲染
前端 Wiki 目录树组件 SHALL 根据页面 parentId 构建树形数据结构，递归渲染嵌套节点。根节点渲染为顶层条目，子节点缩进展示，支持展开/折叠交互。

#### Scenario: 树形组件渲染多层结构
- **WHEN** 后端页面列表包含 3 层嵌套关系
- **THEN** 前端构建 3 层嵌套树，子节点缩进 16px/层，使用展开/折叠箭头

#### Scenario: 旧数据兼容平铺渲染
- **WHEN** 后端页面列表中所有页面 parentId 为 null（旧版本数据）
- **THEN** 前端按平铺列表渲染，行为与原来一致

#### Scenario: 当前页面自动展开路径
- **WHEN** 用户浏览某 Wiki 页面且该页面在树的第 3 层
- **THEN** 树形组件自动展开该页面的所有祖先节点，并高亮当前页面条目

### Requirement: Wiki 页面内容存储到 Workspace 文件
Wiki 页面 SHALL 将 `ContentMarkdown` 写入 Workspace 的 `wiki/{wiki_version_id[:8]}/pages/{page_order:D4}_{slug}.md` 文件，DB 中 `WikiPage.content_file_path` 记录路径，不再将完整 Markdown 内容存入 DB。

#### Scenario: Wiki 页面生成后写文件
- **WHEN** 系统完成一个 Wiki 页面的 LLM 生成
- **THEN** 页面 Markdown 内容写入 workspace 文件
- **AND** `WikiPage.content_file_path` 记录文件路径
- **AND** `WikiPage.ContentMarkdown` DB 列保留为空或存摘要

#### Scenario: 读取 Wiki 页面内容
- **WHEN** API 请求读取某个 Wiki 页面
- **THEN** 系统根据 `content_file_path` 从 workspace 读取文件
- **AND** 文件缺失时触发页面重新生成

### Requirement: Wiki 版本结构存储到 Workspace 文件
`WikiVersion.StructureJson` SHALL 写入 Workspace 的 `wiki/{wiki_version_id[:8]}/structure.json` 文件，DB 中 `structure_file_path` 记录路径。

#### Scenario: Wiki 结构规划完成后写文件
- **WHEN** 系统完成结构规划阶段
- **THEN** 结构 JSON 写入 workspace 文件
- **AND** `WikiVersion.structure_file_path` 记录路径

### Requirement: 仓库克隆路径迁移到 Workspace
仓库克隆目标路径 SHALL 从 `%TEMP%/heimdall_repos/` 改为 `{workspace}/repos/{owner}_{repo}/`，与 Workspace 统一管理。

#### Scenario: 克隆新仓库
- **WHEN** 系统需要克隆 `SilverHawk/wikispider`
- **THEN** 目标路径为 `{workspace}/repos/SilverHawk_wikispider/`
- **AND** 克隆逻辑不变（`git clone --depth=1`）

#### Scenario: 重用已克隆仓库
- **WHEN** 目标路径已存在非空目录
- **THEN** 系统跳过克隆直接复用
- **AND** 行为与当前一致

### Requirement: Wiki 生成前必须解析或复用 AST 版本
Wiki 生成管线 SHALL 在持久化 `WikiVersion` 之前，先为目标 `RepositoryVersion` 解析或复用一个可引用的 AST 版本，且 AST 版本数据 SHALL 存储在 Workspace `ast/` 目录下。若不存在可引用的 AST 版本，则 Wiki 主链路 MUST NOT 进入成功落库阶段。

#### Scenario: 命中可复用 AST 版本
- **WHEN** 目标 `RepositoryVersion` 已存在满足当前解析配置的成功 AST 版本
- **THEN** Wiki 生成管线复用该 AST 版本
- **AND** AST 数据存储在 `{workspace}/ast/{ast_version_id[:8]}/` 目录下
- **AND** 不重复创建语义等价的 AST 结果

#### Scenario: 需要先生成 AST 版本
- **WHEN** 目标 `RepositoryVersion` 不存在可复用的 AST 版本
- **THEN** Wiki 生成管线先完成 AST 解析并将数据写入 workspace
- **AND** 只有 AST 版本可引用后才继续 `WikiVersion` 落库

#### Scenario: AST 持久化失败阻断 Wiki 成功态
- **WHEN** 本次 Wiki 生成所需的 AST 持久化失败
- **THEN** `WikiVersion` 不得进入成功态
- **AND** 系统不得写入指向不存在或失败 AST 版本的 Wiki 关联


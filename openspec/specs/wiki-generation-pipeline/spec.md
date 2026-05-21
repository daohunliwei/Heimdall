## ADDED Requirements

### Requirement: 新 Wiki 生成管线流程
系统 SHALL 按以下阶段执行 Wiki 生成：仓库准备 → 代码结构索引（增强）→ 深度代码理解（含 LLM 辅助）→ 层级结构规划（3-5 层嵌套）→ 拓扑序渐进式页面生成（父先子后）→ 交叉引用编织 → 质量审查（增强）→ 渲染后处理 → 持久化 → 向量嵌入。

#### Scenario: 标准仓库 Wiki 生成
- **WHEN** 用户触发 Wiki 刷新
- **THEN** 系统按 10 阶段顺序执行：Stage 2 执行增强的本地索引（含调用图），Stage 3 执行 1-2 次 LLM 辅助架构理解，Stage 4 输出多层嵌套结构，Stage 5 按拓扑序生成页面，Stage 6 执行交叉引用编织

#### Scenario: 管线中断恢复
- **WHEN** 管线在深度代码理解阶段后中断
- **THEN** 系统恢复时从 CodeUnderstandingResult 工件恢复，跳过已完成的代码索引和深度理解阶段

### Requirement: 结构规划输入变更
结构规划阶段 SHALL 使用目录树、模块列表、入口点文件列表和关键技术栈信息作为输入，不再使用代码摘要作为输入。

#### Scenario: 结构规划使用目录树
- **WHEN** 系统执行结构规划
- **THEN** LLM 收到仓库目录树（深度限制 3 层）、入口文件内容和项目构建文件内容，据以设计 Wiki 页面结构

#### Scenario: 结构规划输出页面-文件映射
- **WHEN** 结构规划完成
- **THEN** 每个规划的页面包含明确的关键文件路径列表和搜索关键词，供后续检索阶段使用

### Requirement: 检索增强页面生成
页面生成阶段 SHALL 使用混合检索（BM25 + 向量搜索）从代码索引中获取真实代码片段，注入提示词后由 LLM 生成页面。输出 SHALL 包含真实代码引用（类名、方法签名、关键实现片段），不得包含虚构的示例代码。

#### Scenario: 页面生成含真实代码
- **WHEN** 生成用户认证 Wiki 页面
- **THEN** 页面内容包含从源代码中检索到的真实类名和方法签名，以及核心实现片段

#### Scenario: 页面生成不得虚构 API
- **WHEN** LLM 生成页面内容
- **THEN** 提示词中明确要求"仅使用提供的源代码片段，如代码片段不足以解释某个概念，请注明'未在代码中找到对应实现'"

#### Scenario: 批量页面生成
- **WHEN** 结构规划确定 10 个页面
- **THEN** 系统以每批 5 页的方式并行生成，每页生成前独立执行代码检索

### Requirement: 条件化页面数量
系统 SHALL 根据深度代码理解阶段的输出（模块数量、入口点数量、设计模式数量、调用图深度、文件总数）动态决定目标页面数量，范围为 15-80 页。

#### Scenario: 小项目生成精简 Wiki
- **WHEN** 项目模块数 ≤ 3 且文件数 < 50
- **THEN** 系统 SHALL 规划 15-20 页 Wiki，最大嵌套深度 2 层

#### Scenario: 中型项目生成中等 Wiki
- **WHEN** 项目模块数 4-8 且文件数 50-200
- **THEN** 系统 SHALL 规划 25-45 页 Wiki，最大嵌套深度 3 层

#### Scenario: 大型项目生成深度 Wiki
- **WHEN** 项目模块数 ≥ 10 且文件数 > 200
- **THEN** 系统 SHALL 规划 45-80 页 Wiki，支持 4-5 层目录嵌套

### Requirement: 跨页面上下文传递
Wiki 页面生成阶段 SHALL 采用拓扑序上下文传递：子页面继承父页面摘要（500 字）和祖父页面标题；同层页面间传递已生成兄弟页面的标题和描述，避免内容重复。

#### Scenario: 子页面获得父页面上下文
- **WHEN** Level 3 页面开始生成，其父 Level 2 页面已完成
- **THEN** 该页面的生成 prompt SHALL 包含父页面的标题和前 500 字内容摘要

#### Scenario: 上下文窗口控制
- **WHEN** 父级链超过 3 层（曾祖父→祖父→父）
- **THEN** 系统 SHALL 仅注入直接父页面的完整摘要 + 祖父页面的标题和描述（不含全文摘要），控制上下文膨胀

### Requirement: 自动质量评估
全局收敛阶段 SHALL 对每个生成页面执行质量评估，输出 quality_score（0-100），评估维度包含：源代码覆盖度（30%权重）、技术深度（30%权重）、可读性（20%权重）、与标题的相关性（20%权重）。V7 新增评估维度：跨页面一致性检查和内容深度符合层级要求。

#### Scenario: 识别弱页面
- **WHEN** 某页面 quality_score < 60
- **THEN** 系统 SHALL 标记该页面为 `needs_regeneration` 并记录扣分原因

#### Scenario: 层级深度不符检测
- **WHEN** Article 类型页面（L4-5）缺少具体代码引用，内容过于概括
- **THEN** 质量评估扣分 20 分（技术深度维度），标注"内容深度不符合 article 页面要求"

### Requirement: 弱页面自动重生成
系统 SHALL 在质量评估后对标记为 `needs_regeneration` 的页面自动触发一轮重生成。V7 中重生成使用单页独立调用模式（不合并），并增加上下文量（检索 token 预算提升 30%）。

#### Scenario: 重生成后质量提升
- **WHEN** 弱页面触发重生成
- **THEN** 重生成的 prompt SHALL 包含"原始内容摘要"、"质量评估反馈"和"额外 30% 代码片段"

#### Scenario: 重生成上限控制
- **WHEN** 弱页面重生成后 quality_score 仍 < 60
- **THEN** 系统 SHALL 保留重生成结果、记录警告日志，不再触发进一步重生成（最多 1 轮）

### Requirement: 无 LLM 代码索引替代旧摘要
系统 SHALL 废弃 Stage 2 的 LLM 代码摘要环节（文件摘要、模块摘要、系统摘要），改用本地代码结构索引。

#### Scenario: 不再调用 LLM 生成文件摘要
- **WHEN** 执行代码分析阶段
- **THEN** 系统不调用任何 LLM Provider，仅执行本地文件遍历和符号提取

### Requirement: 旧管线数据清空
系统 SHALL 在部署新管线时清空旧的 Wiki 生成数据和摘要表，不保留旧管线产生的数据库记录。旧管线代码（CodeSummaryService 的 LLM 摘要方法、code-summary-* 提示词模板）SHALL 直接删除。

#### Scenario: 旧摘要表删除
- **WHEN** 执行新数据库迁移
- **THEN** 旧的 code_summaries 相关表被 DROP，新的 code_index_entries 和 code_index_chunks 表被 CREATE

#### Scenario: 旧代码删除
- **WHEN** 编译新版本代码
- **THEN** CodeSummaryService.cs 中的 LLM 摘要方法不存在，PromptSeedData.cs 中无 code-summary-* 模板

## REMOVED Requirements

### Requirement: 向后兼容
**Reason**: 旧管线输出质量低（示例代码、虚构 API），没有保留价值。当前处于开发验证期，无需兼容历史数据。
**Migration**: 清空数据库中的旧 Wiki 版本和摘要数据，使用新管线重新生成。

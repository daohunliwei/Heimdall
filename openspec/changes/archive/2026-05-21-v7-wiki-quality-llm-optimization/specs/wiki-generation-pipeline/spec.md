## MODIFIED Requirements

### Requirement: 新 Wiki 生成管线流程
系统 SHALL 按以下阶段执行 Wiki 生成：仓库准备  代码结构索引（增强） 深度代码理解（新增，含 LLM 辅助） 层级结构规划（重构，3-5 层嵌套） 拓扑序渐进式页面生成（重构，父先子后） 交叉引用编织（新增） 质量审查（增强） 渲染后处理  持久化  向量嵌入。

#### Scenario: 标准仓库 V7 Wiki 生成
- **WHEN** 用户触发 Wiki 刷新
- **THEN** 系统按 10 阶段顺序执行：Stage 2 执行增强的本地索引（含调用图），Stage 3 执行 1-2 次 LLM 辅助架构理解，Stage 4 输出多层嵌套结构，Stage 5 按拓扑序生成页面，Stage 6 执行交叉引用编织

#### Scenario: 管线中断恢复
- **WHEN** 管线在深度代码理解阶段后中断
- **THEN** 系统恢复时从 CodeUnderstandingResult 工件恢复，跳过已完成的代码索引和深度理解阶段

#### Scenario: V7 管线特性开关
- **WHEN** 环境变量 `HEIMDALL_WIKI_PIPELINE_VERSION` 未设置或设为 `v6`
- **THEN** 系统使用旧管线逻辑（8 阶段），设为 `v7` 时启用新 10 阶段管线

### Requirement: 条件化页面数量
系统 SHALL 根据深度代码理解阶段的输出（模块数量、入口点数量、设计模式数量、调用图深度、文件总数）动态决定目标页面数量，范围为 15-80 页。

#### Scenario: 小项目生成精简 Wiki
- **WHEN** 项目模块数  3 且文件数 < 50
- **THEN** 系统 SHALL 规划 15-20 页 Wiki，最大嵌套深度 2 层

#### Scenario: 中型项目生成中等 Wiki
- **WHEN** 项目模块数 4-8 且文件数 50-200
- **THEN** 系统 SHALL 规划 25-45 页 Wiki，最大嵌套深度 3 层

#### Scenario: 大型项目生成深度 Wiki
- **WHEN** 项目模块数  10 且文件数 > 200
- **THEN** 系统 SHALL 规划 45-80 页 Wiki，支持 4-5 层目录嵌套

### Requirement: 跨页面上下文传递
Wiki 页面生成阶段 SHALL 采用拓扑序上下文传递：子页面继承父页面摘要（500 字）和祖父页面标题；同层页面间传递已生成兄弟页面的标题和描述，避免内容重复。

#### Scenario: 子页面获得父页面上下文
- **WHEN** Level 3 页面开始生成，其父 Level 2 页面已完成
- **THEN** 该页面的生成 prompt SHALL 包含父页面的标题和前 500 字内容摘要

#### Scenario: 上下文窗口控制
- **WHEN** 父级链超过 3 层（曾祖父祖父父）
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

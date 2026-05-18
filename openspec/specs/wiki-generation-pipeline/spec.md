## ADDED Requirements

### Requirement: 新 Wiki 生成管线流程
系统 SHALL 按以下阶段执行 Wiki 生成：仓库准备 → 代码索引（本地，无 LLM）→ 结构规划（LLM）→ 检索增强页面生成（LLM + 混合检索）→ 质量审查 → 渲染后处理 → 持久化 → 向量嵌入。

#### Scenario: 标准仓库 Wiki 生成
- **WHEN** 用户触发 Wiki 刷新
- **THEN** 系统按 8 阶段顺序执行，Stage 2 不再调用 LLM 摘要，改为本地代码索引

#### Scenario: 管线中断恢复
- **WHEN** 管线在页面生成阶段中断
- **THEN** 系统恢复时从上一个成功的阶段继续，复用已生成的页面，不重新执行代码索引

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
系统 SHALL 根据代码分析阶段的输出（模块数量、文件总数、项目复杂度评分）动态决定目标页面数量，而非使用固定范围。

#### Scenario: 小项目生成精简 Wiki
- **WHEN** 项目模块数 ≤ 3 且文件数 < 50
- **THEN** 系统 SHALL 规划 6-10 页 Wiki

#### Scenario: 大型项目生成深度 Wiki
- **WHEN** 项目模块数 ≥ 10 且文件数 > 200
- **THEN** 系统 SHALL 规划 25-50 页 Wiki，支持 3 层目录嵌套

### Requirement: 跨页面上下文传递
Wiki 页面批量生成阶段 SHALL 将已生成页面的摘要（前 3 句 + 标题）注入后续页面生成 prompt 的上下文区域，避免内容重复并促进交叉引用。

#### Scenario: 后续页面获得前置页面上下文
- **WHEN** 第 2 批次的页面开始生成
- **THEN** 该批次每个页面的生成 prompt SHALL 包含第 1 批次所有已完成页面的标题与摘要

#### Scenario: 上下文窗口控制
- **WHEN** 已生成页面超过 20 个
- **THEN** 系统 SHALL 仅注入与当前页面最相关的 10 个页面摘要（基于 relatedPages 关系），而非全部

### Requirement: 自动质量评估
全局收敛阶段 SHALL 对每个生成页面执行质量评估，输出 quality_score（0-100），评估维度包含：源代码覆盖度、技术深度、可读性、与标题的相关性。

#### Scenario: 识别弱页面
- **WHEN** 某页面 quality_score < 60
- **THEN** 系统 SHALL 标记该页面为 `needs_regeneration` 并记录扣分原因

### Requirement: 弱页面自动重生成
系统 SHALL 在质量评估后对标记为 `needs_regeneration` 的页面自动触发一轮重生成，重生成 prompt 包含原始内容、质量评估反馈与改进指导。

#### Scenario: 重生成后质量提升
- **WHEN** 弱页面触发重生成
- **THEN** 重生成的 prompt SHALL 包含"原始内容摘要"与"质量评估反馈"，引导 LLM 针对性改进

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

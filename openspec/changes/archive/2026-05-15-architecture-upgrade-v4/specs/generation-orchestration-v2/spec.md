## ADDED Requirements

### Requirement: 跨页面上下文传递
Wiki 页面批量生成阶段 SHALL 将已生成页面的摘要（前 3 句 + 标题）注入后续页面生成 prompt 的上下文区域，避免内容重复并促进交叉引用。

#### Scenario: 后续页面获得前置页面上下文
- **WHEN** 第 2 批次的页面开始生成
- **THEN** 该批次每个页面的生成 prompt SHALL 包含第 1 批次所有已完成页面的标题与摘要

#### Scenario: 上下文窗口控制
- **WHEN** 已生成页面超过 20 个
- **THEN** 系统 SHALL 仅注入与当前页面最相关的 10 个页面摘要（基于 relatedPages 关系），而非全部

### Requirement: 条件化页面数量
系统 SHALL 根据代码分析阶段的输出（模块数量、文件总数、项目复杂度评分）动态决定目标页面数量，而非使用固定的 8-12 页。

#### Scenario: 小项目生成精简 Wiki
- **WHEN** 项目模块数 ≤ 3 且文件数 < 50
- **THEN** 系统 SHALL 规划 6-10 页 Wiki

#### Scenario: 大型项目生成深度 Wiki
- **WHEN** 项目模块数 ≥ 10 且文件数 > 200
- **THEN** 系统 SHALL 规划 25-50 页 Wiki，支持 3 层目录嵌套

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

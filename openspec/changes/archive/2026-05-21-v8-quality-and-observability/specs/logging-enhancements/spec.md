## MODIFIED Requirements

### Requirement: 任务结构化进度日志
Wiki 生成任务 SHALL 在每个关键步骤输出结构化进度日志，包含 Token 消耗追踪、缓存命中统计和累计成本展示。**修复**：日志中的 Token 数据必须来自实际 LLM 调用返回值，不得为 0。

#### Scenario: 页面生成步骤的进度日志
- **WHEN** Wiki 生成完成一个页面
- **THEN** 系统输出日志：`[WikiTask] 进度: 8/52 页 | Token: 125K↓ 48K↑ | 缓存: 32% | ¥0.42 | 页面: 核心服务架构 | LLM: minimax/MiniMax-M2.7 | 耗时: 12.3s`

#### Scenario: 任务完成的汇总日志
- **WHEN** Wiki 生成任务全部完成
- **THEN** 系统输出：`[WikiTask] 生成完成 | 总页数: 52 | 深度: 4 层 | 总耗时: 45m12s | LLM 调用: 58 次 | Token: 850K↓ 320K↑ | 缓存命中: 28% | ¥3.42`

### Requirement: LLM 调用详细日志
系统 SHALL 为每次 LLM 调用输出详细的调用日志。日志 SHALL 明确记录 Token 消耗、缓存命中、计费类型影响的策略选择。

#### Scenario: 调用后日志（增强）
- **WHEN** LLM 调用完成
- **THEN** 输出日志：`[LLM] 调用完成 | Provider: minimax/MiniMax-M2.7 | Input: 44832 | Output: 3241 | CacheHit: 12000 | Latency: 8.3s | ¥0.08`

### Requirement: 任务监控页面重设计
前端 `/admin/tasks` 页面 SHALL 包含：（1）顶部统计卡片行——总任务数、总 Token 消耗（输入/输出分开）、总成本、平均缓存命中率；（2）任务表格增强——每行显示 TaskId、类型、状态、进度、输入Token、输出Token、缓存命中、成本、Provider、耗时、创建时间、操作按钮；（3）行操作——查看详情（展开 LLM 调用明细表）、重新生成、取消。

#### Scenario: 统计卡片展示
- **WHEN** 管理员打开 /admin/tasks 页面
- **THEN** 顶部展示 4 个统计卡片：累计任务数、累计 Token 消耗（格式"850K↓ / 320K↑"）、累计成本（¥xx.xx）、平均缓存命中率（xx%）

#### Scenario: 任务详情展开
- **WHEN** 管理员点击某任务的"详情"按钮
- **THEN** 展开内嵌的 LLM 调用明细表：每次调用的 Stage、Provider、Model、InputTokens、OutputTokens、CacheHitTokens、LatencyMs、Cost、Success

#### Scenario: 筛选与排序
- **WHEN** 管理员选择状态筛选"运行中"或按 Provider 列排序
- **THEN** 表格动态过滤/排序，统计数据同步更新

### Requirement: 日志级别分类展示
系统 SHALL 在控制台输出中对不同严重级别的日志使用视觉区分。`[LLM]` 前缀用于所有 LLM 调用日志，`[Metrics]` 前缀用于指标汇总日志，`[WikiTask]` 前缀用于业务流程日志。

#### Scenario: LLM 日志与业务日志区分
- **WHEN** LLM 调用日志和业务流程日志同时输出
- **THEN** LLM 日志带有 `[LLM]` 前缀，业务流程日志带有 `[WikiTask]` 前缀，指标汇总带有 `[Metrics]` 前缀

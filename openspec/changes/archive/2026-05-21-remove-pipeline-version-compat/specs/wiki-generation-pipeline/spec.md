## MODIFIED Requirements

### Requirement: 新 Wiki 生成管线流程
系统 SHALL 按以下阶段执行 Wiki 生成：仓库准备 → 代码结构索引（增强）→ 深度代码理解（含 LLM 辅助）→ 层级结构规划（3-5 层嵌套）→ 拓扑序渐进式页面生成（父先子后）→ 交叉引用编织 → 质量审查（增强）→ 渲染后处理 → 持久化 → 向量嵌入。

#### Scenario: 标准仓库 Wiki 生成
- **WHEN** 用户触发 Wiki 刷新
- **THEN** 系统按 10 阶段顺序执行：Stage 2 执行增强的本地索引（含调用图），Stage 3 执行 1-2 次 LLM 辅助架构理解，Stage 4 输出多层嵌套结构，Stage 5 按拓扑序生成页面，Stage 6 执行交叉引用编织

#### Scenario: 管线中断恢复
- **WHEN** 管线在深度代码理解阶段后中断
- **THEN** 系统恢复时从 CodeUnderstandingResult 工件恢复，跳过已完成的代码索引和深度理解阶段

## REMOVED Requirements

### Requirement: V7 管线特性开关
**Reason**: 迭代版本不需要向后兼容，管线版本不应在代码中以条件分支形式体现。
**Migration**: 无需迁移——删除 `HEIMDALL_WIKI_PIPELINE_VERSION` 环境变量读取逻辑，10 阶段管线直接作为唯一路径执行。

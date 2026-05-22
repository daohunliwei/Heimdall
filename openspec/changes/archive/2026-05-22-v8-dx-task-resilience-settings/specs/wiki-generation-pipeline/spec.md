## MODIFIED Requirements

### Requirement: 新 Wiki 生成管线流程
系统 SHALL 按 10 阶段执行 Wiki 生成。**变更**：Stage 3（深度代码理解）的 CodeUnderstandingResult 必须作为 Stage 4（结构规划）的核心输入；Stage 5（页面生成）必须根据页面的 ContentDepthLevel 使用差异化提示词；**Stage 5 启动前须检查 Debug Mode 开关，开启时按 MaxDebugPages 截断页面列表**。

#### Scenario: 深度理解注入结构规划
- **WHEN** 管线执行至 Stage 4 结构规划
- **THEN** BuildWikiStructurePromptV7 方法接收 CodeUnderstandingResult，将架构模式、依赖拓扑、设计模式列表注入提示词上下文段

#### Scenario: 差异化提示词执行
- **WHEN** Stage 5 页面生成时，页面 ContentDepthLevel=article
- **THEN** 使用 article 级提示词（侧重代码深挖、逐方法分析、时序图）
- **AND** 页面 ContentDepthLevel=overview 时使用 overview 级提示词（侧重架构全景、模块关系）

#### Scenario: 调试模式截断页面列表
- **WHEN** 管线执行至 Stage 5 页面生成启动前，Debug Mode 开关为 `true` 且 `MaxDebugPages=5`，结构规划输出 20 个页面
- **THEN** 系统截断页面列表至前 5 个页面（按拓扑序、优先顶级页面），任务日志记录截断详情，Wiki 版本元数据标记 `debug_truncated: true`

#### Scenario: 调试模式关闭时全量生成
- **WHEN** 管线执行至 Stage 5 页面生成启动前，Debug Mode 开关为 `false`
- **THEN** 系统不做任何截断，全部结构规划页面进入生成阶段

## ADDED Requirements

### Requirement: 调试模式配置快照
任务启动时 SHALL 从数据库读取当前 Debug Mode 配置并快照到任务上下文，任务执行期间不受配置热切换影响。

#### Scenario: 任务期间热切换不影响当前任务
- **WHEN** Wiki 任务 A 以 `DebugMode=true, MaxDebugPages=5` 启动，任务执行期间管理员将 `DebugMode` 改为 `false`
- **THEN** 任务 A 仍以 Debug Mode 配置执行（限制 5 页），后续任务 B 以新配置执行（全量生成）

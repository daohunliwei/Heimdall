## MODIFIED Requirements

### Requirement: 新 Wiki 生成管线流程
系统 SHALL 按 10 阶段执行 Wiki 生成。**变更**：Stage 3（深度代码理解）的 CodeUnderstandingResult 必须作为 Stage 4（结构规划）的核心输入；Stage 5（页面生成）必须根据页面的 ContentDepthLevel 使用差异化提示词。

#### Scenario: 深度理解注入结构规划
- **WHEN** 管线执行至 Stage 4 结构规划
- **THEN** BuildWikiStructurePromptV7 方法接收 CodeUnderstandingResult，将架构模式、依赖拓扑、设计模式列表注入提示词上下文段

#### Scenario: 差异化提示词执行
- **WHEN** Stage 5 页面生成时，页面 ContentDepthLevel=article
- **THEN** 使用 article 级提示词（侧重代码深挖、逐方法分析、时序图）
- **AND** 页面 ContentDepthLevel=overview 时使用 overview 级提示词（侧重架构全景、模块关系）

### Requirement: 结构规划输入变更
结构规划阶段 SHALL 使用目录树、模块列表、入口点文件列表、关键技术栈信息、**CodeUnderstandingResult** 作为输入。

#### Scenario: 结构规划使用代码理解结果
- **WHEN** 系统执行结构规划
- **THEN** LLM 收到的输入除目录树和 README 外，还包含：架构模式名称和描述、模块依赖拓扑、设计模式列表及参与类、关键数据流路径

## ADDED Requirements

### Requirement: 质量审查独立提示词
系统 SHALL 在 Stage 7（质量审查）使用独立的审查提示词，而非复用页面生成逻辑。审查提示词 SHALL 包含：源代码覆盖度评估（30%）、技术深度评估（30%）、可读性评估（20%）、内容-标题相关性评估（20%）、层级深度符合性检查。

#### Scenario: 质量审查使用独立提示词
- **WHEN** Stage 7 执行质量审查
- **THEN** 系统使用 `quality-review` 提示词模板，LLM 输出每页的 quality_score 和扣分原因
- **AND** Article 页面缺少代码引用时，审查提示词明确标注为"层级深度不符"

### Requirement: Wiki 版本号递增保证
系统 SHALL 确保每次 Wiki 刷新生成严格递增的新版本号。版本号 SHALL 从已有最大版本号 + 1 计算，不复写任何已有版本。

#### Scenario: 新版本递增
- **WHEN** 仓库已有 v1-v4 四个 Wiki 版本，用户触发刷新
- **THEN** 系统创建 v5，不复写 v1-v4 中任何版本

#### Scenario: 首次生成
- **WHEN** 仓库无任何 Wiki 版本
- **THEN** 系统创建 v1

## ADDED Requirements

### Requirement: 提示词五层结构化架构
所有预设提示词 SHALL 采用"角色定义 → 上下文注入 → 分步指令 → 输出约束 → 质量自查清单"五层结构。每层 SHALL 以 Markdown 标题明确分隔，LLM 可清晰识别各层边界。

#### Scenario: 结构规划提示词含五层结构
- **WHEN** 系统构建结构规划提示词
- **THEN** 提示词包含：`## 角色`（你是有 X 年经验的软件架构师）、`## 上下文`（仓库分析数据）、`## 分步指令`（1.分析架构 2.设计层级 3.规划页面 4.分配文件 5.建立关联）、`## 输出约束`（JSON Schema）、`## 质量自查清单`（5-8 条自检项）

#### Scenario: 页面生成提示词含五层结构
- **WHEN** 系统构建页面生成提示词
- **THEN** 提示词包含：`## 角色`（技术文档专家）、`## 上下文`（父页面摘要、代码片段、仓库元数据）、`## 分步指令`（1.理解主题 2.分析代码 3.构建大纲 4.撰写内容 5.插入图表）、`## 输出约束`（Markdown 格式、代码引用规范、图表语法）、`## 质量自查清单`（验证代码引用真实性等）

### Requirement: 按内容深度级别差异化提示词
系统 SHALL 根据页面的 ContentDepthLevel 提供差异化的提示词指令。overview 页面侧重架构全景和模块关系；section 页面侧重模块边界和数据流分析；article 页面侧重实现细节和代码深挖。

#### Scenario: Overview 页面获得架构侧重指令
- **WHEN** 生成 ContentDepthLevel=overview 的页面
- **THEN** 提示词分步指令强调：不要深入代码实现、聚焦模块间关系、必须包含 Mermaid 架构图、页面间导航引用

#### Scenario: Article 页面获得代码深挖指令
- **WHEN** 生成 ContentDepthLevel=article 的页面
- **THEN** 提示词分步指令强调：必须以真实代码片段为核心、逐方法分析关键逻辑、使用表格对比参数/配置、必须包含 Mermaid 时序图

### Requirement: 代码理解结果注入结构规划
结构规划提示词 SHALL 将深度代码理解结果（CodeUnderstandingResult）作为上下文注入段的核心输入。注入内容 SHALL 包含：架构模式识别结果、模块依赖拓扑摘要、识别到的设计模式列表、关键数据流路径描述。

#### Scenario: 架构模式注入
- **WHEN** CodeUnderstandingResult 识别到"分层架构"模式
- **THEN** 结构规划提示词上下文段包含："该系统采用分层架构（Controller → Service → Repository），请据此设计 Wiki 层级结构，将 Controller 层归入 API 章节，Repository 层归入数据层章节"

#### Scenario: 设计模式注入
- **WHEN** CodeUnderstandingResult 包含 5 个设计模式
- **THEN** 提示词上下文段列出所有模式及其参与类，要求为每个模式创建独立 article 页面或归入相关 section

### Requirement: 输出格式与质量硬约束
所有提示词 SHALL 包含明确的输出格式约束和质量自查清单。约束 SHALL 包括：禁止虚构代码（只能用提供的代码片段）、禁止空泛描述（每个断言必须有代码证据）、Markdown 语法规范（表格、代码块、Mermaid 图）。

#### Scenario: 代码真实性约束
- **WHEN** LLM 生成页面内容
- **THEN** 提示词明确："代码引用必须来自下方提供的代码片段，禁止编造类名、方法名或 API。若某概念在提供的代码中无对应实现，请标注「未在代码中找到对应实现」而非猜测。"

#### Scenario: 质量自查清单执行
- **WHEN** LLM 完成页面生成
- **THEN** 提示词最后一段为自查清单："1. □ 所有代码引用是否来自提供的片段？2. □ 是否有至少一个 Mermaid 图？3. □ 技术深度是否符合页面级别要求？..."

### Requirement: 提示词模板可替换机制
系统 SHALL 支持通过 Prompt 管理界面（/admin/prompts）查看和编辑预设提示词模板，编辑后实时生效无需重启。提示词模板 SHALL 从数据库加载，数据库无记录时回退到代码中的默认模板。

#### Scenario: 管理员编辑提示词
- **WHEN** 管理员在 /admin/prompts 修改"结构规划"提示词
- **THEN** 下次结构规划阶段使用修改后的提示词，无需重启服务

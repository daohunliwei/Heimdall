## ADDED Requirements

### Requirement: 多层嵌套 Wiki 目录结构
系统 SHALL 支持生成 3-5 层嵌套的 Wiki 目录结构。结构层次模型为：顶层章节（Level 1）→ 子章节（Level 2）→ 页面组（Level 3）→ 详细页面（Level 4）→ 附录/深入页面（Level 5）。每个节点 SHALL 具有 parentId 指向其父节点。

#### Scenario: 大型项目 4 层嵌套
- **WHEN** 仓库包含 15+ 模块，500+ 文件
- **THEN** 结构规划输出 4 层嵌套结构，如：系统架构（L1）→ 后端架构（L2）→ 数据层详解（L3）→ EF Core 配置（L4）

#### Scenario: 小型项目 2 层嵌套
- **WHEN** 仓库包含 ≤3 模块，< 50 文件
- **THEN** 结构规划输出 2 层嵌套结构，如：概览（L1）→ 核心模块详解（L2）

#### Scenario: 动态深度决定
- **WHEN** 代码理解阶段确定项目复杂度
- **THEN** 系统根据公式确定最大深度：files < 50 → maxDepth=2，50-200 → maxDepth=3，200-500 → maxDepth=4，> 500 → maxDepth=5

### Requirement: 动态页面数量规划
系统 SHALL 根据代码理解阶段的输出动态确定目标页面数量。计算公式 SHALL 综合考虑：模块数量、入口点数量、设计模式数量、调用图深度、总文件数。目标范围为 15-80 页。

#### Scenario: 小型项目页面数量
- **WHEN** 仓库模块数=2，入口点=1，文件数=30，无设计模式
- **THEN** 目标页面数 = max(15, 2*3 + 1*2 + 0) = 15 页

#### Scenario: 大型项目页面数量
- **WHEN** 仓库模块数=12，入口点=5，文件数=800，设计模式=8，调用图深度=6
- **THEN** 目标页面数 = min(80, 12*3 + 5*2 + 8*2 + 6*3) = min(80, 36+10+16+18) = 80 页

#### Scenario: 中型项目页面数量
- **WHEN** 仓库模块数=6，入口点=3，文件数=150，设计模式=4，调用图深度=4
- **THEN** 目标页面数 = 6*3 + 3*2 + 4*2 + 4*3 = 18+6+8+12 = 44 页

### Requirement: 拓扑序渐进式页面生成
系统 SHALL 按树形拓扑序生成 Wiki 页面：先生成顶层 overview 页面（Level 1-2），再生成 section 页面（Level 3），最后生成 article/appendix 页面（Level 4-5）。子页面生成时 SHALL 继承父页面的摘要作为上下文。

#### Scenario: 父页面先于子页面生成
- **WHEN** 页面 A（L2）是页面 B（L3）的父页面
- **THEN** 系统确保 A 在 B 之前生成完成，B 的生成 prompt 包含 A 的标题和前 500 字摘要

#### Scenario: 同层页面并行生成
- **WHEN** 页面 C 和 D 同为 Level 3，父页面均已完成
- **THEN** 系统可将 C 和 D 放入同一批次并行生成

#### Scenario: 上下文继承链
- **WHEN** 页面 E（L4）的父页面为 D（L3），D 的父页面为 B（L2）
- **THEN** E 的生成 prompt 包含 B 的标题+摘要（祖父级）和 D 的标题+摘要（父级），提供完整上下文链

### Requirement: 内容深度分级模型
系统 SHALL 根据页面的层级和类型确定内容深度要求：overview 页面（L1-2）侧重架构概览和关系说明，section 页面（L3）侧重模块分析和数据流，article 页面（L4-5）侧重实现细节和代码解读。

#### Scenario: Overview 页面内容要求
- **WHEN** 生成 overview 类型页面
- **THEN** 页面 prompt 要求：提供系统架构鸟瞰图、模块间关系说明、Mermaid 架构图、不深入实现细节

#### Scenario: Article 页面内容要求
- **WHEN** 生成 article 类型页面
- **THEN** 页面 prompt 要求：深入分析具体实现、引用真实代码片段（方法签名+核心逻辑）、表格说明参数/配置、Mermaid 时序图展示调用流程

#### Scenario: Section 页面内容要求
- **WHEN** 生成 section 类型页面
- **THEN** 页面 prompt 要求：介绍模块职责和边界、数据流分析、关键类/接口概述、指向子页面的导航引用

### Requirement: 交叉引用编织后处理
系统 SHALL 在所有页面生成完成后执行交叉引用编织阶段，分析页面内容自动插入：相关页面链接块（"另见"区域）、代码符号跨页面追踪链接、术语首次出现页面标注。

#### Scenario: 自动插入相关页面链接
- **WHEN** 页面 A 提到了"用户认证"概念，而页面 B 标题为"认证系统详解"
- **THEN** 系统在页面 A 的相关位置附近自动插入链接 `[详见：认证系统详解](./page-b)`

#### Scenario: 代码符号跨页面追踪
- **WHEN** 页面 C 和页面 D 都引用了 `UserService` 类
- **THEN** 系统在两个页面的符号首次出现处互相插入链接，指向对方页面的相关段落

#### Scenario: 术语交叉引用
- **WHEN** "DI 容器"术语在页面 E 首次详细解释，后续在页面 F、G 中被提及
- **THEN** 页面 F、G 中的"DI 容器"文字旁注明 `（详见：[依赖注入详解](./page-e#di-container)）`

### Requirement: 结构规划 prompt 重写
结构规划阶段的 prompt SHALL 接收深度代码理解结果（架构洞察、依赖拓扑、设计模式列表）作为额外输入，并要求 LLM 输出支持多层嵌套的 JSON 结构（sections 内嵌 children，pages 含 depth 字段）。

#### Scenario: 结构规划输入扩展
- **WHEN** 系统执行结构规划
- **THEN** LLM 收到的输入除目录树和 README 外，还包含：模块依赖拓扑图、识别到的设计模式列表、架构模式名称和描述、关键数据流路径

#### Scenario: 结构规划输出嵌套树
- **WHEN** 结构规划完成
- **THEN** 输出 JSON 包含嵌套的 sections.children 结构，每个页面包含 depth（1-5）、parentId、contentDepthLevel（overview/section/article）字段

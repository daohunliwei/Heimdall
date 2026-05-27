## Purpose

前端基础质量——涵盖 API 类型层与统一客户端、组件化布局、通用加载/错误/空态组件、版本上下文一致性、响应式适配及 UI 细节修复。
## Requirements
### Requirement: API 类型层
系统前端 SHALL 维护与后端 API 一致的 TypeScript 类型定义层。统一的 apiClient HTTP 抽象层为后续迭代计划——当前各页面直接使用 `fetch()` 完成 API 调用。

#### Scenario: API 响应类型不匹配时优雅降级
- **WHEN** 后端返回的 JSON 字段与前端类型定义不一致
- **THEN** 系统在控制台记录警告，对缺失字段使用安全默认值，不导致页面崩溃

#### Scenario: 网络请求失败时统一错误展示
- **WHEN** 任意 API 请求返回 4xx/5xx 或网络超时
- **THEN** 对应页面区域展示统一的错误提示组件，包含错误信息与重试按钮

### Requirement: 统一加载态与错误态组件
系统 SHALL 提供 LoadingState、ErrorState、EmptyState 三个通用组件，所有页面区域 MUST 使用这些组件。

#### Scenario: 长时间任务进行中
- **WHEN** Wiki 生成任务正在执行
- **THEN** 页面展示带进度条的 LoadingState，实时更新进度百分比与阶段描述

### Requirement: 仓库详情页布局
仓库详情页的 Wiki 浏览、侧边导航、页面内容、操作栏功能当前集中实现在 `page.tsx` 中。WikiBrowser 和 WikiSidebar 为 V4 占位组件，独立子组件拆分和加载/错误状态隔离为后续迭代方向。

#### Scenario: Wiki 页面树加载
- **WHEN** 用户进入仓库详情页且存在已发布 WikiVersion
- **THEN** 左侧导航展示页面树，支持多层嵌套展开/折叠，当前页面高亮

#### Scenario: 无 Wiki 数据时的空态引导
- **WHEN** 仓库无任何 WikiVersion
- **THEN** 页面展示空态引导，包含"生成 Wiki"按钮与简要说明

### Requirement: Ask/Slides/Workshop 版本上下文一致性
Ask、Slides、Workshop 页面 SHALL 自动继承仓库详情页当前选中的 wikiVersionId。

#### Scenario: 切换版本后使用 Ask
- **WHEN** 用户在仓库页面切换到历史 WikiVersion 后打开 Ask
- **THEN** Ask 基于该历史版本的知识库进行问答

### Requirement: 响应式布局与主题适配
所有页面 SHALL 适配 desktop（≥1024px）与 tablet（768-1023px）两种视口，深色/浅色主题切换无样式异常。窄屏侧边栏使用 `w-full lg:w-72` 堆叠布局，抽屉/汉堡菜单模式为后续迭代计划。

### Requirement: Wiki 版本选择与记忆
Wiki 页面加载时 SHALL 默认选择最新版本。用户选择的版本 SHALL 记录到 localStorage，页面刷新时恢复。VersionSwitcher 中的仓库快照列表 SHALL 可选择。

#### Scenario: 首次加载默认选择最新版本
- **WHEN** 用户进入仓库详情页
- **THEN** 系统按 createdAt 降序排序，自动选择第一个 Completed 或 Published 的版本

#### Scenario: 切换版本后刷新页面恢复选择
- **WHEN** 用户从 V3 切换到 V5 后刷新页面
- **THEN** 系统从 localStorage 恢复 V5 选择

#### Scenario: 选择不同快照
- **WHEN** 用户在 VersionSwitcher 中点击某个仓库快照
- **THEN** 系统重新加载对应快照下的 Wiki 内容

### Requirement: RefreshPanel 引导说明
RefreshPanel 弹窗 SHALL 为"刷新策略"和"生成档位"选项提供清晰的 Tooltip 说明。Provider/Model 选择器 SHALL 使用 UserSelector 组件。

#### Scenario: 鼠标悬停查看说明
- **WHEN** 用户将鼠标悬停在选项的 Tooltip 图标上
- **THEN** 显示对应选项的详细说明

### Requirement: Markdown 内联代码正确渲染
单反引号包裹的内联代码 SHALL 正确渲染为行内 code 元素，带 monospace 字体、背景色和圆角。`rehype-raw` 插件已安装（`package.json`），完整配置待后续版本激活。

#### Scenario: 单反引号内联代码渲染
- **WHEN** Markdown 内容包含单反引号内联代码
- **THEN** 渲染为行内 code 元素，带 monospace 字体和背景色

#### Scenario: LLM 输出的原始 HTML code 标签
- **WHEN** LLM 返回内容中包含 `<code>someFunction()</code>` 原始 HTML 标签
- **THEN** 渲染为内联 code 元素，不触发块级 SyntaxHighlighter

### Requirement: Wiki 多层树结构
Wiki 页面树 SHALL 支持 2-5 层嵌套结构，前端 TreeView 展示层级缩进引导线和折叠/展开动画。

#### Scenario: 小仓库浅层结构 / 大仓库深层结构
- **WHEN** 仓库文件数 < 50 → 至少 2 层；文件数 > 200 → 可生成 4-5 层
- **THEN** 前端 TreeView 对每层增加视觉引导线，支持折叠/展开

#### Scenario: TreeView 折叠展开交互
- **WHEN** 用户点击有子节点的章节
- **THEN** 子节点以 CSS transition 动画折叠或展开

## ADDED Requirements

### Requirement: 首页主题切换按钮布局修复

首页 header 中的 `ThemeToggle` 按钮 SHALL 在视觉上正确对齐，与右侧其他按钮/链接保持一致的间距和垂直居中。

#### Scenario: 首页 header 布局检查
- **WHEN** 用户访问首页 `/`
- **THEN** header 中所有元素（Logo、Wiki Projects 链接、ThemeToggle）在同一水平线上，无错位或溢出
- **AND** ThemeToggle 与相邻元素的间距与其他元素之间一致

### Requirement: Wiki 版本默认选择最新版本

Wiki 页面加载时 SHALL 默认选择按时间倒序排列的最新 Wiki 版本，而非固定选择 V1 版本。

#### Scenario: 首次加载 Wiki 页面
- **WHEN** 用户进入 `/repositories/{repositoryId}` 页面
- **THEN** 系统从 `wikiVersions` 数组中按 `createdAt` 降序排序，自动选择第一个状态为 "Completed" 或 "Published" 的版本
- **AND** 若所有版本均未完成，则选择最新版本（无论状态）

#### Scenario: 仅有 V1 版本时
- **WHEN** 仓库仅有 V1 一个 Wiki 版本
- **THEN** 系统默认选择 V1 版本

### Requirement: Wiki 版本选择前端记忆

用户选择的 Wiki 版本 SHALL 被记录到浏览器 `localStorage`，页面刷新或重新进入时恢复上次选择。

#### Scenario: 切换版本后刷新页面
- **WHEN** 用户从 V3 切换到 V5，然后刷新页面
- **THEN** 系统从 `localStorage` 读取 key `heimdall:lastWikiVersion:{repositoryId}`，若值为 V5 且 V5 在版本列表中，则自动选中 V5
- **AND** 若 localStorage 中的版本已不在列表中（被删除），则回退到最新版本

#### Scenario: 首次访问无记忆
- **WHEN** 用户首次访问某仓库的 Wiki 页面，localStorage 中无记录
- **THEN** 系统默认选择最新版本

### Requirement: 仓库快照可选择

`VersionSwitcher` 中的仓库快照列表 SHALL 从只读改为可选择，用户选择快照后触发对应仓库版本的加载。

#### Scenario: 选择不同快照
- **WHEN** 用户在 VersionSwitcher 中点击某个仓库快照
- **THEN** 系统调用 `onVersionChange` 回调，传入对应的 `repositoryVersionId`，重新加载对应快照下的 Wiki 内容

#### Scenario: 当前选中快照高亮
- **WHEN** 某个仓库快照被选中
- **THEN** 该快照条目显示选中样式（如边框高亮或背景色变化），与其他未选中条目区分

### Requirement: 刷新弹窗引导说明

`RefreshPanel` 弹窗 SHALL 为"刷新策略"和"生成档位"选项提供清晰的 Tooltip 说明，帮助用户理解每个选项的含义。

#### Scenario: 鼠标悬停查看刷新策略说明
- **WHEN** 用户将鼠标悬停在"刷新策略"选项的 Tooltip 图标上
- **THEN** 显示说明："**最新版本**：拉取远程仓库最新代码后重新生成 Wiki；**当前快照**：基于已保存的仓库快照重新生成，不拉取新代码"

#### Scenario: 鼠标悬停查看生成档位说明
- **WHEN** 用户将鼠标悬停在"生成档位"选项的 Tooltip 图标上
- **THEN** 显示说明："**完整**：生成全面的代码分析、架构图和模块文档；**简洁**：仅生成核心文件和入口点文档，速度更快"

#### Scenario: Provider/Model 选择器使用动态数据
- **WHEN** RefreshPanel 渲染 Provider/Model 选择器
- **THEN** 选择器 SHALL 使用 `UserSelector` 组件（动态获取 `/api/models/config`）而非硬编码的 `<select>` 元素
- **AND** 选择器的选项与当前系统配置的后端 Provider 列表一致

### Requirement: Markdown 内联代码正确渲染

单反引号包裹的内联代码 SHALL 正确渲染为行内 `<code>` 元素，不被误渲染为块级代码块。`rehype-raw` 插件处理原始 HTML `<code>` 标签时产生的 `inline === undefined` 节点 SHALL 走内联渲染路径。

#### Scenario: 单反引号内联代码
- **WHEN** Markdown 内容包含 `` `const x = 1;` `` 单反引号内联代码
- **THEN** 渲染为 `<code>` 行内元素，带有 monospace 字体、背景色和圆角，不换行

#### Scenario: LLM 输出的原始 HTML code 标签
- **WHEN** LLM 返回内容中包含 `<code>someFunction()</code>` 原始 HTML 标签（经 rehype-raw 转为 hast 节点，`inline` 为 `undefined`）
- **THEN** 渲染为内联 `<code>` 元素，不触发块级代码块的 SyntaxHighlighter

#### Scenario: 三反引号代码块正常渲染
- **WHEN** Markdown 内容包含 ```` ```javascript\nconst x = 1;\n``` ```` 三反引号围栏代码块
- **THEN** react-markdown 传入 `inline === false`，块级代码路径正常执行，显示语言标签和复制按钮

### Requirement: Wiki 多层树结构

Wiki 页面树 SHALL 支持 2-5 层嵌套结构，模型根据仓库文件总数和复杂度自行判断层级深度。前端 TreeView SHALL 展示层级缩进引导线和折叠/展开动画。

#### Scenario: 小仓库的浅层结构
- **WHEN** 仓库文件数 < 50 且结构简单
- **THEN** 模型生成至少 2 层（章节 → 页面）的 Wiki 树，前端显示一级缩进

#### Scenario: 大型仓库的深层结构
- **WHEN** 仓库文件数 > 200 且模块复杂
- **THEN** 模型可生成 4-5 层（章 → 节 → 子节 → 页面 → 子页面）的 Wiki 树
- **AND** 前端 TreeView 对每层增加视觉引导线（左边框线），支持折叠/展开

#### Scenario: TreeView 折叠展开交互
- **WHEN** 用户点击某个有子节点的章节
- **THEN** 子节点以 CSS transition 动画折叠或展开（`max-height` 过渡 + `overflow: hidden`）
- **AND** 折叠/展开图标（箭头）旋转 90 度指示状态

#### Scenario: 最大深度限制
- **WHEN** Wiki 树结构超过 5 层深度
- **THEN** 模型被提示词约束为最多 5 层，不生成 6 层及以上嵌套

## 1. 数据库与实体变更（提示词管理基础）

- [x] 1.1 扩展 `PromptTemplate` 实体：新增 `Category`、`SubCategory`、`Priority`、`ApplicableProviders` 字段
- [x] 1.2 扩展 `PromptTemplateConfiguration` (EF Fluent API)：添加新列的类型配置和索引
- [x] 1.3 生成 EF Core 增量迁移 `V5PromptManagement`
- [x] 1.4 执行数据库迁移

## 2. 提示词管理 - 核心服务层

- [x] 2.1 创建 `IPromptMergeService` 接口：定义 `BuildPrompt(category, provider, outputFormat, variables)` 方法
- [x] 2.2 实现 `PromptMergeService`：按 Category + Priority 查询模板，按 ApplicableProviders 过滤，拼接片段，执行变量插值
- [x] 2.3 扩展 `IPromptTemplateRepository`：新增 `GetByCategoryAsync`、`GetBySlugAsync` 查询方法
- [x] 2.4 基于 deepwiki-open 原始英文提示词，重写全新中文提示词：结构规划（wiki_structure — 含多层树结构 2-5 层边界规则）、页面生成（wiki_page — 含完整 Markdown 样式规范）、Slides 生成、Workshop 生成、聊天 system prompt、代码分析摘要三级
- [x] 2.5 完善 `PromptSeedData`：将全部中文提示词纳入种子数据，配置 Category/SubCategory/Priority/ApplicableProviders
- [x] 2.6 创建 `backend/Heimdall.Repository/Data/SeedScripts/v5_prompts.sql`：纯 SQL 初始化脚本，INSERT ON CONFLICT DO NOTHING，可直接执行恢复
- [x] 2.7 更新 `Program.cs` DI 注册：注册 `IPromptMergeService`、更新种子数据调用

## 3. 提示词管理 - Provider 层迁移

- [x] 3.1 扩展 `ChatRequest` 类：新增 `SystemPrompt` 可空字段
- [x] 3.2 修改 `OllamaChatProvider.GenerateAsync`：移除硬编码 system prompt（第 61 行），改为读取 `request.SystemPrompt`
- [x] 3.3 修改其余 5 个 Provider（OpenAiCompatible、Azure、Google、MiniMax、Bedrock）：支持读取并发送 `request.SystemPrompt`
- [x] 3.4 修改 `ChatController.StreamChat`：移除硬编码 `systemPrompt` 拼接（第 58-59 行），改为通过 `IPromptMergeService` 获取
- [x] 3.5 修改 `CodeSummaryService` 三个方法：改为通过 `IPromptMergeService` 获取提示词模板
- [x] 3.6 修改 `TaskPromptService`：`TryResolveManagedTemplateAsync` 改为调用 `IPromptMergeService.BuildPrompt`，将 7 个硬编码方法标记为废弃
- [x] 3.7 修改 `WikiTaskService`：完整接入 `TryResolveManagedTemplateAsync`，替换直接调用 `BuildWikiStructurePrompt`/`BuildWikiPagePrompt` 的逻辑
- [x] 3.8 修改 `SlidesTaskService` 和 `WorkshopTaskService`：接入 `IPromptMergeService` 获取提示词

## 4. 提示词管理 - API 层

- [x] 4.1 废弃 `PromptsController`（`admin/prompts`），保留路由但返回 410 并指示迁移到 `/api/admin/prompt-templates`
- [x] 4.2 完善 `PromptTemplatesController`：确保 CRUD、版本历史、回滚、覆盖管理、运行时预览接口完整可用
- [x] 4.3 新增 `GET /api/admin/prompt-templates/categories`：返回所有可用 Category/SubCategory 列表

## 5. 前端 UI 修复

- [x] 5.1 修复首页 header 中 `ThemeToggle` 的布局错位：检查 flex 对齐属性和间距
- [x] 5.2 修复 Wiki 版本默认选择逻辑：在 `loadInitialData` 中按 `createdAt DESC` 排序取最新完成版本
- [x] 5.3 实现 Wiki 版本选择记忆：`VersionSwitcher` 中 `onVersionChange` 写入 `localStorage`（key: `heimdall:lastWikiVersion:{repoId}`），页面加载时优先读取
- [x] 5.4 修复仓库快照选择器：将只读快照列表改为可选，onClick 触发 `onVersionChange` 传入 `repositoryVersionId`
- [x] 5.5 为 `RefreshPanel` 的"刷新策略"添加 Tooltip 说明（最新版本 vs 当前快照的含义）
- [x] 5.6 为 `RefreshPanel` 的"生成档位"添加 Tooltip 说明（完整 vs 简洁的含义）
- [x] 5.7 将 `RefreshPanel` 中硬编码的 Provider/Model `<select>` 替换为 `UserSelector` 动态组件
- [x] 5.8 修复 `Markdown.tsx` 内联代码渲染 Bug：`!inline` 改为 `inline === false` 严格判断（第 118 行 Mermaid 检查 + 第 126 行块级代码检查）
- [x] 5.9 优化 `WikiTreeView`：增加层级缩进引导线（左边框线）、折叠/展开 CSS transition 动画、选中页面高亮和父节点自动展开

## 6. Slides/Workshop 模型参数修复

- [x] 6.1 修改 `SlidesTaskService`：读取请求中的 `model` 参数，若为空则从系统配置获取默认值，校验必填
- [x] 6.2 修改 `WorkshopTaskService`：同 Slides 逻辑，读取/校验 model 参数
- [x] 6.3 Slides 页面添加"当前模型"标签展示和模型选择入口（无 model 参数时弹窗选择）
- [x] 6.4 Workshop 页面添加"当前模型"标签展示和模型选择入口
- [x] 6.5 统一 Slides/Workshop/RefreshPanel 的模型选择器为 `UserSelector` 组件

## 7. 日志分类与过滤

- [x] 7.1 创建 `LogCategoryFilter` 单例服务：维护 `ShowSqlCommands`/`ShowEfCore` 开关状态
- [x] 7.2 实现自定义日志过滤：通过 `DynamicLogFilterOptions` + `IPostConfigureOptions<LoggerFilterOptions>` 动态过滤
- [x] 7.3 创建 `GET /api/admin/logging/status` 端点：返回当前过滤状态
- [x] 7.4 创建 `POST /api/admin/logging/filter` 端点：接受 `{ showSql, showEfCore }` 动态切换
- [x] 7.5 在 `Program.cs` 中注册 `LogCategoryFilter` 和自定义 LoggerProvider，默认关闭 SQL 日志
- [x] 7.6 在 `Program.cs` 中读取环境变量 `HEIMDALL_LOG_SQL`，若为 `true` 则在启动时预设 `LogCategoryFilter.ShowSqlCommands = true`

## 8. 任务结构化进度日志

- [x] 8.1 创建 `IStructuredLogger` 接口：定义 `LogTaskProgress`、`LogLlmCall`、`LogTaskSummary` 方法
- [x] 8.2 实现 `StructuredLogger`：统一前缀格式 `[WikiTask]`、`[SQL]`、`[LLM]`，包含 TaskId 上下文
- [x] 8.3 修改 `WikiTaskService`：在仓库准备、结构规划、逐页生成、弱页重生、完成等关键步骤调用 `StructuredLogger.LogTaskProgress`
- [x] 8.4 修改 `WikiGenerationParserService`：将 JSON 解析失败的 `Debug` 日志提升为 `Warning`，增加页面 ID 和任务上下文
- [x] 8.5 在 `Program.cs` 中注册 `IStructuredLogger`

## 9. 构建验证与测试

- [x] 9.1 执行 `dotnet build` 验证后端编译通过
- [x] 9.2 执行 `npm run build` 验证前端编译通过
- [x] 9.3 验证首页：ThemeToggle 对齐、URL 输入→导入→跳转 Wiki 页面流程正常
- [x] 9.4 验证 Wiki 版本管理：默认选最新、版本切换、刷新记忆、快照选择
- [x] 9.5 验证 Slides：模型选择→生成→全屏播放→导出 HTML 全流程
- [x] 9.6 验证 Workshop：模型选择→生成→导出 Markdown 全流程
- [x] 9.7 验证日志过滤：API 开关→SQL 日志消失/出现→进度日志输出→错误日志包含异常详情
- [x] 9.8 验证提示词管理：Admin 面板 CRUD→版本历史→回滚→仓库覆盖→运行时解析正确→SQL 脚本可独立恢复
- [x] 9.9 验证 Markdown 渲染：单反引号内联代码、三反引号代码块、原始 HTML `<code>` 标签均正确渲染
- [x] 9.10 验证多层树结构：小仓库 2 层、大型仓库 4-5 层→TreeView 缩进引导线→折叠/展开动画
- [x] 9.11 验证 SQL 日志启动预设：`HEIMDALL_LOG_SQL=true` 启动→SQL 日志可见→运行时 API 关闭→日志消失

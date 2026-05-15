## 1. 数据库与实体变更（提示词管理基础）

- [ ] 1.1 扩展 `PromptTemplate` 实体：新增 `Category`、`SubCategory`、`Priority`、`ApplicableProviders` 字段
- [ ] 1.2 扩展 `PromptTemplateConfiguration` (EF Fluent API)：添加新列的类型配置和索引
- [ ] 1.3 生成 EF Core 增量迁移 `V5PromptManagement`
- [ ] 1.4 执行数据库迁移

## 2. 提示词管理 - 核心服务层

- [ ] 2.1 创建 `IPromptMergeService` 接口：定义 `BuildPrompt(category, provider, outputFormat, variables)` 方法
- [ ] 2.2 实现 `PromptMergeService`：按 Category + Priority 查询模板，按 ApplicableProviders 过滤，拼接片段，执行变量插值
- [ ] 2.3 扩展 `IPromptTemplateRepository`：新增 `GetByCategoryAsync`、`GetBySlugAsync` 查询方法
- [ ] 2.4 完善 `PromptSeedData`：将 `TaskPromptService` 的 7 个硬编码方法、`ChatController` system prompt、`CodeSummaryService` 的 3 个模板、`OllamaChatProvider` 角色设定完整迁移入种子数据
- [ ] 2.5 更新 `Program.cs` DI 注册：注册 `IPromptMergeService`、更新种子数据调用

## 3. 提示词管理 - Provider 层迁移

- [ ] 3.1 扩展 `ChatRequest` 类：新增 `SystemPrompt` 可空字段
- [ ] 3.2 修改 `OllamaChatProvider.GenerateAsync`：移除硬编码 system prompt（第 61 行），改为读取 `request.SystemPrompt`
- [ ] 3.3 修改其余 5 个 Provider（OpenAiCompatible、Azure、Google、MiniMax、Bedrock）：支持读取并发送 `request.SystemPrompt`
- [ ] 3.4 修改 `ChatController.StreamChat`：移除硬编码 `systemPrompt` 拼接（第 58-59 行），改为通过 `IPromptMergeService` 获取
- [ ] 3.5 修改 `CodeSummaryService` 三个方法：改为通过 `IPromptMergeService` 获取提示词模板
- [ ] 3.6 修改 `TaskPromptService`：`TryResolveManagedTemplateAsync` 改为调用 `IPromptMergeService.BuildPrompt`，将 7 个硬编码方法标记为废弃
- [ ] 3.7 修改 `WikiTaskService`：完整接入 `TryResolveManagedTemplateAsync`，替换直接调用 `BuildWikiStructurePrompt`/`BuildWikiPagePrompt` 的逻辑
- [ ] 3.8 修改 `SlidesTaskService` 和 `WorkshopTaskService`：接入 `IPromptMergeService` 获取提示词

## 4. 提示词管理 - API 层

- [ ] 4.1 废弃 `PromptsController`（`admin/prompts`），保留路由但返回 410 并指示迁移到 `/api/admin/prompt-templates`
- [ ] 4.2 完善 `PromptTemplatesController`：确保 CRUD、版本历史、回滚、覆盖管理、运行时预览接口完整可用
- [ ] 4.3 新增 `GET /api/admin/prompt-templates/categories`：返回所有可用 Category/SubCategory 列表

## 5. 前端 UI 修复

- [ ] 5.1 修复首页 header 中 `ThemeToggle` 的布局错位：检查 flex 对齐属性和间距
- [ ] 5.2 修复 Wiki 版本默认选择逻辑：在 `loadInitialData` 中按 `createdAt DESC` 排序取最新完成版本
- [ ] 5.3 实现 Wiki 版本选择记忆：`VersionSwitcher` 中 `onVersionChange` 写入 `localStorage`（key: `heimdall:lastWikiVersion:{repoId}`），页面加载时优先读取
- [ ] 5.4 修复仓库快照选择器：将只读快照列表改为可选，onClick 触发 `onVersionChange` 传入 `repositoryVersionId`
- [ ] 5.5 为 `RefreshPanel` 的"刷新策略"添加 Tooltip 说明（最新版本 vs 当前快照的含义）
- [ ] 5.6 为 `RefreshPanel` 的"生成档位"添加 Tooltip 说明（完整 vs 简洁的含义）
- [ ] 5.7 将 `RefreshPanel` 中硬编码的 Provider/Model `<select>` 替换为 `UserSelector` 动态组件

## 6. Slides/Workshop 模型参数修复

- [ ] 6.1 修改 `SlidesTaskService`：读取请求中的 `model` 参数，若为空则从系统配置获取默认值，校验必填
- [ ] 6.2 修改 `WorkshopTaskService`：同 Slides 逻辑，读取/校验 model 参数
- [ ] 6.3 Slides 页面添加"当前模型"标签展示和模型选择入口（无 model 参数时弹窗选择）
- [ ] 6.4 Workshop 页面添加"当前模型"标签展示和模型选择入口
- [ ] 6.5 统一 Slides/Workshop/RefreshPanel 的模型选择器为 `UserSelector` 组件

## 7. 日志分类与过滤

- [ ] 7.1 创建 `LogCategoryFilter` 单例服务：维护 `ShowSqlCommands`/`ShowEfCore` 开关状态
- [ ] 7.2 实现自定义 `ILoggerProvider`：根据 `LogCategoryFilter` 状态过滤特定类别的日志
- [ ] 7.3 创建 `GET /api/admin/logging/status` 端点：返回当前过滤状态
- [ ] 7.4 创建 `POST /api/admin/logging/filter` 端点：接受 `{ showSql, showEfCore }` 动态切换
- [ ] 7.5 在 `Program.cs` 中注册 `LogCategoryFilter` 和自定义 LoggerProvider，默认关闭 SQL 日志

## 8. 任务结构化进度日志

- [ ] 8.1 创建 `IStructuredLogger` 接口：定义 `LogTaskProgress`、`LogLlmCall`、`LogTaskSummary` 方法
- [ ] 8.2 实现 `StructuredLogger`：统一前缀格式 `[WikiTask]`、`[SQL]`、`[LLM]`，包含 TaskId 上下文
- [ ] 8.3 修改 `WikiTaskService`：在仓库准备、结构规划、逐页生成、弱页重生、完成等关键步骤调用 `StructuredLogger.LogTaskProgress`
- [ ] 8.4 修改 `WikiGenerationParserService`：将 JSON 解析失败的 `Debug` 日志提升为 `Warning`，增加页面 ID 和任务上下文
- [ ] 8.5 在 `Program.cs` 中注册 `IStructuredLogger`

## 9. 构建验证与测试

- [ ] 9.1 执行 `dotnet build` 验证后端编译通过
- [ ] 9.2 执行 `npm run build` 验证前端编译通过
- [ ] 9.3 验证首页：ThemeToggle 对齐、URL 输入→导入→跳转 Wiki 页面流程正常
- [ ] 9.4 验证 Wiki 版本管理：默认选最新、版本切换、刷新记忆、快照选择
- [ ] 9.5 验证 Slides：模型选择→生成→全屏播放→导出 HTML 全流程
- [ ] 9.6 验证 Workshop：模型选择→生成→导出 Markdown 全流程
- [ ] 9.7 验证日志过滤：API 开关→SQL 日志消失/出现→进度日志输出→错误日志包含异常详情
- [ ] 9.8 验证提示词管理：Admin 面板 CRUD→版本历史→回滚→仓库覆盖→运行时解析正确

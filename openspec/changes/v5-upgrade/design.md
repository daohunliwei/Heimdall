## Context

Heimdall V4 存在四大痛点：Provider 层硬编码 system prompt 与任务级 prompt 冲突（OllamaChatProvider 第 61 行强制输出 XML，但页面生成要求 JSON）；前端交互缺陷（主题切换错位、Wiki 版本默认 V1 而非最新、快照选择器失效、刷新弹窗引导缺失）；Slides/Workshop 因缺失 model 参数报 400 错误；SQL 日志淹没控制台。

当前提示词管理为"双轨制"——旧的 `TaskPromptService` 包含硬编码字符串，新的 `PromptManagementService` + `PromptTemplate` 实体体系已有雏形但种子数据为占位符，工作流服务仍直接调用硬编码方法而非走数据库解析。`TaskPromptService.TryResolveManagedTemplateAsync` 桥接方法存在但未被完整接入。

Provider 层现状：仅 `OllamaChatProvider` 硬编码了 `role: "system"` 消息；`ChatController` 的 SSE 聊天端点将 system prompt 拼入 user prompt 字符串；`CodeSummaryService` 三个方法含硬编码指令模板。其余五个 Provider（OpenAI Compatible、Azure、Google、MiniMax、Bedrock）无硬编码 system prompt。

## Goals / Non-Goals

**Goals:**
- 将所有提示词（Provider system prompt、任务 prompt、代码分析 prompt）统一迁移至数据库 `PromptTemplates` 表管理
- 实现 Prompt 片段合并引擎：请求时按 `[任务类型]` + `[Provider]` + `[输出格式]` 三个维度组合基础模板、格式指令、Provider 个性片段
- 修复首页主题切换错位、Wiki 版本默认选择、快照选择器、刷新弹窗引导等前端 UI 缺陷
- Slides/Workshop 页面接入模型选择机制，界面显式展示当前模型名称和选项说明
- 实现控制台日志分类过滤与任务结构化进度日志

**Non-Goals:**
- 不新增 LLM Provider 类型
- 不重构 Slides/Workshop 的内容生成逻辑（仅修复模型参数传递和 UI 显示）
- 不引入第三方日志框架（Serilog、NLog 等），基于 Microsoft.Extensions.Logging 实现
- 不修改生产 appsettings.json 中的日志级别

## Decisions

### 1. 提示词统一管理架构

**决定**: 将 `PromptTemplate` 扩展为支持 `Category`（任务类别）+ `SubCategory`（输出格式/Provider 个性）的层级结构，运行时由 `PromptMergeService` 按优先级拼装。

```
请求时 Prompt 拼装流程:
  IPromptMergeService.BuildPrompt(category, provider, outputFormat, variables)
    → 查询 PromptTemplates WHERE Category = 'wiki_page' AND SubCategory IN ('base', 'json_format', 'ollama_system')
    → 按 Priority 排序后拼接: [System片段] + [格式指令] + [任务Prompt正文] + [Provider个性尾缀]
    → 变量插值 {{variable}}
```

**替代方案**: 完全移除 Provider 的 system prompt，只保留任务 prompt。但 Gemini、Claude 等模型确实受益于角色设定，且 Ollama 小模型需要更强的格式约束。完全移除会降低小模型输出质量。

**数据库 Schema 扩展**:

```sql
ALTER TABLE prompt_templates ADD COLUMN category varchar(64) NOT NULL;   -- e.g. 'wiki_structure', 'wiki_page', 'ask', 'slides', 'workshop'
ALTER TABLE prompt_templates ADD COLUMN sub_category varchar(64);        -- e.g. 'base', 'json_format', 'ollama_system', 'xml_format'
ALTER TABLE prompt_templates ADD COLUMN priority int NOT NULL DEFAULT 0; -- 拼接顺序
ALTER TABLE prompt_templates ADD COLUMN applicable_providers text[];     -- NULL = all; '{ollama}' = only Ollama
```

**种子数据迁移策略**: 将 TaskPromptService 中的 7 个硬编码方法内容完整迁移至 `PromptSeedData`，不再保留占位符。

### 2. Provider System Prompt 处理

**决定**: 所有 ChatProvider 移除硬编码 system prompt/指令，改为接收已拼装完成的 `ChatRequest`（含可选的 `SystemPrompt` 字段）。

```csharp
// ChatRequest 扩展
public class ChatRequest
{
    public string Prompt { get; set; }           // 用户级 prompt（已拼装完整）
    public string? SystemPrompt { get; set; }    // 系统级 prompt（由 PromptMergeService 提供，可为 null）
    public string Provider { get; set; }
    public string Model { get; set; }
    public ChatParameters? Parameters { get; set; }
}
```

各 Provider 修改:
- **OllamaChatProvider**: 移除硬编码 system message，改为读取 `request.SystemPrompt`（若不为空则作为 `role: "system"` 发送）
- **ChatController**: 移除 `systemPrompt` 局部变量拼接，改为通过 `PromptMergeService` 获取
- **CodeSummaryService**: 改为通过 `PromptMergeService.BuildPrompt("code_summary", ...)` 获取

**风险**: 小模型（如 gemma4:e2b）失去强制 XML 格式指令后，输出格式可能不稳定。→ 缓解：在 PromptTemplate 中为 Ollama 类 Provider 保留 `sub_category = 'format_enforcement'` 的格式强化片段，但格式要求与任务实际输出格式一致（JSON 任务发 JSON 格式要求，XML 任务发 XML 格式要求）。

### 3. 前端 UI 修复方案

**主题切换错位**: 当前 `ThemeToggle` 在首页 header 中右对齐，但在 Wiki 页面 header (`h-12`) 中也被使用。检查 header 的 flex 布局，确保 `ThemeToggle` 使用一致的 `ml-auto` 定位。

**Wiki 版本选择**: 
- 默认选择：修改 `loadInitialData` 中的版本选择逻辑，按 `created_at DESC` 排序取第一个
- 记忆功能：在 `VersionSwitcher` 组件的 `onVersionChange` 回调中将 `wikiVersionId` 写入 `localStorage`，key 为 `heimdall:lastWikiVersion:{repositoryId}`
- 页面加载时优先读取 `localStorage` 中的版本 ID，若该版本仍存在则选择，否则回退到最新版本

**快照选择器**: 当前 VersionSwitcher 中快照部分标记为只读列表，需将其改为可选列表，`onVersionChange` 需同时支持选择仓库快照（传入 `repositoryVersionId`）。

**刷新弹窗引导**: 
- 为"刷新策略"添加 Tooltip 说明："最新版本：拉取远程仓库最新代码后重新生成 Wiki；当前版本：基于已拉取的快照重新生成"
- 为"生成档位"添加 Tooltip 说明："完整：生成全面的代码分析、架构图和模块文档；简洁：仅生成核心文件和入口点文档，速度更快"
- 将 Provider/Model 选择器从硬编码 `<select>` 替换为 `UserSelector` 组件

### 4. Slides/Workshop 模型参数修复

**决定**: 后端 Slides/Workshop 控制器当前从请求体读取 `model` 字段但未有效传递至 Provider。需统一后端 Task 创建逻辑和前端参数传递。

**后端修复**:
- `SlidesTaskService` 和 `WorkshopTaskService` 在创建 LLM 请求时需显式设置 `request.Model`
- 若请求未提供 model，从系统配置 `HEIMDALL_DEFAULT_MODEL` 或 Provider 默认模型读取，而非留空

**前端修复**:
- Slides/Workshop 页面从 URL 参数读取 provider/model 并显式展示在页面顶栏
- 若 URL 参数中无 provider/model，在加载前弹窗让用户选择（复用 `ModelSelectionModal` 或 `UserSelector`）
- 添加"当前模型：xxx"的标签展示

### 5. 日志分类与过滤

**决定**: 通过自定义 `ILoggerProvider` 实现日志分类过滤，运行时通过 API 端点切换 SQL 日志可见性。

```
IStructuredLogger 接口:
  - LogTaskProgress(taskId, step, currentPage, totalPages, message)
  - LogLlmCall(taskId, provider, model, promptLength, latencyMs)
  - LogError(taskId, context, exception)

LogCategoryFilter (单例):
  - bool ShowSqlCommands { get; set; }  // 默认 false，通过 API 切换
  - bool ShowEfCore { get; set; }       // 默认 false

API 端点:
  GET  /api/admin/logging/status     → 返回当前过滤状态
  POST /api/admin/logging/filter     → 设置过滤选项 { showSql: true/false, showEfCore: true/false }
```

**SQL 日志控制**: 在 `Program.cs` 中通过 `builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None)` 默认关闭 SQL 日志，由 `LogCategoryFilter` 运行时动态切换。开发环境可在 `appsettings.Development.json` 中保持 `Information` 级别，运行时通过 API 关闭。

**任务进度日志**: `WikiTaskService` 在关键步骤（仓库准备→结构规划→逐页生成→弱页重生→完成）输出结构化日志，包含当前步骤、页码进度、耗时、LLM 调用参数等。

## Risks / Trade-offs

- **[数据迁移风险]** PromptTemplate 种子数据从占位符替换为完整内容 → 若已部署实例有自定义 PromptTemplate 修改，迁移需保守（INSERT ON CONFLICT DO NOTHING，仅补充 `IsSystem=true` 的缺失模板）
- **[小模型格式退化]** 移除 Ollama 的强制 XML system prompt 后，小模型 JSON 格式合规率可能下降 → 通过 PromptTemplate 中 `sub_category = 'format_enforcement'` 片段按任务输出格式动态注入格式要求来缓解
- **[日志性能]** 结构化进度日志在大量 LLM 调用时可能产生 I/O 开销 → 进度日志使用 `ILogger.IsEnabled(LogLevel.Information)` 守卫，且避免字符串插值分配
- **[向后兼容]** ChatRequest 新增 `SystemPrompt` 字段 → 所有 Provider 需同步更新，建议一次性全量修改并编译验证

## Migration Plan

1. **数据库迁移**: 新增 `category`、`sub_category`、`priority`、`applicable_providers` 列到 `prompt_templates` 表，生成 EF Core 增量迁移
2. **种子数据**: 将 `TaskPromptService` 的 7 个硬编码方法、`ChatController` 的 system prompt、`CodeSummaryService` 的 3 个模板、`OllamaChatProvider` 的系统角色设定分别录入 `PromptSeedData`，分配 category/sub_category
3. **Provider 修改**: 5 个 Provider 同步更新以支持 `SystemPrompt` 字段（Ollama 发送 system role，其余用 `SystemPrompt` + `Prompt` 拼装），编译验证
4. **前端部署**: 渐进式修复，逐页验证
5. **日志**: 运行时 API 切换，无需停机
6. **回滚**: 保留 Provider 和 TaskPromptService 中的旧代码路径（用 `#if V5` 或配置开关），若 PromptTemplate 查询失败则回退到硬编码

## Open Questions

1. `applicable_providers` 为 NULL 时表示"所有 Provider"，但 Google Gemini API 不支持 system role —— 是否需要在 Provider 层做能力声明（如 `bool SupportsSystemRole`），当 Provider 不支持 system role 时自动将 system prompt 合并到 user prompt 前？
2. PromptTemplate 的版本历史是否需要支持回滚后的"重新激活"（即回滚不是创建新版本，而是切换 `IsActive`）？当前 `PromptManagementService.RollbackAsync` 的实现是创建新版本。
3. 日志过滤 API 是否需要身份验证？建议复用 Admin 控制器的认证中间件。

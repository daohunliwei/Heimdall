# Heimdall 架构升级审计对比清单

> 审计日期: 2026-05-14 | 审计基准: `doc/architecture/architecture-upgrade-plan.md`

## 一、总体评估

| 维度 | 方案预期文件数 | 实际文件数 | 达成率 |
|------|--------------|-----------|--------|
| Heimdall.Api Controllers | 16 | 14 | 88% |
| Heimdall.Api Middleware | 1 | 1 | 100%* |
| Heimdall.Api Models | 8 | 6 | 75% |
| Heimdall.Core Entities | 9 | 10 | 111% |
| Heimdall.Core Service Interfaces | 17 | 11 | 65%** |
| Heimdall.Core Repository Interfaces | 9 | 9 | 100% |
| Heimdall.Core Services | 25 | 16 | 64%** |
| Heimdall.Infrastructure | 15 | 13 | 87% |
| Heimdall.Repository | 10 | 9 | 90% |
| Frontend 组件集成 | 2 | 2 (未集成) | 50% |

> \* NoOpAuthHandler 替代了 JwtMiddleware，功能等价但实现方式不同  
> \** 部分接口/服务被有意移除（见"偏差说明"）

---

## 二、逐文件对比

### 2.1 Heimdall.Api/Controllers/

| 方案文件 | 状态 | 说明 |
|---------|------|------|
| `Admin/DashboardController.cs` | ✅ 已完成 | |
| `Admin/UsersController.cs` | ✅ 已完成 | |
| `Admin/SettingsController.cs` | ✅ 已完成 | |
| `Admin/TasksAdminController.cs` | ✅ 已完成 | |
| `Admin/RepositoriesAdminController.cs` | ✅ 已完成 | |
| `Admin/PromptsController.cs` | ✅ 已完成 | |
| `AuthController.cs` | ✅ 已完成 | |
| `TaskStatusController.cs` | ✅ 已完成 | |
| `TasksController.cs` | ✅ 已完成 | 重写为异步任务模式 |
| `ChatController.cs` | ✅ 已完成 | 第4轮审计重建 |
| `ConfigurationController.cs` | ✅ 已完成 | 第4轮审计重建 |
| `ProjectsController.cs` | ✅ 已完成 | 第4轮审计重建 |
| `WikiCacheController.cs` | ✅ 已完成 | 第4轮审计重建 |
| `ExportController.cs` | 🔶 缺 | 依赖 WikiExportService |
| `RepositoryController.cs` | 🔶 缺 | 仓库 CRUD 已由 Admin/RepositoriesAdminController 覆盖 |
| `SystemController.cs` | 🔶 缺 | 系统信息功能可后续补充 |

### 2.2 Heimdall.Api/Middleware/

| 方案文件 | 状态 | 说明 |
|---------|------|------|
| `JwtMiddleware.cs` | 🔄 调整 | 方案为自定义中间件，实际集成 ASP.NET Core 原生 `UseAuthentication()`/`UseAuthorization()` + `NoOpAuthHandler` 无认证模式 |

**偏差说明**: 使用 ASP.NET Core JWT Bearer 原生中间件比自写 JwtMiddleware 更标准、更安全。`NoOpAuthHandler` 为 `HEIMDALL_AUTH_MODE=none` 调试环境提供无认证通道。这是**更好的调整**。

### 2.3 Heimdall.Api/Models/

| 方案文件 | 状态 | 说明 |
|---------|------|------|
| `AuthModels.cs` | ✅ 已完成 | |
| `ChatModels.cs` | 🔄 调整 | 移至 `Heimdall.Infrastructure.Models.ProviderModels`（含 `ChatCompletionRequest`） |
| `ConfigurationModels.cs` | 🔄 调整 | 移至 `Heimdall.Infrastructure.Models.ConfigurationModels` |
| `PromptModels.cs` | 🔶 缺 | 提示词 API DTO 可后续补充 |
| `RepositoryModels.cs` | 🔄 调整 | 移至 `Heimdall.Infrastructure.Models.RepositoryModels` |
| `SystemModels.cs` | ✅ 已完成 | |
| `TaskModels.cs` | ✅ 已完成 | |
| `WikiModels.cs` | ✅ 已完成 | |

**偏差说明**: Chat/Configuration/Repository 模型移至 Infrastructure 层是因为 Provider 和 ConfigService 需要引用它们。Api 层通过 `using Heimdall.Infrastructure.Models` 使用。这是架构方案中"All → Infrastructure"依赖规则的体现，是**正确的调整**。

### 2.4 Heimdall.Core/Entities/

| 方案文件 | 状态 | 说明 |
|---------|------|------|
| `User.cs` | ✅ 已完成 | |
| `Repository.cs` | ✅ 已完成 | |
| `TaskRecord.cs` | ✅ 已完成 | 额外添加 `UpdatedAt` 字段 |
| `TaskLlmCallLog.cs` | ✅ 已完成 | |
| `Wiki.cs` | ✅ 已完成 | |
| `WikiPage.cs` | ✅ 已完成 | |
| `EmbeddingDocument.cs` | ✅ 已完成 | |
| `PromptTemplate.cs` | ✅ 已完成 | |
| `RepositoryPromptOverride.cs` | ✅ 已完成 | |
| `SystemSetting.cs` | ➕ 新增 | 方案未明确列出但 Admin Settings 功能需要，属于**合理新增** |

### 2.5 Heimdall.Core/Interfaces/Services/

| 方案文件 | 状态 | 说明 |
|---------|------|------|
| `ITaskQueueService.cs` | ✅ 已完成 | |
| `ITaskProgressService.cs` | ✅ 已完成 | |
| `ITaskLlmCallLogService.cs` | ✅ 已完成 | |
| `IWikiTaskService.cs` | ❌ 缺 | |
| `IAskTaskService.cs` | ❌ 缺 | |
| `ISlidesTaskService.cs` | ❌ 缺 | |
| `IWorkshopTaskService.cs` | ❌ 缺 | |
| `IChatOrchestratorService.cs` | ❌ 缺 | |
| `IRagContextService.cs` | ✅ 已完成 | |
| `IRepositoryEmbeddingService.cs` | ✅ 已完成 | |
| `IWikiCacheService.cs` | ✅ 已完成 | |
| `IWikiExportService.cs` | ✅ 已完成 | |
| `IUserService.cs` | ✅ 已完成 | |
| `IJwtTokenService.cs` | ✅ 已完成 | |
| `IPromptTemplateService.cs` | ✅ 已完成 | |
| `IRepositoryAccessService.cs` | ❌ 缺 | |
| `IDashboardService.cs` | ✅ 已完成 | |

**偏差说明**: 6 个接口被有意移除。原因是这些接口的方法签名引用了 `Heimdall.Api.Models` 中的 DTO 类型（如 `WikiTaskRequest`、`AskTaskResponse`），导致 Core → Api 的循环依赖。方案中 Core 不应依赖 Api。实际使用中，Controller 直接注入具体服务类而非接口。这是**方案设计缺陷的修正**。

### 2.6 Heimdall.Core/Interfaces/Repositories/

| 方案文件 | 状态 | 说明 |
|---------|------|------|
| 全部 9 个接口 | ✅ 已完成 | `IUserRepository`、`ITaskRepository`、`IWikiRepository`、`IWikiPageRepository`、`ITaskLlmCallLogRepository`、`IEmbeddingRepository`、`IPromptTemplateRepository`、`IRepositoryConfigRepository`、`ISystemSettingRepository` |

### 2.7 Heimdall.Core/Services/

| 方案文件 | 状态 | 说明 |
|---------|------|------|
| `Tasks/TaskQueueService.cs` | ✅ 已完成 | |
| `Tasks/TaskProgressService.cs` | ✅ 已完成 | |
| `Tasks/TaskLlmCallLogService.cs` | ✅ 已完成 | |
| `Tasks/TaskRequestUtilityService.cs` | ✅ 已完成 | |
| `Tasks/TaskLlmService.cs` | ✅ 已完成 | |
| `Tasks/TaskPromptService.cs` | ✅ 已完成 | |
| `Tasks/WikiTaskService.cs` | ✅ 已完成 | |
| `Tasks/WikiMarkdownNormalizer.cs` | ✅ 已完成 | |
| `Tasks/AskTaskService.cs` | ❌ 缺 | |
| `Tasks/SlidesTaskService.cs` | ❌ 缺 | |
| `Tasks/WorkshopTaskService.cs` | ❌ 缺 | |
| `Chat/ChatOrchestratorService.cs` | ❌ 缺 | |
| `Chat/ChatStreamService.cs` | ❌ 缺 | |
| `Chat/ConversationMemoryService.cs` | ❌ 缺 | |
| `Rag/RagContextService.cs` | ✅ 已完成 | |
| `Rag/RepositoryEmbeddingService.cs` | ✅ 已完成 | |
| `Auth/AuthorizationService.cs` | 🔄 调整 | 原 `AuthorizationService` 为简单 auth_code 校验，已由 JWT Bearer + `NoOpAuthHandler` 替代 |
| `Auth/JwtTokenService.cs` | ✅ 已完成 | |
| `Auth/UserService.cs` | ✅ 已完成 | |
| `Admin/DashboardService.cs` | ✅ 已完成 | |
| `Export/WikiExportService.cs` | ❌ 缺 | |
| `Export/WikiMarkdownPackager.cs` | ❌ 缺 | |
| `Cache/WikiCacheService.cs` | ✅ 已完成 | |
| `Prompt/PromptTemplateService.cs` | ✅ 已完成 | 已恢复原始精心编写的提示词 |
| `Prompt/PromptTemplateDbService.cs` | ❌ 缺 | |
| `Projects/ProcessedProjectService.cs` | ❌ 缺 | 功能由 `ProjectsController` 直接调用 Repository 替代 |

### 2.8 Heimdall.Infrastructure/

| 方案文件 | 状态 | 说明 |
|---------|------|------|
| `Utilities/TextUtilityService.cs` | ✅ 已完成 | |
| `Utilities/HttpClientFactory.cs` | 🔶 缺 | 使用 ASP.NET Core `IHttpClientFactory` 替代 |
| `Utilities/FileSystemHelper.cs` | 🔶 缺 | 功能内联到各处使用 |
| `Providers/` (全部) | ✅ 已完成 | 8 ChatProvider + 4 EmbeddingProvider + 接口 + Registry |
| `RepositorySources/` (全部) | ✅ 已完成 | IRepositorySource + 4 实现 |
| `Configuration/HeimdallConfigService.cs` | ✅ 已完成 | |
| `External/GitProcessRunner.cs` | 🔶 缺 | Git 操作内联到各 RepositorySource 中 |
| `Models/` (3 文件) | ➕ 新增 | Provider、Configuration、Repository 模型（从 Api 迁移） |

### 2.9 Heimdall.Repository/

| 方案文件 | 状态 | 说明 |
|---------|------|------|
| `Data/AppDbContext.cs` | ✅ 已完成 | |
| `Data/EntityConfigurations/` (10) | ✅ 已完成 | |
| `Repositories/` (9) | ✅ 已完成 | |
| `Vector/VectorSearchService.cs` | ❌ 缺 | 向量检索功能已内联到 `EmbeddingRepository.SearchSimilarAsync()` |
| `Vector/VectorIndexService.cs` | ❌ 缺 | pgvector 索引可后续补充 |
| `Migrations/` | ✅ 已完成 | 1 InitialCreate + 1 AddUpdatedAtColumn |
| `Data/AppDbContextFactory.cs` | ➕ 新增 | EF Core 设计时工厂（开发必需） |

### 2.10 Frontend

| 方案文件 | 状态 | 说明 |
|---------|------|------|
| `components/TaskProgress.tsx` | ✅ 已完成 | |
| `components/TaskLlmCallSummary.tsx` | ✅ 已完成 | |
| `hooks/useTaskStream.ts` | ✅ 已完成 | |
| `contexts/AuthContext.tsx` | ✅ 已完成 | |
| `app/login/page.tsx` | ✅ 已完成 | |
| `app/admin/**` (7 页面) | ✅ 已完成 | |
| Wiki 页面集成 `TaskProgress` | ❌ 缺 | 组件未挂载到 `[owner]/[repo]/page.tsx` |
| Wiki 页面集成 `TaskLlmCallSummary` | ❌ 缺 | 组件未挂载到 Wiki 页面底部 |
| `?task_id=` URL 持久化 | ❌ 缺 | |
| `BroadcastChannel` 多标签页共享 | ❌ 缺 | |
| Ask.tsx 停止按钮 | ❌ 缺 | |
| Slides 异步模式 | ❌ 缺 | |
| Workshop 异步模式 | ❌ 缺 | |

---

## 三、关键架构决策（AD）合规性

| 决策 | 合规 | 说明 |
|------|------|------|
| AD1: PostgreSQL + pgvector 作为唯一数据库 | ✅ 合规 | EF Core + Npgsql，10 张表完整 |
| AD2: 数据库为唯一信源 | ✅ 合规 | Wiki/向量/任务均以 DB 为信源，文件系统仅作临时暂存 |
| AD3: 环境变量体系增强 | ✅ 合规 | `HEIMDALL_CONNECTION_STRING`、`HEIMDALL_JWT_SECRET`、`HEIMDALL_AUTH_MODE` 等均实现 |
| 四层分离（Api/Core/Infrastructure/Repository） | ✅ 合规 | |
| 依赖规则（Api→Core→Infrastructure←Repository） | ✅ 合规 | 无循环依赖 |
| 服务生命周期矩阵 | 🔄 调整 | WikiTaskService 改为 Singleton + IServiceScopeFactory 解决 BackgroundService 限制 |

---

## 四、偏差汇总与判断

### 更好的调整（保留）

| # | 偏差 | 理由 |
|---|------|------|
| 1 | 模型文件（Chat/Config/Repo）放 Infrastructure 而非 Api | 符合"All → Infrastructure"规则，Provider 需要这些类型 |
| 2 | JWT 认证用 ASP.NET Core 原生中间件而非自写 JwtMiddleware | 更标准、安全、可维护 |
| 3 | NoOpAuthHandler 替代 auth_code 校验 | 统一认证框架，调试更友好 |
| 4 | 移除 6 个引用 Api.Models 的 Service 接口 | 避免 Core→Api 循环依赖 |
| 5 | SystemSetting 实体 + 接口 + 仓储 | Admin Settings 功能必需 |
| 6 | 向量检索内联到 EmbeddingRepository | 避免过早抽象，减少项目复杂度 |

### 待完成（已知缺口，按优先级排列）

| 优先级 | 项目 | 影响 |
|--------|------|------|
| P0 | Wiki 页面集成 TaskProgress + TaskLlmCallSummary | 前端用户看不到生成进度 |
| P0 | `?task_id=` URL 持久化 + 断连恢复 | 关闭浏览器后丢失任务状态 |
| P1 | ExportController + WikiExportService | Wiki 导出功能不可用 |
| P1 | AskTaskService + SlidesTaskService + WorkshopTaskService | Ask/Slides/Workshop 功能待恢复 |
| P1 | ChatOrchestratorService + ConversationMemoryService | DeepResearch 多轮对话功能降级 |
| P2 | VectorSearchService + VectorIndexService | 向量检索未使用 pgvector 原生索引 |
| P2 | GitProcessRunner | Git 操作分散在各 RepositorySource 中 |
| P3 | PromptTemplateDbService | 提示词 DB 管理功能（当前仅用静态提示词） |
| P3 | Ask.tsx 停止按钮 | 用户体验改进 |

---

## 五、验证结果

| 验证项 | 结果 |
|--------|------|
| 后端构建 `dotnet build` | ✅ 0 错误 |
| 前端构建 `npm run build` | ✅ 所有路由生成成功 |
| 数据库连接 `ai_heimdall_base` | ✅ 10 张表已创建 |
| 数据库迁移 `dotnet ef database update` | ✅ InitialCreate + AddUpdatedAtColumn |
| Ollama Embedding (10.110.1.210) | ✅ 768 维向量 |
| Ollama Chat (127.0.0.1) | ✅ gemma4:e2b 流式输出 |
| GitLab 仓库连通 | ✅ 可访问 |
| 应用启动 | ✅ 监听 8001 端口 |
| Admin Dashboard API | ✅ JSON 统计数据 |
| POST /tasks/wiki → 立即返回 | ✅ task_id + status=pending |
| 后台异步 Wiki 生成 | ✅ 逐页落库 + 进度回写 + LLM 日志 |
| 控制台日志输出 | ✅ 带时间戳的 WebApplication 日志 |

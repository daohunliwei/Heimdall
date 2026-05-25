# AGENTS.md

## 仓库目标

本仓库用于构建 Heimdall，把代码仓库转换为中文 Wiki、问答内容、演示文稿与训练营材料。

当前官方技术栈：

- 后端：C# / ASP.NET Core / `.NET 10`
- 前端：Next.js 16 (App Router)
- 数据库：PostgreSQL + pgvector
- ORM：SqlSugar（CodeFirst 自动同步，无迁移文件）
- AI 抽象：Microsoft.Extensions.AI（MEAI）`IChatClient`

历史 Python 逻辑已完全移除，仓库不再包含任何 Python 运行链路与源码目录。

## 架构

```
Heimdall.Api (API 层)         →  控制器、DTO、中间件、Mappings
    ↓
Heimdall.Core (业务层)        →  实体、业务接口与实现、领域模型
    ↓
Heimdall.Repository (数据层)  →  SqlSugar ORM、仓储实现
    ↘              ↙
Heimdall.Infrastructure (工具层) →  MEAI IChatClient Provider、配置、仓库源、BM25 搜索、文本工具
```

依赖规则：Api → Core → Repository；全部 → Infrastructure。Core 不依赖 Api。层间通过接口通信，DI 注入。

### V9 关键变更

- **ORM**：EF Core → SqlSugar。`ISqlSugarClient` (Singleton) 替代 `AppDbContext` (Scoped)。无 DbContext、无 EntityConfigurations、无迁移文件。
- **Provider**：自研 `IChatProvider` 接口 → MEAI `IChatClient`。`ChatClientFactory` + Keyed DI 替代 `ProviderRegistry`。OpenAI/OpenRouter/DashScope/DeepSeek/Azure 统一走 `OpenAiCompatibleClientFactory`；Ollama/Gemini/MiniMax 为自定义 `IChatClient` 适配器（`Infrastructure/Providers/CustomBackends/`）。
- **流式**：`IChatClient.GetStreamingResponseAsync()` 真流式 SSE，Chat 和 Ask 端点均已升级。
- **CodeFirst**：启动时 `CodeFirstSyncService` 扫描 `Core.Entities` 自动同步表结构。
- **指标**：`ILlmObservabilityService.RecordCallAsync` 接收 MEAI `UsageDetails`，支持 `IsStreaming`/`FirstTokenLatencyMs`。

## 目录职责

- `backend/Heimdall.Api`：C# API 入口——控制器、中间件、DTO 模型、Mappings、`Program.cs`
- `backend/Heimdall.Core`：业务逻辑——`Entities/`（`[SugarTable]` 实体）、`Interfaces/`、`Services/`、`Models/`。`Services/Repository/` 含 `CodeIndexService`（Tree-sitter AST 代码索引）和 `CodeStructureIndexService`；`Services/Tasks/` 含 `AgentOrchestratorService`（大仓库子代理协调）
- `backend/Heimdall.Infrastructure`：工具层——`Providers/`（MEAI IChatClient 适配 + `ChatClientFactory` + `OpenAiCompatibleClientFactory` + `BedrockClientFactory`）、`Providers/CustomBackends/`（OllamaChatClient / GeminiChatClient / MiniMaxChatClient）、`Search/`（BM25 搜索引擎）、`RepositorySources/`（仓库源）、`Configuration/`、`Utilities/`
- `backend/Heimdall.Repository`：数据层——`Repositories/`（注入 `ISqlSugarClient`）
- `frontend/src/app`：Next.js 页面与 API 代理路由（含 `/api/tasks/ask/stream` SSE 代理）
- `frontend/src/components`：前端组件（`Ask.tsx` 含流式 SSE 支持）
- `frontend/src/contexts`：Auth/Language/Repository 上下文
- `frontend/src/hooks`：自定义 Hook（useTaskStream、useProcessedProjects、useArtifactVersionContext）
- `frontend/src/messages`：中文界面文案
- `docs/architecture`：系统架构设计文档（[`architecture.md`](docs/architecture/architecture.md)）

## 修改原则

- 所有新增文档、注释、说明文字必须使用中文
- C# 运行时固定为 `.NET 10`
- 优先修改 C# 后端与 Next.js 前端，不要引入 Python 业务代码
- 新服务需在 `Program.cs` 中注册 DI

## 常见任务入口

### 新增后端接口或业务能力

优先修改：

- `backend/Heimdall.Api/Program.cs` — DI 注册、中间件管道
- `backend/Heimdall.Api/Controllers/` — API 控制器
- `backend/Heimdall.Core/Services/` — 业务服务实现
- `backend/Heimdall.Core/Interfaces/` — 接口定义
- `backend/Heimdall.Core/Entities/` — 领域实体（`[SugarTable]` / `[SugarColumn]`）
- `backend/Heimdall.Repository/Repositories/` — 数据访问实现（注入 `ISqlSugarClient`）
- `backend/Heimdall.Api/Models/` — API DTO
- `backend/Heimdall.Api/config/` — JSON 配置文件

如果只是前端转发，可同步查看：

- `frontend/src/app/api/**/route.ts`
- `frontend/next.config.ts`

### 修改首页与仓库页面

优先修改：

- `frontend/src/app/page.tsx`
- `frontend/src/app/repositories/[repositoryId]/page.tsx`
- `frontend/src/components/*`
- `frontend/src/components/RefreshPanel.tsx`
- `frontend/src/components/VersionSwitcher.tsx`

### 修改缓存与项目列表

优先修改：

- `backend/Heimdall.Api/Controllers/ProjectsController.cs`
- `backend/Heimdall.Api/Controllers/WikiCacheController.cs`
- `frontend/src/app/api/wiki/projects/route.ts`
- `frontend/src/components/ProcessedProjects.tsx`

### 修改 Wiki 生成管线与代码索引

优先修改：

- `backend/Heimdall.Core/Services/Tasks/WikiTaskService.cs` — 8 阶段管线编排
- `backend/Heimdall.Core/Services/Repository/CodeIndexService.cs` — Tree-sitter AST 代码索引
- `backend/Heimdall.Infrastructure/Search/Bm25SearchService.cs` — BM25 精确匹配引擎
- `backend/Heimdall.Core/Services/Tasks/TaskPromptService.cs` — 提示词构建
- `backend/Heimdall.Core/Services/Prompt/PromptSeedData.cs` — 提示词模板播种
- `backend/Heimdall.Core/Entities/CodeIndexEntry.cs` — 代码索引实体
- `backend/Heimdall.Repository/Repositories/CodeIndexRepository.cs` — 索引持久化

### 修改问答、演示文稿、训练营能力

优先修改：

- `backend/Heimdall.Api/Controllers/ChatController.cs` — 流式 SSE Chat
- `backend/Heimdall.Api/Controllers/TasksController.cs` — Ask/AskStream 端点
- `backend/Heimdall.Core/Services/Tasks/AskTaskService.cs` — Ask + AskStreamingAsync
- `backend/Heimdall.Core/Services/Tasks/SlidesTaskService.cs`
- `backend/Heimdall.Core/Services/Tasks/WorkshopTaskService.cs`
- `backend/Heimdall.Core/Services/Tasks/TaskPromptService.cs`
- `backend/Heimdall.Core/Services/Tasks/VersionedKnowledgeService.cs`
- `backend/Heimdall.Core/Services/Prompt/PromptTemplateService.cs`
- `backend/Heimdall.Infrastructure/Providers/` — MEAI Provider 工厂与适配器
- `frontend/src/components/Ask.tsx` — 流式 SSE 问答界面
- `frontend/src/app/repositories/[repositoryId]/slides/page.tsx`
- `frontend/src/app/repositories/[repositoryId]/workshop/page.tsx`

### 修改数据库

优先修改：

- `backend/Heimdall.Core/Entities/` — 实体定义（`[SugarTable]` + `[SugarColumn]` 属性）
- 启动时 CodeFirst 自动同步（由 `HEIMDALL_CODEFIRST_AUTOSYNC` 环境变量控制）

## 运行方式

### 环境变量

所有调试凭据统一存放在 `scripts/dev.env`（已 gitignore），启动脚本自动加载：

```bash
cp scripts/dev.env.example scripts/dev.env
# 编辑 scripts/dev.env，填入真实值
```

### 本地开发

```bash
# macOS / Linux — 一键启动
bash scripts/dev.sh

# Windows — 一键启动
.\scripts\dev-start.ps1

# 仅后端
bash scripts/dev.sh --backend-only

# 仅前端
.\scripts\dev.ps1 -FrontendOnly

# 预览启动命令（不实际执行）
.\scripts\dev.ps1 -DryRun
```

### AI 工具自动调试

```bash
# 加载环境变量后直接启动（命令行无密码明文）
source scripts/dev.env && dotnet run --no-launch-profile --project backend/Heimdall.Api/Heimdall.Api.csproj
```

### 关键环境变量

- `HEIMDALL_CONNECTION_STRING` — PostgreSQL 连接字符串
- `HEIMDALL_AUTH_MODE` — `none` 或 `jwt`
- `HEIMDALL_JWT_SECRET` — JWT 签名密钥
- `HEIMDALL_CODEFIRST_AUTOSYNC` — 启动时是否自动同步表结构（bool）
- `HEIMDALL_DEFAULT_PROVIDER` — 默认 LLM Provider
- `HEIMDALL_OLLAMA_CHAT_HOST` — Ollama Chat 地址
- `SERVER_BASE_URL` — 前端代理的后端地址
- Provider 密钥：`OPENAI_API_KEY`、`GOOGLE_API_KEY`、`DEEPSEEK_API_KEY` 等

完整列表见 `scripts/dev.env.example`。

## 验证方式

### 前端

```bash
npm run lint
npm run build
```

### 后端

```bash
dotnet build backend/Heimdall.Api/Heimdall.Api.csproj
```

### 联调重点

- 首页输入仓库 URL → 调用 `POST /api/repositories/import` → 跳转 `/repositories/{repositoryId}`
- 仓库页能否加载 Wiki 版本列表、页面树与页面内容
- `POST /api/repositories/{repositoryId}/wiki/refresh` 是否立即返回 task_id
- 后台异步 Wiki 生成管线：仓库准备 → 代码索引 → 代码理解 → 结构规划 → 页面生成（BM25+pgvector 混合检索注入）→ 质量审查 → 渲染后处理 → 持久化
- 流式 Chat（SSE）和流式 Ask（`POST /tasks/ask/stream`）是否正常
- 版本切换器能否切换到指定 Wiki 版本并正确加载页面
- Slides / Workshop 页面是否透传 `repositoryVersionId` + `wikiVersionId`
- 管理后台仪表盘、用户管理、任务监控、Prompt 模板是否正常

## 禁止事项

- 不要在主目录重新新增 Python 业务代码
- 不要引入新的多语言文档与多语言界面资源
- 不要把 .NET 版本改成非 `.NET 10`
- 不要引入任何 Python 运行链路（包括脚本、服务、镜像依赖与 CI 步骤）
- 不要删除数据库已有表（通过 SqlSugar CodeFirst 增量同步）
- 不要创建 Core → Api 方向的项目引用
- 不要引入 `Microsoft.EntityFrameworkCore.*` 包（已迁移到 SqlSugar）
- 不要引入自研 `IChatProvider` 实现（已迁移到 MEAI `IChatClient`）

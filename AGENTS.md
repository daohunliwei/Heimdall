# AGENTS.md

## 仓库目标

本仓库用于构建 Heimdall，把代码仓库转换为中文 Wiki、问答内容、演示文稿与训练营材料。

当前官方技术栈：

- 后端：C# / ASP.NET Core / `.NET 10`
- 前端：Next.js 16 (App Router)
- 数据库：PostgreSQL + pgvector

历史 Python 逻辑已完全移除，仓库不再包含任何 Python 运行链路与源码目录。

## 架构

```
Heimdall.Api (API 层)         →  控制器、DTO、中间件、Mappings
    ↓
Heimdall.Core (业务层)        →  实体、业务接口与实现、领域模型
    ↓
Heimdall.Repository (数据层)  →  EF Core、仓储、迁移、向量查询
    ↘              ↙
Heimdall.Infrastructure (工具层) →  Provider、配置、仓库源、文本工具
```

依赖规则：Api → Core → Repository；全部 → Infrastructure。Core 不依赖 Api。层间通过接口通信，DI 注入。

## 目录职责

- `backend/Heimdall.Api`：C# API 入口——控制器、中间件、DTO 模型、Mappings、`Program.cs`
- `backend/Heimdall.Core`：业务逻辑——`Entities/`、`Interfaces/`、`Services/`、`Models/`
- `backend/Heimdall.Infrastructure`：工具层——`Providers/`（LLM 适配）、`RepositorySources/`（仓库源）、`Configuration/`、`Utilities/`
- `backend/Heimdall.Repository`：数据层——`Data/`（AppDbContext）、`EntityConfigurations/`、`Repositories/`、`Migrations/`
- `frontend/src/app`：Next.js 页面与 API 代理路由
- `frontend/src/components`：前端组件
- `frontend/src/contexts`：Auth/Language 上下文
- `frontend/src/hooks`：自定义 Hook（useTaskStream、useProcessedProjects）
- `frontend/src/messages`：中文界面文案
- `doc/architecture`：架构升级方案与审计清单

## 修改原则

- 所有新增文档、注释、说明文字必须使用中文
- C# 运行时固定为 `.NET 10`
- 优先修改 C# 后端与 Next.js 前端，不要引入 Python 业务代码
- 数据层变更需生成 EF Core 迁移
- 新服务需在 `Program.cs` 中注册 DI

## 常见任务入口

### 新增后端接口或业务能力

优先修改：

- `backend/Heimdall.Api/Program.cs` — DI 注册、中间件管道
- `backend/Heimdall.Api/Controllers/` — API 控制器
- `backend/Heimdall.Core/Services/` — 业务服务实现
- `backend/Heimdall.Core/Interfaces/` — 接口定义
- `backend/Heimdall.Core/Entities/` — 领域实体
- `backend/Heimdall.Repository/Repositories/` — 数据访问实现
- `backend/Heimdall.Api/Models/` — API DTO
- `backend/Heimdall.Api/config/` — JSON 配置文件

如果只是前端转发，可同步查看：

- `frontend/src/app/api/**/route.ts`
- `frontend/next.config.ts`

### 修改首页与仓库页面

优先修改：

- `frontend/src/app/page.tsx`
- `frontend/src/app/[owner]/[repo]/page.tsx`
- `frontend/src/components/*`

### 修改缓存与项目列表

优先修改：

- `backend/Heimdall.Api/Controllers/ProjectsController.cs`
- `backend/Heimdall.Api/Controllers/WikiCacheController.cs`
- `frontend/src/app/api/wiki/projects/route.ts`
- `frontend/src/components/ProcessedProjects.tsx`

### 修改问答、演示文稿、训练营能力

优先修改：

- `backend/Heimdall.Api/Controllers/ChatController.cs`
- `backend/Heimdall.Core/Services/Tasks/TaskPromptService.cs`
- `backend/Heimdall.Core/Services/Prompt/PromptTemplateService.cs`
- `backend/Heimdall.Infrastructure/Providers/`
- `frontend/src/components/Ask.tsx`
- `frontend/src/app/[owner]/[repo]/slides/page.tsx`
- `frontend/src/app/[owner]/[repo]/workshop/page.tsx`

### 修改数据库

优先修改：

- `backend/Heimdall.Core/Entities/` — 实体定义
- `backend/Heimdall.Repository/Data/EntityConfigurations/` — Fluent API
- `backend/Heimdall.Repository/Data/AppDbContext.cs` — DbContext
- 修改后执行：`dotnet ef migrations add <Name>` → `dotnet ef database update`

## 运行方式

### 本地开发

后端：

```bash
dotnet run --project backend/Heimdall.Api/Heimdall.Api.csproj
```

前端：

```bash
cd frontend
npm install
npm run dev
```

一键启动：

```bash
cd frontend
npm run dev:all
```

### 关键环境变量

- `HEIMDALL_CONNECTION_STRING` — PostgreSQL + pgvector 连接字符串
- `HEIMDALL_AUTH_MODE` — `none` 或 `jwt`
- `HEIMDALL_JWT_SECRET` — JWT 签名密钥
- `HEIMDALL_JWT_EXPIRY_HOURS` — Token 过期小时数
- `HEIMDALL_REGISTRATION_OPEN` — 是否开放注册
- `HEIMDALL_DEFAULT_PROVIDER` — 默认 LLM Provider
- `HEIMDALL_EMBEDDER_TYPE` — 向量嵌入器类型
- `HEIMDALL_OLLAMA_CHAT_HOST` — Ollama Chat 地址
- `HEIMDALL_OLLAMA_EMBED_HOST` — Ollama Embedding 地址
- `SERVER_BASE_URL` — 前端代理的后端地址
- `HEIMDALL_DATA_DIR` — Wiki 缓存数据目录
- `HEIMDALL_STORAGE_DIR` — 仓库克隆暂存目录
- `HEIMDALL_HTTP_TIMEOUT_MINUTES` — HTTP 超时
- `HEIMDALL_WIKI_TASK_TIMEOUT_MINUTES` — Wiki 任务超时

Provider 密钥：`OPENAI_API_KEY`、`GOOGLE_API_KEY`、`OLLAMA_HOST` 等

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

### 数据库迁移

```bash
dotnet ef migrations add <Name> \
  --project backend/Heimdall.Repository \
  --startup-project backend/Heimdall.Api

dotnet ef database update \
  --project backend/Heimdall.Repository \
  --startup-project backend/Heimdall.Api
```

### 联调重点

- 首页能否进入仓库页面
- 项目列表能否加载与删除缓存
- Wiki 缓存能否读取、保存、清理
- `POST /tasks/wiki` 是否立即返回 task_id
- 后台异步 Wiki 生成是否逐页落库
- 问答流式输出是否正常
- 演示文稿与训练营页面是否能正常生成内容
- 管理后台仪表盘数据是否正确

## 禁止事项

- 不要在主目录重新新增 Python 业务代码
- 不要引入新的多语言文档与多语言界面资源
- 不要把 .NET 版本改成非 `.NET 10`
- 不要引入任何 Python 运行链路（包括脚本、服务、镜像依赖与 CI 步骤）
- 不要删除数据库已有表（使用增量迁移）
- 不要创建 Core → Api 方向的项目引用

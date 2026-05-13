# AGENTS.md

## 仓库目标

本仓库用于构建 Heimdall，把代码仓库转换为中文 Wiki、问答内容、演示文稿与训练营材料。

当前官方技术栈：

- 后端：C# / ASP.NET Core / `.NET 10`
- 前端：Next.js

历史 Python 逻辑已完全移除，仓库不再包含任何 Python 运行链路与源码目录。

## 目录职责

- `backend/Heimdall.Api`：C# 后端入口、配置、Provider、RAG、仓库访问与缓存逻辑
- `frontend/src/app`：Next.js 页面与 API 代理路由
- `frontend/src/components`：前端组件
- `frontend/src/contexts`：前端上下文
- `frontend/src/messages`：中文界面文案
- `.trae/specs/migrate-stack-to-csharp-nextjs`：本次改造规格与任务文档

## 修改原则

- 所有新增文档、注释、说明文字必须使用中文
- 主目录只允许存在 C# 与 Next.js 主逻辑
- C# 运行时固定为 `.NET 10`
- 优先修改 C# 后端与 Next.js 前端，不要引入新的 Python 业务代码或运行依赖

## 常见任务入口

### 新增后端接口或后端业务能力

优先修改：

- `backend/Heimdall.Api/Program.cs`
- `backend/Heimdall.Api/Services/Chat/*`
- `backend/Heimdall.Api/Services/Providers/*`
- `backend/Heimdall.Api/Services/Rag/*`
- `backend/Heimdall.Api/Services/Repository/*`
- `backend/Heimdall.Api/Services/Configuration/*`
- `backend/Heimdall.Api/Models/ApiModels.cs`
- `backend/Heimdall.Api/config/*.json`

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

- `backend/Heimdall.Api/Program.cs` 中的路由映射
- `backend/Heimdall.Api/Models/ApiModels.cs`
- `frontend/src/app/api/wiki/projects/route.ts`
- `frontend/src/components/ProcessedProjects.tsx`

### 修改问答、演示文稿、训练营能力

优先修改：

- `backend/Heimdall.Api/Program.cs` 中的 `/chat/completions/stream`
- `backend/Heimdall.Api/Services/Chat/ChatOrchestratorService.cs`
- `backend/Heimdall.Api/Services/Rag/*`
- `backend/Heimdall.Api/Services/Providers/*`
- `backend/Heimdall.Api/Services/Utility/PromptTemplateService.cs`
- `frontend/src/components/Ask.tsx`
- `frontend/src/app/[owner]/[repo]/slides/page.tsx`
- `frontend/src/app/[owner]/[repo]/workshop/page.tsx`

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

### 关键环境变量

- 品牌名称已切换为 Heimdall，环境变量统一使用 `HEIMDALL_*` 键名
- `SERVER_BASE_URL`
- `HEIMDALL_AUTH_MODE`
- `HEIMDALL_AUTH_CODE`
- `HEIMDALL_DATA_DIR`
- `HEIMDALL_DEFAULT_PROVIDER`
- `HEIMDALL_EMBEDDER_TYPE`
- `OPENAI_API_KEY`
- `OPENROUTER_API_KEY`
- `GOOGLE_API_KEY`
- `MINIMAX_API_KEY`
- `MINIMAX_BASE_URL`
- `DASHSCOPE_API_KEY`
- `AZURE_OPENAI_API_KEY`
- `AZURE_OPENAI_ENDPOINT`
- `AZURE_OPENAI_VERSION`
- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_REGION`
- `OLLAMA_HOST`

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

- 首页能否进入仓库页面
- 项目列表能否加载与删除缓存
- Wiki 缓存能否读取、保存、清理
- 问答流式输出是否正常
- 演示文稿与训练营页面是否能正常生成内容

## 禁止事项

- 不要在主目录重新新增 Python 业务代码
- 不要引入新的多语言文档与多语言界面资源
- 不要把 .NET 版本改成非 `.NET 10`
- 不要引入任何 Python 运行链路（包括脚本、服务、镜像依赖与 CI 步骤）

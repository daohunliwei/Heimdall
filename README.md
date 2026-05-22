# Heimdall

Heimdall 是一个 AI 驱动的代码仓库智能文档系统，将任意 Git 仓库自动转换为结构化的中文 Wiki 文档、问答、演示文稿与训练营材料。

**技术栈**：C# / ASP.NET Core (.NET 10) + Next.js 16 (App Router) + PostgreSQL/pgvector

## 架构

```
Heimdall.Api (API 层)         →  控制器、中间件、DTO
    ↓
Heimdall.Core (业务层)        →  领域实体、业务服务、接口定义
    ↓
Heimdall.Repository (数据层)  →  EF Core、PostgreSQL/pgvector、仓储实现
    ↘              ↙
Heimdall.Infrastructure (工具层) →  Provider 适配、仓库源、配置、文本工具
```

详细架构文档：
- [`doc/architecture/backend-architecture.md`](doc/architecture/backend-architecture.md) — 后端架构设计
- [`doc/architecture/frontend-architecture.md`](doc/architecture/frontend-architecture.md) — 前端架构设计
- [`doc/architecture/architecture-upgrade-planV3.md`](doc/architecture/architecture-upgrade-planV3.md) — V3 升级方案

## 目录说明

| 目录 | 用途 |
|------|------|
| `backend/Heimdall.Api` | API 入口：控制器、中间件、DTO、`Program.cs` |
| `backend/Heimdall.Core` | 业务逻辑：实体、服务接口与实现、领域模型 |
| `backend/Heimdall.Infrastructure` | 工具层：LLM Provider 适配、仓库源、配置、文本工具 |
| `backend/Heimdall.Repository` | 数据层：EF Core DbContext、实体配置、仓储实现、迁移 |
| `frontend/src` | Next.js 前端（App Router） |
| `doc/architecture` | 架构升级方案与审计清单 |
| `scripts` | 开发脚本：环境配置、启动/停止、数据重置 |
| `docker` | Docker 构建文件 |

## 快速开始

### 方式一：一键启动（推荐）

```powershell
# 1. 配置密钥（交互式引导）
.\scripts\setup-env.ps1

# 2. 一键启动（自动检查 PostgreSQL、执行迁移、启动前后端）
.\scripts\dev-start.ps1
```

### 方式二：手动启动

#### 1. 启动数据库

```bash
docker compose up -d postgres
```

#### 2. 配置环境变量

```bash
cp .env.example .env
# 编辑 .env，填写数据库连接、Provider 密钥等
```

关键环境变量：

| 变量 | 说明 | 默认值 |
|------|------|--------|
| `HEIMDALL_CONNECTION_STRING` | PostgreSQL 连接字符串 | `Host=localhost;...` |
| `HEIMDALL_AUTH_MODE` | 认证模式：`none` / `jwt` | `jwt` |
| `HEIMDALL_JWT_SECRET` | JWT 签名密钥 | 生产环境必须设置 |
| `HEIMDALL_DEFAULT_PROVIDER` | 默认 LLM Provider | `ollama` |
| `HEIMDALL_EMBEDDER_TYPE` | 向量嵌入器类型 | `ollama` |
| `HEIMDALL_OLLAMA_CHAT_HOST` | Ollama Chat 地址 | 回退到 `OLLAMA_HOST` |
| `HEIMDALL_OLLAMA_EMBED_HOST` | Ollama Embedding 地址 | 回退到 `OLLAMA_HOST` |

#### 3. 应用数据库迁移

```bash
dotnet ef database update \
  --project backend/Heimdall.Repository/Heimdall.Repository.csproj \
  --startup-project backend/Heimdall.Api/Heimdall.Api.csproj
```

#### 4. 启动服务

```bash
# 后端（http://localhost:8001）
dotnet run --project backend/Heimdall.Api/Heimdall.Api.csproj

# 前端（http://localhost:3000）
cd frontend && npm install && npm run dev

# 或一键启动
cd frontend && npm run dev:all
```

## 调试指南

### 调试脚本一览

| 脚本 | 用途 |
|------|------|
| `scripts/setup-env.ps1` | 交互式配置 Provider 密钥，生成 `.env` 文件 |
| `scripts/dev-start.ps1` | 一键启动：检查 PostgreSQL → 生成 .env → 迁移 → 启动前后端 |
| `scripts/dev-stop.ps1` | 优雅停止前后端进程，可选停止 PostgreSQL |
| `scripts/dev-reset.ps1` | 清空 Wiki 缓存、任务记录、代码索引（保留仓库和用户数据） |
| `scripts/dev.ps1` | 经典启动脚本（支持 DryRun / BackendOnly / FrontendOnly 等模式） |

### 典型调试流程

```powershell
# 1. 首次配置
.\scripts\setup-env.ps1

# 2. 日常调试：重置数据 → 启动 → 测试
.\scripts\dev-reset.ps1 -Force
.\scripts\dev-start.ps1

# 3. 调试完成后停止
.\scripts\dev-stop.ps1

# 4. 仅启动后端进行 API 调试
.\scripts\dev-start.ps1 -BackendOnly

# 5. 预览启动命令（不实际执行）
.\scripts\dev.ps1 -DryRun
```

### 调试模式（Debug Mode）

在管理后台 `/admin/settings` → "调试设置" Tab 中开启调试模式：
- 开启后 Wiki 生成最多 5 页（可在 1-20 范围内调节）
- 大幅缩短调试反馈周期，快速验证管线逻辑
- 生成的 Wiki 版本会标记 `debug_truncated: true`

### AI 工具辅助调试

本项目包含 `CLAUDE.md`，Claude Code 等 AI 工具可基于其中的指令直接调用调试脚本。`.env` 文件已在 `.gitignore` 中排除，密钥不会泄露到 Git 历史。

## Provider 配置指南

### 支持矩阵

所有 Provider 适配器位于 `Heimdall.Infrastructure/Providers/`：

| Provider | 聊天 | 嵌入 | 配置方式 |
|----------|------|------|----------|
| OpenAI | ✅ | ✅ | `OPENAI_API_KEY` + `OPENAI_BASE_URL` |
| OpenRouter | ✅ | — | `OPENROUTER_API_KEY` |
| Google | ✅ | ✅ | `GOOGLE_API_KEY` |
| MiniMax | ✅ | — | `MINIMAX_API_KEY` + `MINIMAX_BASE_URL` |
| DashScope | ✅ | — | `DASHSCOPE_API_KEY` + `DASHSCOPE_BASE_URL` |
| DeepSeek | ✅ | — | `DEEPSEEK_API_KEY` + `DEEPSEEK_BASE_URL` |
| Azure OpenAI | ✅ | — | `AZURE_OPENAI_API_KEY` + Endpoint + Version |
| AWS Bedrock | ✅ | ✅ | `AWS_ACCESS_KEY_ID` + `AWS_SECRET_ACCESS_KEY` + `AWS_REGION` |
| Ollama | ✅ | ✅ | `OLLAMA_HOST` 或 `HEIMDALL_OLLAMA_CHAT_HOST` / `HEIMDALL_OLLAMA_EMBED_HOST` |

### 配置方式

**方式一：环境变量**（推荐用于开发）

编辑项目根目录的 `.env` 文件（从 `.env.example` 复制），填写对应 Provider 的 API Key：

```env
OPENAI_API_KEY=sk-your-key-here
GOOGLE_API_KEY=AIza-your-key-here
DEEPSEEK_API_KEY=sk-your-key-here
```

**方式二：配置文件**

在 `backend/Heimdall.Api/config/generator.json` 中配置 Provider 参数和模型列表。

**方式三：管理后台 UI**

启动服务后访问 `/admin/settings`，在 "Provider 管理" Tab 中可视化编辑模型元数据（上下文窗口、价格、填充比例等），修改即时生效无需重启。

### 配置加载优先级

1. ASP.NET Core 默认配置
2. `HEIMDALL_RUNTIME_CONFIG_PATH` 指向的 JSON 文件
3. 实际进程环境变量（`.env` 文件加载）
4. 命令行参数

同一个 Key 同时出现在多处时，**以优先级高的为准**（环境变量 > JSON 文件 > 默认值）。

## 环境变量参考

### 前端代理与联调

| Key | 含义 | 取值示例 | 默认值 |
| --- | --- | --- | --- |
| `SERVER_BASE_URL` | 前端代理后端基地址 | `http://localhost:8001` | `http://localhost:8001` |

### 后端公共配置

| Key | 含义 | 取值示例 | 是否必须 | 默认值 |
| --- | --- | --- | --- | --- |
| `HEIMDALL_CONNECTION_STRING` | PostgreSQL 连接字符串 | `Host=localhost;Port=5432;Database=heimdall;Username=heimdall;Password=heimdall` | 是 | — |
| `HEIMDALL_RUNTIME_CONFIG_PATH` | 后端运行配置文件路径 | `scripts/backend.runtime.config.json` | 否 | — |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core 运行环境 | `Development` | 否 | `Development` |
| `ASPNETCORE_URLS` | 后端监听地址 | `http://localhost:8001` | 否 | `http://localhost:8001` |
| `HEIMDALL_AUTH_MODE` | 认证模式 | `jwt` / `none` | 否 | `jwt` |
| `HEIMDALL_JWT_SECRET` | JWT 签名密钥 | `your-secret-key` | jwt 模式必须 | — |
| `HEIMDALL_JWT_EXPIRY_HOURS` | Token 过期小时数 | `72` | 否 | `72` |
| `HEIMDALL_REGISTRATION_OPEN` | 是否开放公开注册 | `true` | 否 | `true` |
| `HEIMDALL_DATA_DIR` | Wiki 缓存与项目数据目录 | `/data` | 否 | 程序目录下 `data` |
| `HEIMDALL_STORAGE_DIR` | 仓库克隆暂存目录 | `/storage` | 否 | 程序目录下 `storage` |
| `HEIMDALL_CONFIG_DIR` | 配置文件目录 | `/config` | 否 | 程序目录下 `config` |
| `HEIMDALL_DEFAULT_PROVIDER` | 默认聊天 Provider | `openai` | 否 | `generator.json` 中默认值 |
| `HEIMDALL_EMBEDDER_TYPE` | RAG 嵌入器类型 | `ollama` | 否 | `ollama` |
| `HEIMDALL_OLLAMA_CHAT_HOST` | Ollama Chat 地址 | `http://127.0.0.1:11434` | 使用 Ollama 时 | 回退 OLLAMA_HOST → `http://127.0.0.1:11434` |
| `HEIMDALL_OLLAMA_EMBED_HOST` | Ollama Embedding 地址 | `http://10.110.1.210:11434` | 使用 Ollama Embed 时 | 同上 |
| `HEIMDALL_HTTP_TIMEOUT_MINUTES` | HTTP 超时（分钟） | `180` | 否 | `180` |
| `HEIMDALL_WIKI_TASK_TIMEOUT_MINUTES` | Wiki 任务超时（分钟） | `180` | 否 | `180` |
| `HEIMDALL_OLLAMA_REQUEST_TIMEOUT_MINUTES` | Ollama 请求超时（分钟） | `60` | 否 | `60` |
| `OLLAMA_HOST` | Ollama 统一服务地址 | `http://127.0.0.1:11434` | 使用 Ollama 时 | 作为 Chat/Embed 兜底值 |

### Provider 密钥

完整密钥列表见 `.env.example` 模板文件。主要密钥：

| Key | Provider |
|-----|----------|
| `OPENAI_API_KEY` | OpenAI |
| `OPENROUTER_API_KEY` | OpenRouter |
| `GOOGLE_API_KEY` | Google Gemini |
| `MINIMAX_API_KEY` | MiniMax |
| `DASHSCOPE_API_KEY` | DashScope（阿里云百炼） |
| `DEEPSEEK_API_KEY` | DeepSeek |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI |
| `AWS_ACCESS_KEY_ID` + `AWS_SECRET_ACCESS_KEY` | AWS Bedrock |

## API 端点

### 仓库与版本

| 端点 | 说明 |
|------|------|
| `POST /api/repositories/import` | 导入仓库（URL → repositoryId） |
| `GET /api/repositories` | 列出所有仓库 |
| `GET /api/repositories/{id}` | 仓库详情 |
| `GET /api/repositories/{id}/versions` | 代码版本列表 |
| `GET /api/repositories/{id}/versions/latest` | 最新代码版本 |

### Wiki

| 端点 | 说明 |
|------|------|
| `GET /api/repositories/{id}/wiki/versions` | Wiki 版本列表 |
| `GET /api/repositories/{id}/wiki/pages?wikiVersionId=` | 版本页面内容 |
| `POST /api/repositories/{id}/wiki/refresh` | 提交 Wiki 刷新任务（异步，返回 task_id） |
| `POST /api/repositories/{id}/wiki/versions/{wvId}/publish` | 发布指定版本 |
| `POST /api/repositories/{id}/wiki/compare` | 比较两个 Wiki 版本 |

### 任务

| 端点 | 说明 |
|------|------|
| `POST /tasks/ask` | AI 问答（支持 DeepResearch） |
| `POST /tasks/slides` | 生成演示幻灯片 |
| `POST /tasks/workshop` | 生成工作坊材料 |
| `GET /tasks/{id}/status` | 查询任务状态 |
| `GET /tasks/{id}/stream` | SSE 订阅任务进度 |
| `GET /tasks/{id}/token-summary` | Token 消耗汇总 |
| `POST /api/tasks/{id}/resume` | 恢复中断的任务 |

### 管理

| 端点 | 说明 |
|------|------|
| `GET /admin/dashboard` | 管理仪表盘（需 Admin） |
| `GET /api/admin/provider-metadata` | Provider 模型元数据列表 |
| `GET /api/admin/provider-status` | Provider 连接状态 |
| `GET /api/admin/system-config` | 系统运行时配置 |
| `GET /api/admin/system-info` | 系统信息摘要 |
| `GET /api/admin/debug-config` | 调试模式配置 |
| `PUT /api/admin/debug-config` | 更新调试模式配置 |

### 其他

| 端点 | 说明 |
|------|------|
| `POST /chat/completions/stream` | 流式聊天 |
| `GET /models/config` | Provider/Model 配置 |
| `POST /auth/login` | 用户登录 |

## Docker 部署

```bash
# 构建镜像
docker build -f docker/backend/Dockerfile -t heimdall-backend:latest .
docker build -f docker/frontend/Dockerfile -t heimdall-frontend:latest .

# 启动全部服务（含 PostgreSQL）
docker compose up -d
```

## 故障排查

### 数据库连接失败

```
NpgsqlException: Failed to connect to localhost:5432
```

**解决**：确认 PostgreSQL 容器已启动：
```powershell
docker ps | Select-String postgres
docker compose up -d postgres
```

检查 `HEIMDALL_CONNECTION_STRING` 中 Host/Port/Username/Password 是否正确。

### Provider API Key 未配置

```
InvalidOperationException: API Key not configured for provider 'xxx'
```

**解决**：运行 `.\scripts\setup-env.ps1` 重新配置密钥，或直接编辑 `.env` 文件补充对应 Provider 的 Key。确保启动前已加载 `.env`。

### Ollama 连接超时

```
HttpRequestException: Connection refused (127.0.0.1:11434)
```

**解决**：确认 Ollama 服务已启动并监听正确端口。检查 `OLLAMA_HOST` / `HEIMDALL_OLLAMA_CHAT_HOST` 配置。

### 前端代理报错

```
ECONNREFUSED ::1:8001
```

**解决**：确认后端已启动。`SERVER_BASE_URL` 应指向后端实际监听地址（默认 `http://localhost:8001`）。

### 数据库迁移失败

```
The migration has already been applied
```

**解决**：无需处理，迁移已是最新。若表结构异常可运行 `.\scripts\dev-reset.ps1` 清空数据库后重新迁移。

### Wiki 生成超时

**解决**：
- 调大 `HEIMDALL_WIKI_TASK_TIMEOUT_MINUTES`（默认 180 分钟）
- 对于大仓库，开启调试模式限制生成页数（`/admin/settings` → 调试设置）
- 检查 Provider 速率限制，避免被限流

### 页面空白或加载错误

**解决**：
1. 检查浏览器控制台是否有 Next.js 错误
2. 确认前端 `.env` 中 `SERVER_BASE_URL` 指向正确后端地址
3. 尝试清除前端缓存：`cd frontend && rm -rf .next && npm run dev`

## 验证

```bash
# 后端构建
dotnet build backend/Heimdall.Api/Heimdall.Api.csproj

# 前端构建 + Lint
cd frontend && npm run build && npm run lint
```

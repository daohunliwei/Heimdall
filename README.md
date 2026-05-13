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

详细架构文档：[`doc/architecture/architecture-upgrade-plan.md`](doc/architecture/architecture-upgrade-plan.md)
审计对比清单：[`doc/architecture/audit-checklist.md`](doc/architecture/audit-checklist.md)

## 目录说明

| 目录 | 用途 |
|------|------|
| `backend/Heimdall.Api` | API 入口：控制器、中间件、DTO、`Program.cs` |
| `backend/Heimdall.Core` | 业务逻辑：实体、服务接口与实现、领域模型 |
| `backend/Heimdall.Infrastructure` | 工具层：LLM Provider 适配、仓库源、配置、文本工具 |
| `backend/Heimdall.Repository` | 数据层：EF Core DbContext、实体配置、仓储实现、迁移 |
| `frontend/src` | Next.js 前端（App Router） |
| `doc/architecture` | 架构升级方案与审计清单 |
| `scripts` | 开发环境变量与启动脚本 |
| `docker` | Docker 构建文件 |

## 快速开始

### 1. 启动数据库

```bash
docker compose up -d postgres
```

### 2. 配置环境变量

```bash
cp scripts/dev.env.example scripts/dev.env
# 编辑 dev.env，填写数据库连接、Provider 密钥等
```

关键环境变量：

| 变量 | 说明 | 默认值 |
|------|------|--------|
| `HEIMDALL_CONNECTION_STRING` | PostgreSQL 连接字符串 | `Host=localhost;...` |
| `HEIMDALL_AUTH_MODE` | 认证模式：`none` / `jwt` | `jwt` |
| `HEIMDALL_JWT_SECRET` | JWT 签名密钥 | 生产环境必须设置 |
| `HEIMDALL_DEFAULT_PROVIDER` | 默认 LLM Provider | `google` |
| `HEIMDALL_EMBEDDER_TYPE` | 向量嵌入器类型 | `ollama` |
| `HEIMDALL_OLLAMA_CHAT_HOST` | Ollama Chat 地址 | 回退到 `OLLAMA_HOST` |
| `HEIMDALL_OLLAMA_EMBED_HOST` | Ollama Embedding 地址 | 回退到 `OLLAMA_HOST` |

### 3. 启动服务

```bash
# 后端（http://localhost:8001）
dotnet run --project backend/Heimdall.Api/Heimdall.Api.csproj

# 前端（http://localhost:3000）
cd frontend && npm install && npm run dev

# 一键启动
cd frontend && npm run dev:all
```

## 应用数据库迁移

```bash
export HEIMDALL_CONNECTION_STRING="Host=...;Database=heimdall;..."
dotnet ef database update \
  --project backend/Heimdall.Repository/Heimdall.Repository.csproj \
  --startup-project backend/Heimdall.Api/Heimdall.Api.csproj
```

## Docker 部署

```bash
# 构建镜像
docker build -f docker/backend/Dockerfile -t heimdall-backend:latest .
docker build -f docker/frontend/Dockerfile -t heimdall-frontend:latest .

# 启动全部服务（含 PostgreSQL）
docker compose up -d
```

## 环境变量参考

### 数据库与认证

| Key | 说明 |
|-----|------|
| `HEIMDALL_CONNECTION_STRING` | PostgreSQL 连接字符串 |
| `HEIMDALL_AUTH_MODE` | `none`=无认证, `jwt`=JWT Bearer |
| `HEIMDALL_JWT_SECRET` | JWT 签名密钥 |
| `HEIMDALL_JWT_EXPIRY_HOURS` | Token 过期小时数（默认 72） |
| `HEIMDALL_REGISTRATION_OPEN` | 是否开放公开注册（默认 true） |

### Provider 配置

| Key | 说明 |
|-----|------|
| `HEIMDALL_DEFAULT_PROVIDER` | 默认 Provider（openai/ollama/google/...） |
| `HEIMDALL_EMBEDDER_TYPE` | 嵌入器类型（openai/ollama/google/bedrock） |
| `HEIMDALL_OLLAMA_CHAT_HOST` | Ollama Chat 地址 |
| `HEIMDALL_OLLAMA_EMBED_HOST` | Ollama Embedding 地址 |
| `OPENAI_API_KEY` / `GOOGLE_API_KEY` 等 | 各 Provider 密钥 |

### 超时

| Key | 默认值 |
|-----|--------|
| `HEIMDALL_HTTP_TIMEOUT_MINUTES` | 180 |
| `HEIMDALL_WIKI_TASK_TIMEOUT_MINUTES` | 180 |
| `HEIMDALL_OLLAMA_REQUEST_TIMEOUT_MINUTES` | 60 |

## API 端点

| 端点 | 说明 |
|------|------|
| `POST /tasks/wiki` | 提交 Wiki 生成任务（异步，立即返回 task_id） |
| `GET /tasks/{id}/status` | 查询任务状态 |
| `GET /tasks/{id}/stream` | SSE 订阅任务进度 |
| `GET /tasks/{id}/token-summary` | Token 消耗汇总 |
| `POST /chat/completions/stream` | 流式聊天 |
| `GET /models/config` | Provider/Model 配置 |
| `POST /auth/login` | 用户登录 |
| `GET /admin/dashboard` | 管理仪表盘 |

## 验证

```bash
dotnet build backend/Heimdall.Api/Heimdall.Api.csproj
cd frontend && npm run build && npm run lint
```

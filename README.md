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

所有 Provider 逻辑和密钥配置**均已完整保留**，由 `Heimdall.Infrastructure` 层的 `HeimdallConfigService` 和各个 Provider 适配器统一处理。

### 前端代理与联调

| Key | 含义 | 取值示例 | 是否必须 | 默认值 / 备注 |
| --- | --- | --- | --- | --- |
| `SERVER_BASE_URL` | 前端代理后端接口时使用的后端基地址 | `http://localhost:8001` | 前端联调时建议配置 | 默认 `http://localhost:8001` |

### 后端公共配置

| Key | 含义 | 取值示例 | 是否必须 | 默认值 / 备注 |
| --- | --- | --- | --- | --- |
| `HEIMDALL_CONNECTION_STRING` | PostgreSQL 连接字符串 | `Host=localhost;Port=5432;Database=heimdall;Username=heimdall;Password=heimdall` | 是 | 新架构硬依赖 |
| `HEIMDALL_RUNTIME_CONFIG_PATH` | 后端运行配置文件路径 | `scripts/backend.runtime.config.json` | 否 | 可替代零散环境变量 |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core 运行环境 | `Development` | 否 | 本地脚本默认 `Development` |
| `ASPNETCORE_URLS` | 后端监听地址 | `http://localhost:8001` | 否 | 本地脚本默认 `http://localhost:8001` |
| `HEIMDALL_AUTH_MODE` | 认证模式 | `jwt` / `none` | 否 | `none`=调试环境无认证，`jwt`=生产 JWT 认证 |
| `HEIMDALL_JWT_SECRET` | JWT 签名密钥 | `your-secret-key` | `jwt` 模式必须 | |
| `HEIMDALL_JWT_EXPIRY_HOURS` | Token 过期小时数 | `72` | 否 | 默认 `72` |
| `HEIMDALL_REGISTRATION_OPEN` | 是否开放公开注册 | `true` | 否 | 默认 `true` |
| `HEIMDALL_DATA_DIR` | Wiki 缓存与项目数据目录 | `/data` | 否 | 默认程序目录下的 `data` |
| `HEIMDALL_STORAGE_DIR` | 仓库克隆与暂存目录根路径 | `/storage` | 否 | 默认程序目录下的 `storage`，任务完成可清理 |
| `HEIMDALL_CONFIG_DIR` | `generator.json`、`embedder.json` 等配置目录 | `/config` | 否 | 默认程序目录下的 `config` |
| `HEIMDALL_DEFAULT_PROVIDER` | 默认聊天 Provider | `openai` | 否 | 未命中时回退到 `generator.json` 中的默认值 |
| `HEIMDALL_EMBEDDER_TYPE` | RAG 嵌入器类型 | `ollama` | 否 | 可选 `openai`、`google`、`ollama`、`bedrock`，默认 `ollama` |
| `HEIMDALL_OLLAMA_CHAT_HOST` | Ollama Chat 服务地址 | `http://127.0.0.1:11434` | 使用 Ollama 时可选 | 回退到 `OLLAMA_HOST`，再回退 `http://127.0.0.1:11434` |
| `HEIMDALL_OLLAMA_EMBED_HOST` | Ollama Embedding 服务地址 | `http://10.110.1.210:11434` | 使用 Ollama Embedding 时可选 | 回退到 `OLLAMA_HOST`，再回退 `http://127.0.0.1:11434` |
| `HEIMDALL_HTTP_TIMEOUT_MINUTES` | 后端默认 HttpClient 超时（分钟） | `180` | 否 | 默认 `180` |
| `HEIMDALL_WIKI_TASK_TIMEOUT_MINUTES` | 单次 Wiki 任务总超时（分钟） | `180` | 否 | 默认 `180` |
| `HEIMDALL_OLLAMA_REQUEST_TIMEOUT_MINUTES` | 单次 Ollama 请求超时（分钟） | `60` | 否 | 默认 `60` |
| `OLLAMA_HOST` | Ollama 统一服务地址 | `http://127.0.0.1:11434` | 使用 Ollama 时必须 | 作为 Chat/Embed 的兜底值 |

### Provider 与密钥配置

| Key | 含义 | 取值示例 | 是否必须 | 默认值 / 备注 |
| --- | --- | --- | --- | --- |
| `OPENAI_API_KEY` | OpenAI 聊天与嵌入调用密钥 | `sk-...` | 使用 OpenAI 时必须 | 与 `OPENAI_BASE_URL` 搭配可兼容代理地址 |
| `OPENAI_BASE_URL` | OpenAI 兼容接口基地址 | `https://api.openai.com/v1` | 否 | 默认 `https://api.openai.com/v1` |
| `OPENROUTER_API_KEY` | OpenRouter 调用密钥 | `sk-or-...` | 使用 OpenRouter 时必须 | 使用固定官方接口地址 |
| `GOOGLE_API_KEY` | Google 模型调用密钥 | `AIza...` | 使用 Google 时必须 | 同时用于聊天与嵌入能力 |
| `MINIMAX_API_KEY` | MiniMax 调用密钥 | `eyJ...` | 使用 MiniMax 时必须 | 仅聊天 Provider 使用 |
| `MINIMAX_BASE_URL` | MiniMax 接口基地址 | `https://api.minimaxi.com/v1` | 否 | 默认 `https://api.minimaxi.com/v1`，海外域名可改为 `https://api.minimax.io/v1` |
| `DASHSCOPE_API_KEY` | DashScope 调用密钥 | `sk-...` | 使用 DashScope 时必须 | 按 OpenAI 兼容协议调用 |
| `DASHSCOPE_BASE_URL` | DashScope 兼容接口基地址 | `https://dashscope.aliyuncs.com/compatible-mode/v1` | 否 | |
| `DASHSCOPE_WORKSPACE_ID` | DashScope 工作空间 ID | `ws_1234567890` | 否 | 配置后会附加到请求头 |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI 调用密钥 | `your-key` | 使用 Azure OpenAI 时必须 | 需与 `AZURE_OPENAI_ENDPOINT`、`AZURE_OPENAI_VERSION` 一起配置 |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI 资源地址 | `https://your-resource.openai.azure.com` | 使用 Azure OpenAI 时必须 | 不带具体路径 |
| `AZURE_OPENAI_VERSION` | Azure OpenAI API 版本 | `2024-10-21` | 使用 Azure OpenAI 时必须 | 按 Azure 实际可用版本填写 |
| `AWS_ACCESS_KEY_ID` | AWS 访问密钥 ID | `AKIA...` | 使用 Bedrock 且不走角色链路时必须 | 与 `AWS_SECRET_ACCESS_KEY` 配对使用 |
| `AWS_SECRET_ACCESS_KEY` | AWS 访问密钥 Secret | `abcd...` | 使用 Bedrock 且不走角色链路时必须 | 与 `AWS_ACCESS_KEY_ID` 配对使用 |
| `AWS_SESSION_TOKEN` | AWS 临时会话令牌 | `IQoJ...` | 否 | 使用临时凭证时填写 |
| `AWS_REGION` | AWS 区域 | `us-east-1` | 使用 Bedrock 时建议配置 | 默认 `us-east-1` |
| `AWS_ROLE_ARN` | 需要切换的 AWS 角色 ARN | `arn:aws:iam::...` | 否 | 配置后可结合当前凭证执行角色切换 |

### 配置加载优先级

1. ASP.NET Core 默认配置
2. `HEIMDALL_RUNTIME_CONFIG_PATH` 指向的 JSON 文件
3. 实际进程环境变量
4. 命令行参数

同一个 Key 同时出现在 JSON 文件和环境变量中时，**以环境变量为准**。

### Provider 支持矩阵

所有 Provider 适配器位于 `Heimdall.Infrastructure/Providers/`：

| Provider | 聊天 | 嵌入 | 配置方式 |
|----------|------|------|----------|
| OpenAI | ✅ | ✅ | `OPENAI_API_KEY` + `OPENAI_BASE_URL` |
| OpenRouter | ✅ | — | `OPENROUTER_API_KEY` |
| Google | ✅ | ✅ | `GOOGLE_API_KEY` |
| MiniMax | ✅ | — | `MINIMAX_API_KEY` + `MINIMAX_BASE_URL` |
| DashScope | ✅ | — | `DASHSCOPE_API_KEY` + `DASHSCOPE_BASE_URL` |
| Azure OpenAI | ✅ | — | `AZURE_OPENAI_API_KEY` + Endpoint + Version |
| AWS Bedrock | ✅ | ✅ | `AWS_ACCESS_KEY_ID` + `AWS_SECRET_ACCESS_KEY` + `AWS_REGION` |
| Ollama | ✅ | ✅ | `OLLAMA_HOST` 或 `HEIMDALL_OLLAMA_CHAT_HOST` / `HEIMDALL_OLLAMA_EMBED_HOST` |

## API 端点

| 端点 | 说明 |
|------|------|
| `POST /api/repositories/{repositoryId}/wiki/refresh` | 提交 Wiki 刷新/生成任务（异步，立即返回 task_id） |
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

# Heimdall

Heimdall 是一个 AI 驱动的代码仓库智能文档系统，将任意 Git 仓库自动转换为结构化的中文 Wiki 文档、问答、演示文稿与训练营材料。

**技术栈**：C# / ASP.NET Core (.NET 10) + Next.js 16 (App Router) + PostgreSQL/pgvector + SqlSugar + Microsoft.Extensions.AI

## 架构

```
Heimdall.Api (API 层)         →  控制器、中间件、DTO
    ↓
Heimdall.Core (业务层)        →  领域实体、业务服务、接口定义
    ↓
Heimdall.Repository (数据层)  →  SqlSugar ORM、仓储实现
    ↘              ↙
Heimdall.Infrastructure (工具层) →  MEAI IChatClient Provider、仓库源、BM25 搜索、配置、文本工具
```

详细架构文档：[`docs/architecture/architecture.md`](docs/architecture/architecture.md)

## 目录说明

| 目录 | 用途 |
|------|------|
| `backend/Heimdall.Api` | API 入口：控制器、中间件、DTO、`Program.cs` |
| `backend/Heimdall.Core` | 业务逻辑：`[SugarTable]` 实体、服务接口与实现、领域模型 |
| `backend/Heimdall.Infrastructure` | 工具层：MEAI IChatClient Provider 适配、仓库源、BM25 搜索、配置、文本工具 |
| `backend/Heimdall.Repository` | 数据层：SqlSugar 仓储实现（注入 `ISqlSugarClient`） |
| `frontend/src` | Next.js 前端（App Router） |
| `docs/architecture` | 系统架构设计文档 |
| `scripts` | 开发脚本：环境配置、启动/停止、数据重置 |

## 快速开始

### 1. 配置环境变量

```bash
# 从模板复制
cp scripts/dev.env.example scripts/dev.env

# 编辑 scripts/dev.env，填写数据库连接、Provider 密钥等
```

关键环境变量：

| 变量 | 说明 | 默认值 |
|------|------|--------|
| `HEIMDALL_CONNECTION_STRING` | PostgreSQL 连接字符串 | `Host=localhost;...` |
| `HEIMDALL_AUTH_MODE` | 认证模式：`none` / `jwt` | `jwt` |
| `HEIMDALL_JWT_SECRET` | JWT 签名密钥 | 生产环境必须设置 |
| `HEIMDALL_CODEFIRST_AUTOSYNC` | 启动时自动同步表结构 | `true` |
| `HEIMDALL_DEFAULT_PROVIDER` | 默认 LLM Provider | `ollama` |
| `HEIMDALL_OLLAMA_CHAT_HOST` | Ollama Chat 地址 | `http://127.0.0.1:11434` |

完整变量列表见 `scripts/dev.env.example`。

### 2. 启动服务

```bash
# macOS / Linux — 一键启动（自动加载 dev.env → 后端 → 前端）
bash scripts/dev.sh

# Windows — 一键启动
.\scripts\dev-start.ps1

# 仅后端（http://localhost:8001）
bash scripts/dev.sh --backend-only

# 仅前端（http://localhost:3000）
.\scripts\dev.ps1 -FrontendOnly

# 预览启动命令（不实际执行）
.\scripts\dev.ps1 -DryRun
```

启动时若 `HEIMDALL_CODEFIRST_AUTOSYNC=true`，会自动同步数据库表结构。首次启动建议开启。

### 3. 回退方案：手动建表

如果 CodeFirst 同步失败，可通过 SqlSugar `ISqlSugarClient.DbMaintenance` API 导出建表脚本，或启动时设置 `HEIMDALL_CODEFIRST_AUTOSYNC=true` 自动同步。

## 调试指南

### 环境变量（唯一来源：`scripts/dev.env`）

所有调试凭据统一存放在 `scripts/dev.env`（已 gitignore）。脚本自动加载此文件，**命令行中不出现密码明文**。

首次使用：
```bash
cp scripts/dev.env.example scripts/dev.env
# 编辑填入真实值
```

### 调试脚本一览

| 脚本 | 平台 | 用途 |
|------|------|------|
| `scripts/dev.sh` | macOS/Linux | 加载 dev.env → 启动后端 → 健康检查 → 启动前端 |
| `scripts/dev.ps1` | Windows | 同上（支持 DryRun / BackendOnly / FrontendOnly） |
| `scripts/dev-start.ps1` | Windows | 一键启动：检查 PostgreSQL → 生成 .env → 启动前后端 |
| `scripts/setup-env.ps1` | Windows | 交互式配置 Provider 密钥 |
| `scripts/dev-stop.ps1` | Windows | 优雅停止前后端进程 |
| `scripts/dev-reset.ps1` | Windows | 清空 Wiki 缓存、任务记录、代码索引（保留仓库和用户） |

### 典型调试流程

```bash
# macOS / Linux
bash scripts/dev.sh

# Windows
.\scripts\dev-reset.ps1 -Force
.\scripts\dev-start.ps1

# 仅启动后端进行 API 调试
bash scripts/dev.sh --backend-only

# 预览启动命令（不实际执行）
.\scripts\dev.ps1 -DryRun
```

### AI 工具辅助调试

本项目包含 `CLAUDE.md` 和 `AGENTS.md`，Claude Code 等 AI 工具可基于其中的指令直接调试。`scripts/dev.env` 和 `.env` 文件已在 `.gitignore` 中排除，密钥不会泄露。

## Provider 配置指南

### 支持矩阵

所有 Provider 适配器位于 `Heimdall.Infrastructure/Providers/`：

| Provider | 聊天 | 实现方式 |
|----------|------|----------|
| OpenAI | ✅ | `OpenAiCompatibleClientFactory`（`Microsoft.Extensions.AI.OpenAI`） |
| OpenRouter | ✅ | 同上 |
| DashScope | ✅ | 同上 |
| DeepSeek | ✅ | 同上 |
| Azure OpenAI | ✅ | 同上 |
| AWS Bedrock | ✅ | `BedrockClientFactory`（Converse API） |
| Google Gemini | ✅ | `CustomBackends/GeminiChatClient`（自定义 IChatClient） |
| MiniMax | ✅ | `CustomBackends/MiniMaxChatClient`（自定义 IChatClient） |
| Ollama | ✅ | `CustomBackends/OllamaChatClient`（自定义 IChatClient） |

### 配置方式

编辑 `scripts/dev.env`，填写对应 Provider 的 API Key：

```env
OPENAI_API_KEY=sk-your-key-here
GOOGLE_API_KEY=AIza-your-key-here
DEEPSEEK_API_KEY=sk-your-key-here
```

**管理后台 UI**：启动后访问 `/admin/settings`，在 "Provider 管理" Tab 中可视化编辑模型元数据（上下文窗口、价格、填充比例等）。

## 环境变量参考

### 后端公共配置

| Key | 含义 | 默认值 |
| --- | --- | --- |
| `HEIMDALL_CONNECTION_STRING` | PostgreSQL 连接字符串 | — |
| `HEIMDALL_AUTH_MODE` | 认证模式：`jwt` / `none` | `jwt` |
| `HEIMDALL_JWT_SECRET` | JWT 签名密钥 | — |
| `HEIMDALL_REGISTRATION_OPEN` | 是否开放注册 | `true` |
| `HEIMDALL_CODEFIRST_AUTOSYNC` | 启动时自动同步表结构 | `true` |
| `HEIMDALL_DEFAULT_PROVIDER` | 默认聊天 Provider | `ollama` |
| `HEIMDALL_OLLAMA_CHAT_HOST` | Ollama Chat 地址 | `http://127.0.0.1:11434` |
| `HEIMDALL_LOG_SQL` | 是否输出 SQL 日志 | `false` |
| `ASPNETCORE_URLS` | 后端监听地址 | `http://localhost:8001` |
| `SERVER_BASE_URL` | 前端代理后端地址 | `http://localhost:8001` |

### Provider 密钥

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
| `POST /api/repositories/import` | 导入仓库 |
| `GET /api/repositories` | 列出所有仓库 |
| `GET /api/repositories/{id}` | 仓库详情 |
| `GET /api/repositories/{id}/versions` | 代码版本列表 |

### Wiki

| 端点 | 说明 |
|------|------|
| `GET /api/repositories/{id}/wiki/versions` | Wiki 版本列表 |
| `GET /api/repositories/{id}/wiki/pages?wikiVersionId=` | 版本页面内容 |
| `POST /api/repositories/{id}/wiki/refresh` | 提交 Wiki 刷新任务 |
| `POST /api/repositories/{id}/wiki/compare` | 比较两个 Wiki 版本 |

### 任务

| 端点 | 说明 |
|------|------|
| `POST /tasks/ask` | AI 问答（JSON 响应） |
| `POST /tasks/ask/stream` | AI 流式问答（SSE） |
| `POST /tasks/slides` | 生成演示幻灯片 |
| `POST /tasks/workshop` | 生成工作坊材料 |
| `GET /tasks/{id}/status` | 查询任务状态 |
| `GET /tasks/{id}/stream` | SSE 订阅任务进度 |

### 流式

| 端点 | 说明 |
|------|------|
| `POST /chat/completions/stream` | 流式聊天（SSE） |
| `GET /models/config` | Provider/Model 配置 |

### 管理

| 端点 | 说明 |
|------|------|
| `GET /admin/dashboard` | 管理仪表盘 |
| `GET /api/admin/provider-metadata` | Provider 模型元数据 |
| `GET /api/admin/system-info` | 系统信息摘要 |

## 生产部署

```bash
docker compose up -d
```

首次启动约 30-60 秒（PostgreSQL → 数据库初始化 → 后端 CodeFirst 建表 → 前端）。

完整部署指南（环境变量、Provider 配置、Nginx 反向代理、备份恢复）：**[部署手册 →](docs/deploy.md)**

## 故障排查

### 数据库连接失败

```
NpgsqlException: Failed to connect to localhost:5432
```

检查 `scripts/dev.env` 中 `HEIMDALL_CONNECTION_STRING` 的 Host/Port/Username/Password 是否正确。

### 表结构不匹配

```
PostgresException: column "xxx" does not exist
```

设置 `HEIMDALL_CODEFIRST_AUTOSYNC=true` 重启应用即可自动同步表结构。

### Provider API Key 未配置

```
InvalidOperationException: API Key not configured for provider 'xxx'
```

编辑 `scripts/dev.env` 补充对应 Provider 的 Key，确保启动前脚本已加载该文件。

### 页面空白或加载错误

1. 检查浏览器控制台是否有 Next.js 错误
2. 确认 `SERVER_BASE_URL` 指向正确后端地址
3. 清除前端缓存：`cd frontend && rm -rf .next && npm run dev`

## 验证

```bash
# 后端构建
dotnet build backend/Heimdall.Api/Heimdall.Api.csproj

# 前端构建 + Lint
cd frontend && npm run build && npm run lint
```

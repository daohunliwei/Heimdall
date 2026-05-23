# Heimdall 生产环境部署指南

## 前置条件

- **Docker** ≥ 24.0，已安装 Docker Compose（`docker compose` 子命令）
- 至少 **4 GB** 可用内存（PostgreSQL + 后端 + 前端）
- 一个可用的 **LLM Provider** API Key（OpenAI / MiniMax / Ollama 等），用于 AI 生成功能

## 快速部署

### 1. 获取镜像

镜像已发布到 GitHub Container Registry（GHCR）：

```bash
# 拉取最新镜像（可选，docker compose 会自动拉取）
docker pull ghcr.io/daohunliwei/heimdall-backend:latest
docker pull ghcr.io/daohunliwei/heimdall-frontend:latest
```

### 2. 配置环境变量

创建 `.env` 文件（或直接 export 环境变量）：

```bash
# 数据库密码（必填，不要使用默认值）
DB_PASSWORD=<your-strong-password>

# JWT 签名密钥（必填，生成方式见下方）
JWT_SECRET=<your-jwt-secret>

# 认证模式：jwt（推荐）或 none（仅内网调试）
AUTH_MODE=jwt

# 是否开放注册（建议生产环境设为 false）
REGISTRATION_OPEN=false

# 默认 LLM Provider：ollama / openai / minimax / deepseek 等
DEFAULT_PROVIDER=ollama
```

生成 JWT 密钥：

```bash
openssl rand -hex 32
```

### 3. 配置 Provider 密钥

如果使用云端 LLM Provider（OpenAI、MiniMax 等），需要在 `.env` 中补充对应密钥：

```bash
HEIMDALL_OPENAI_API_KEY=sk-xxx
HEIMDALL_MINIMAX_API_KEY=sk-xxx
HEIMDALL_DEEPSEEK_API_KEY=sk-xxx
# ... 等
```

完整环境变量列表见 `docker-compose.yml` 中 backend 服务的 `environment` 配置。

### 4. 启动服务

```bash
# 后台启动所有服务
docker compose up -d

# 查看启动日志
docker compose logs -f

# 查看服务状态
docker compose ps
```

启动过程：PostgreSQL → 数据库初始化（pgvector 扩展）→ 后端（CodeFirst 自动建表）→ 前端。

首次启动约需 30-60 秒（包括镜像拉取）。

### 5. 验证部署

```bash
# 后端健康检查
curl http://localhost:8001/api/repositories

# 前端管理后台
open http://localhost:3000/admin/dashboard
```

## 端口说明

| 服务 | 默认端口 | 说明 |
|------|----------|------|
| 后端 API | `8001` | 通过 `BACKEND_PORT` 环境变量修改 |
| 前端 Web | `3000` | 通过 `FRONTEND_PORT` 环境变量修改 |
| PostgreSQL | `5432` | 默认仅容器内网可见，如需外部访问取消 ports 注释 |

## 配置 Provider

### 使用 Ollama（本地）

```bash
# 确保 Ollama 已安装并运行
ollama serve

# 拉取模型
ollama pull qwen3:32b

# .env 中配置
DEFAULT_PROVIDER=ollama
HEIMDALL_OLLAMA_CHAT_HOST=http://host.docker.internal:11434
```

### 使用 MiniMax

```bash
# .env 中配置
DEFAULT_PROVIDER=minimax
HEIMDALL_MINIMAX_API_KEY=sk-xxx
HEIMDALL_MINIMAX_MODEL=MiniMax-M2.7
```

### 通过管理后台配置

启动后访问 `/admin/settings` → Provider 管理，可视化编辑每个 Provider/Model 的元数据（上下文窗口、价格、速率限制等），修改即时生效无需重启。

## 数据管理

### 数据持久化

所有数据存储在 Docker volumes 中：
- `pgdata`：PostgreSQL 数据库文件
- `heimdall-data`：Wiki 缓存、仓库克隆等运行时数据

```bash
# 备份数据库
docker compose exec postgres pg_dump -U heimdall heimdall > backup.sql

# 恢复数据库
docker compose exec -T postgres psql -U heimdall heimdall < backup.sql
```

### 重置数据

```bash
# 停止服务并删除所有数据卷
docker compose down -v

# 重新启动（会创建全新的数据库）
docker compose up -d
```

## 升级指南

```bash
# 拉取最新镜像
docker pull ghcr.io/daohunliwei/heimdall-backend:latest
docker pull ghcr.io/daohunliwei/heimdall-frontend:latest

# 重新创建容器
docker compose up -d --force-recreate

# 后端启动时会自动通过 CodeFirst 同步新增的数据库表
```

## 故障排查

### 后端启动失败

```bash
# 查看后端日志
docker compose logs backend

# 常见原因：
# 1. 数据库连接失败 → 检查 DB_PASSWORD
# 2. JWT_SECRET 未设置 → 设置至少 32 字符的密钥
# 3. 端口冲突 → 修改 BACKEND_PORT
```

### 前端页面空白

```bash
# 检查前端能否访问后端
docker compose exec frontend wget -qO- http://backend:8001/api/repositories

# 确认 SERVER_BASE_URL 正确
docker compose exec frontend env | grep SERVER_BASE_URL
```

### 数据库连接失败

```bash
# 确认 PostgreSQL 已就绪
docker compose exec postgres pg_isready -U heimdall -d heimdall

# 检查连接字符串
docker compose exec backend env | grep HEIMDALL_CONNECTION_STRING
```

## 安全建议

1. **`JWT_SECRET`**：务必修改默认值，使用 `openssl rand -hex 32` 生成
2. **`DB_PASSWORD`**：不使用默认密码
3. **`REGISTRATION_OPEN`**：生产环境建议设置为 `false`
4. **`AUTH_MODE`**：生产环境使用 `jwt`，仅在纯内网调试时使用 `none`
5. **端口暴露**：在生产环境中通过 Nginx/Caddy 等反向代理对外暴露，不要直接暴露后端端口

## Nginx 反向代理（可选）

```nginx
server {
    listen 80;
    server_name heimdall.example.com;

    # 前端
    location / {
        proxy_pass http://127.0.0.1:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }

    # 后端 API
    location /api/ {
        proxy_pass http://127.0.0.1:8001;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

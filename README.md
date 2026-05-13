# Heimdall

Heimdall 是一个开源的中文代码仓库文档系统，当前主技术栈为：

- 后端：C# / ASP.NET Core（`.NET 10`）
- 前端：Next.js

## 当前目录说明

- `backend/Heimdall.Api`：新的 C# 后端，包含接口入口、配置加载、Provider 适配、仓库访问、RAG 检索与缓存能力
- `frontend/src`：Next.js 前端
- `frontend/public`：前端静态资源
- `.trae/specs`：本次改造的规格、任务和验收清单

## 开发启动

### 启动后端

```bash
dotnet run --project backend/Heimdall.Api/Heimdall.Api.csproj
```

默认监听 `http://localhost:8001`。

### 启动前端

```bash
cd frontend
npm install
npm run dev
```

默认访问地址为 [http://localhost:3000](http://localhost:3000)。

### 一键启动（推荐）

先复制一份本地环境变量文件并按需修改：

```bash
copy scripts\dev.env.example scripts\dev.env
```

如果你不想把后端配置散落在一堆环境变量里，建议再复制一份后端集中配置文件：

```bash
copy scripts\backend.runtime.config.example.json scripts\backend.runtime.config.json
```

然后一键启动前后端：

```bash
cd frontend
npm run dev:all
```

然后在 `scripts/dev.env` 中补上这一行，让后端启动时自动读取该文件：

```bash
HEIMDALL_RUNTIME_CONFIG_PATH=scripts/backend.runtime.config.json
```

如需临时覆盖环境变量，可通过参数注入：

```bash
cd frontend
npm run dev:all -- -Env "SERVER_BASE_URL=http://localhost:8001;OPENAI_API_KEY=你的密钥"
```

如需仅检查解析与命令拼装，可使用 DryRun：

```bash
cd frontend
npm run dev:all -- -DryRun
```

### 后端集中配置文件

Heimdall 后端支持通过 `HEIMDALL_RUNTIME_CONFIG_PATH` 指向一个单独的 JSON 文件，把原本分散的环境变量统一收口到一个地方管理。

最常见的使用方式如下：

1. 复制示例文件 `scripts/backend.runtime.config.example.json`
2. 按需填写你自己的 Provider、密钥、目录和超时配置
3. 在 `scripts/dev.env` 或部署环境中设置 `HEIMDALL_RUNTIME_CONFIG_PATH`
4. 启动后端或执行 `npm run dev:all`

程序的实际加载顺序如下：

1. ASP.NET Core 默认配置
2. `HEIMDALL_RUNTIME_CONFIG_PATH` 指向的 JSON 文件
3. 实际进程环境变量
4. 命令行参数

可以直接按下面这条规则理解：

- 大部分后端配置都可以写进 JSON 文件
- 如果同一个 Key 同时写在 JSON 文件和环境变量里，以环境变量为准
- `HEIMDALL_RUNTIME_CONFIG_PATH` 自己是“入口配置”，仍然需要放在环境变量里

配置文件建议直接使用 Heimdall 新环境变量名作为 JSON 顶层属性，例如：

```json
{
  "HEIMDALL_DEFAULT_PROVIDER": "openai",
  "HEIMDALL_EMBEDDER_TYPE": "ollama",
  "OPENAI_API_KEY": "your-openai-api-key",
  "OLLAMA_HOST": "http://127.0.0.1:11434"
}
```

## Docker 镜像打包

项目已拆分为两个可独立构建的镜像：

- `heimdall-backend`：`.NET 10` C# 后端
- `heimdall-frontend`：Next.js 前端

### 构建本地镜像

```bash
docker build -f docker/backend/Dockerfile -t heimdall-backend:latest .
docker build -f docker/frontend/Dockerfile -t heimdall-frontend:latest .
```

### 构建并打远端仓库标签

将下面的 `your-registry` 和 `your-namespace` 替换成你自己的镜像仓库地址：

```bash
docker build -f docker/backend/Dockerfile -t your-registry/your-namespace/heimdall-backend:latest .
docker build -f docker/frontend/Dockerfile -t your-registry/your-namespace/heimdall-frontend:latest .
```

### 推送镜像

```bash
docker push your-registry/your-namespace/heimdall-backend:latest
docker push your-registry/your-namespace/heimdall-frontend:latest
```

如果你使用 GitHub Container Registry，也可以采用类似下面的命名：

```bash
ghcr.io/<你的组织或用户名>/heimdall-backend:latest
ghcr.io/<你的组织或用户名>/heimdall-frontend:latest
```

## 使用镜像版 Docker Compose

当前仓库中的 `docker-compose.yml` 已切换为纯镜像模式，不再在 `compose` 中本地构建。

### 第一步：修改镜像名

请先把 `docker-compose.yml` 中的以下占位镜像名替换成你的真实镜像地址：

- `ghcr.io/your-org/heimdall-backend:latest`
- `ghcr.io/your-org/heimdall-frontend:latest`

### 第二步：启动服务

```bash
docker compose pull
docker compose up -d
```

### 第三步：查看状态

```bash
docker compose ps
docker compose logs -f
```

默认端口：

- 前端：`http://localhost:3000`
- C# 后端：`http://localhost:8001`

### 停止服务

```bash
docker compose down
```

如果你希望连缓存卷一起删除，可以执行：

```bash
docker compose down -v
```

## 环境变量说明
 
后端环境变量统一使用 `HEIMDALL_*` 键名。

如果你只是本地开发，可以优先关注下面这几个最常用的 Key：

- `HEIMDALL_RUNTIME_CONFIG_PATH`：让后端知道去哪里读集中配置文件
- `SERVER_BASE_URL`：让前端知道后端地址
- `HEIMDALL_DEFAULT_PROVIDER`：指定默认聊天 Provider
- `HEIMDALL_EMBEDDER_TYPE`：指定 RAG 使用哪种嵌入器
- `OPENAI_API_KEY`、`GOOGLE_API_KEY`、`OLLAMA_HOST` 等：按你实际使用的 Provider 选择性填写

下面的表格按“前端联调、后端公共配置、Provider 密钥”三个维度整理。

### 前端代理与联调

| Key | 含义 | 取值示例 | 是否必须 | 默认值 / 备注 |
| --- | --- | --- | --- | --- |
| `SERVER_BASE_URL` | 前端代理后端接口时使用的后端基地址 | `http://localhost:8001` | 前端联调时建议配置 | 默认 `http://localhost:8001` |

### 后端启动与公共配置

| Key | 含义 | 取值示例 | 是否必须 | 默认值 / 备注 |
| --- | --- | --- | --- | --- |
| `HEIMDALL_RUNTIME_CONFIG_PATH` | 后端运行配置文件路径 | `scripts/backend.runtime.config.json` | 否 | 不可在该 JSON 内再定义自己 |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core 运行环境 | `Development` | 否 | 本地脚本默认 `Development` |
| `ASPNETCORE_URLS` | 后端监听地址 | `http://localhost:8001` | 否 | 本地脚本默认 `http://localhost:8001` |
| `HEIMDALL_AUTH_MODE` | 是否启用授权码校验 | `true` | 否 | `true` / `1` 表示启用；为空或其他值表示关闭 |
| `HEIMDALL_AUTH_CODE` | 授权码内容 | `heimdall-demo-code` | 否 | 开启授权时建议同时设置 |
| `HEIMDALL_DATA_DIR` | Wiki 缓存与项目数据目录 | `D:\Heimdall\data` | 否 | 默认使用程序目录下的 `data` |
| `HEIMDALL_STORAGE_DIR` | 仓库克隆目录与向量缓存目录根路径 | `D:\Heimdall\storage` | 否 | 默认使用程序目录下的 `storage` |
| `HEIMDALL_CONFIG_DIR` | `generator.json`、`embedder.json` 等配置目录 | `D:\Heimdall\config` | 否 | 默认使用程序目录下的 `config` |
| `HEIMDALL_DEFAULT_PROVIDER` | 默认聊天 Provider | `openai` | 否 | 未命中时回退到 `generator.json` 中的默认值 |
| `HEIMDALL_EMBEDDER_TYPE` | RAG 嵌入器类型 | `ollama` | 否 | 可选 `openai`、`google`、`ollama`、`bedrock`，默认 `ollama` |
| `HEIMDALL_HTTP_TIMEOUT_MINUTES` | 后端默认 HttpClient 超时时间（分钟） | `180` | 否 | 默认 `180` |
| `HEIMDALL_WIKI_TASK_TIMEOUT_MINUTES` | 单次 Wiki 任务总超时时间（分钟） | `180` | 否 | 默认 `180` |
| `HEIMDALL_OLLAMA_REQUEST_TIMEOUT_MINUTES` | 单次 Ollama 请求超时时间（分钟） | `60` | 否 | 默认 `60` |
| `OLLAMA_HOST` | Ollama 服务地址 | `http://127.0.0.1:11434` | 使用 Ollama 时必须 | 默认 `http://127.0.0.1:11434` |

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
| `DASHSCOPE_BASE_URL` | DashScope 兼容接口基地址 | `https://dashscope.aliyuncs.com/compatible-mode/v1` | 否 | 默认 `https://dashscope.aliyuncs.com/compatible-mode/v1` |
| `DASHSCOPE_WORKSPACE_ID` | DashScope 工作空间 ID | `ws_1234567890` | 否 | 配置后会附加到请求头 |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI 调用密钥 | `your-key` | 使用 Azure OpenAI 时必须 | 需与 `AZURE_OPENAI_ENDPOINT`、`AZURE_OPENAI_VERSION` 一起配置 |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI 资源地址 | `https://your-resource.openai.azure.com` | 使用 Azure OpenAI 时必须 | 不带具体路径 |
| `AZURE_OPENAI_VERSION` | Azure OpenAI API 版本 | `2024-10-21` | 使用 Azure OpenAI 时必须 | 按 Azure 实际可用版本填写 |
| `AWS_ACCESS_KEY_ID` | AWS 访问密钥 ID | `AKIA...` | 使用 Bedrock 且不走角色链路时必须 | 与 `AWS_SECRET_ACCESS_KEY` 配对使用 |
| `AWS_SECRET_ACCESS_KEY` | AWS 访问密钥 Secret | `abcd...` | 使用 Bedrock 且不走角色链路时必须 | 与 `AWS_ACCESS_KEY_ID` 配对使用 |
| `AWS_SESSION_TOKEN` | AWS 临时会话令牌 | `IQoJ...` | 否 | 使用临时凭证时填写 |
| `AWS_REGION` | AWS 区域 | `us-east-1` | 使用 Bedrock 时建议配置 | 默认 `us-east-1` |
| `AWS_ROLE_ARN` | 需要切换的 AWS 角色 ARN | `arn:aws:iam::123456789012:role/HeimdallBedrockRole` | 否 | 配置后可结合当前凭证执行角色切换 |

## 迁移说明

本仓库已经完成目录治理：

- 主目录只保留 C# 与 Next.js 主逻辑
- 原有 Python 逻辑已完全移除，不再包含任何 Python 运行链路与源码目录
- 前端默认只保留中文界面与中文文案

## 验证命令

```bash
cd frontend
npm run build
npm run lint
```

如本地已安装 `.NET 10`，还可以执行：

```bash
dotnet build backend/Heimdall.Api/Heimdall.Api.csproj
```

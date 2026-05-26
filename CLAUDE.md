# CLAUDE.md

## 项目概况

Heimdall — 把代码仓库自动转换为中文 Wiki、问答、演示文稿与训练营材料。

- 后端：C# / ASP.NET Core / `.NET 10`
- 前端：Next.js 16 (App Router)
- 数据库：PostgreSQL + pgvector
- ORM：SqlSugar（CodeFirst 自动同步，无迁移文件）
- AI 抽象：Microsoft.Extensions.AI（MEAI）`IChatClient`
- 开发平台：Windows 11 / PowerShell 7+（macOS 通过 `dev.sh` 兼容）

## 调试与开发工作流

### 环境变量（唯一来源：`scripts/dev.env`）

所有调试凭据统一存放在 `scripts/dev.env`（已 gitignore），启动脚本自动加载，**命令行中不出现密码明文**。

从 `scripts/dev.env.example` 复制并填入真实值：
```bash
cp scripts/dev.env.example scripts/dev.env
# 编辑 scripts/dev.env，填入数据库连接串、Provider 密钥等
```

### 脚本体系（均在 `scripts/` 目录）

| 脚本 | 平台 | 用途 |
|------|------|------|
| `dev.sh` | macOS/Linux | 加载 `dev.env` → 启动后端 → 等待就绪 → 启动前端 |
| `dev.ps1` | Windows | 同上（支持 DryRun / BackendOnly / FrontendOnly） |
| `dev-start.ps1` | Windows | 一键启动：检查 PostgreSQL → 生成 .env → 启动前后端 |
| `setup-env.ps1` | Windows | 交互式配置 Provider 密钥 |
| `dev-reset.ps1` | Windows | 清空 Wiki 缓存、任务、索引（保留仓库和用户） |
| `dev-stop.ps1` | Windows | 优雅停止前后端进程 |

### 日常调试

```bash
# macOS / Linux
bash scripts/dev.sh

# Windows
.\scripts\dev-start.ps1

# 仅后端
bash scripts/dev.sh --backend-only
# 预览启动（不实际执行）
.\scripts\dev.ps1 -DryRun
```

### AI 工具（Claude Code）调试

- 环境变量通过 `scripts/dev.env` 注入，脚本自动加载
- 命令行中不出现密码明文，不会被 auto-mode 拦截
- `dev.sh` / `dev.ps1` 是唯一启动入口，优先使用
- CodeFirst 开关 `HEIMDALL_CODEFIRST_AUTOSYNC` 控制启动时是否自动同步表结构
- `.claude/settings.local.json` 管理 AI 工具权限

## 架构约束

```
Heimdall.Api (API 层)         →  控制器、DTO、中间件
    ↓
Heimdall.Core (业务层)        →  实体、业务接口/实现、领域模型
    ↓
Heimdall.Repository (数据层)  →  SqlSugar ORM、仓储实现
    ↘              ↙
Heimdall.Infrastructure (工具层) →  MEAI IChatClient Provider、配置、仓库源、文本工具
```

依赖规则：Api → Core → Repository；全部 → Infrastructure。Core 不依赖 Api。层间通过接口通信，DI 注入。

### V9 关键架构变更

- **ORM**：EF Core → SqlSugar（`ISqlSugarClient` 注入仓储层，Singleton 生命周期）
- **Provider 抽象**：自研 `IChatProvider` → MEAI `IChatClient`（统一通过 Keyed DI 获取）
- **Provider 实现**：5 个 OpenAI 兼容走 `OpenAiCompatibleClientFactory`，Ollama/Gemini/MiniMax 自定义适配器
- **流式**：`IChatClient.GetStreamingResponseAsync()` 真流式 SSE
- **CodeFirst**：启动时 `CodeFirstSyncService` 自动同步表结构，替代 EF Core 迁移

## 修改惯例

- 所有新增文档、注释、说明文字必须使用中文
- C# 运行时固定为 `.NET 10`
- 不得引入 Python 业务代码
- 新服务需在 `Program.cs` 中注册 DI
- 不要删除数据库已有表（通过 SqlSugar CodeFirst 增量同步）
- 不要创建 Core → Api 方向的项目引用

## 核心目录

| 目录 | 用途 |
|------|------|
| `backend/Heimdall.Api/Controllers/` | API 控制器 |
| `backend/Heimdall.Core/Services/` | 业务服务实现 |
| `backend/Heimdall.Core/Interfaces/` | 接口定义 |
| `backend/Heimdall.Core/Entities/` | 领域实体（SqlSugar `[SugarTable]` 标注） |
| `backend/Heimdall.Infrastructure/Providers/` | MEAI IChatClient Provider 适配 |
| `backend/Heimdall.Infrastructure/Providers/CustomBackends/` | 自定义适配器（Ollama/Gemini/MiniMax） |
| `backend/Heimdall.Repository/Repositories/` | 数据访问（注入 `ISqlSugarClient`） |
| `frontend/src/app/` | Next.js 页面与 API 路由 |
| `frontend/src/components/` | 前端组件 |
| `frontend/src/hooks/` | 自定义 Hook |
| `scripts/` | 开发调试脚本 |

## 验证命令

```bash
# 后端构建
dotnet build backend/Heimdall.Api/Heimdall.Api.csproj

# 前端构建与 Lint
cd frontend && npm run build && npm run lint
```

## Wiki 生成管线（8 阶段）

仓库准备 → 代码索引（Tree-sitter AST + BM25）→ 代码理解 → 结构规划（三策略）→ 页面生成（BM25+pgvector 混合检索注入）→ 质量审查（含弱页重生成）→ 渲染后处理 → 持久化

核心文件：
- `backend/Heimdall.Core/Services/Tasks/WikiTaskService.cs` — 管线编排
- `backend/Heimdall.Core/Services/Tasks/TaskPromptService.cs` — 提示词构建
- `backend/Heimdall.Core/Services/Repository/CodeIndexService.cs` — Tree-sitter AST 代码索引
- `backend/Heimdall.Infrastructure/Search/Bm25SearchService.cs` — BM25 检索
- `backend/Heimdall.Core/Services/Tasks/DeterministicStructurePlanner.cs` — 结构规划（Deterministic/LlmEnhanced）

架构设计文档：[`docs/architecture/architecture.md`](docs/architecture/architecture.md)

# CLAUDE.md

## 项目概况

Heimdall — 把代码仓库自动转换为中文 Wiki、问答、演示文稿与训练营材料。

- 后端：C# / ASP.NET Core / `.NET 10`
- 前端：Next.js 16 (App Router)
- 数据库：PostgreSQL + pgvector
- ORM：SqlSugar（CodeFirst 自动同步，无迁移文件）
- AI 抽象：Microsoft.Extensions.AI（MEAI）`IChatClient`
- 代码分析：Tree-sitter AST（20+ 语言）+ BM25 全文检索
- 开发平台：Windows 11 / PowerShell 7+（macOS 通过 `dev.sh` 兼容）
- 规范驱动：OpenSpec（`openspec/` 目录，26 个域 spec + 活跃 change 工作流）

## 调试与开发工作流

### 环境变量（唯一来源：`scripts/dev.env`）

所有调试凭据统一存放在 `scripts/dev.env`（已 gitignore），启动脚本自动加载，**命令行中不出现密码明文**。

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
- CodeFirst 开关 `HEIMDALL_CODEFIRST_AUTOSYNC` 控制启动时是否自动同步表结构
- `.claude/settings.local.json` 管理 AI 工具权限
- OpenSpec 工作流通过 `.claude/commands/opsx/` 和 `.claude/skills/` 集成

## 架构约束

```
Heimdall.Api (API 层)         →  控制器、DTO、中间件
    ↓
Heimdall.Core (业务层)        →  实体、业务接口/实现、领域模型
    ↓
Heimdall.Repository (数据层)  →  SqlSugar ORM、仓储实现
    ↘              ↙
Heimdall.Infrastructure (工具层) →  MEAI IChatClient Provider、配置、仓库源、BM25 搜索、文本工具
```

依赖规则：Api → Core → Repository；全部 → Infrastructure。Core 不依赖 Api。层间通过接口通信，DI 注入。

### V9–V11 关键架构变更

- **ORM**：EF Core → SqlSugar（`ISqlSugarClient` 注入仓储层，Singleton 生命周期）
- **Provider 抽象**：自研 `IChatProvider` → MEAI `IChatClient`（统一通过 Keyed DI 获取）
- **Provider 实现**：10 个 Provider（OpenAI/Azure/Bedrock/Ollama/Gemini/MiniMax/OpenRouter/DashScope/DeepSeek），OpenAI 兼容走 `OpenAiCompatibleClientFactory`，Ollama/Gemini/MiniMax 自定义适配器
- **流式**：`IChatClient.GetStreamingResponseAsync()` 真流式 SSE
- **CodeFirst**：启动时 `CodeFirstSyncService` 自动同步表结构，替代 EF Core 迁移
- **FunctionInvokingChatClient**：所有 Provider 管道注册 `UseFunctionInvocation()`，自动处理 Tool Call 往返（最大 8 轮，支持并发调用）
- **Tool Call 配置**：`ToolCallConfigurationService` 统一管理 Stage 3/Stage 5 的 Tool Call 开关
- **Workspace 文件系统**：大文本（Wiki 页面 Markdown、AST CST S-expression、Wiki 结构 JSON）从 DB 迁移到 `workspace/` 文件存储，DB 仅保留路径引用
- **AST 版本化**：`AstVersion` 实体与 `RepositoryVersion` 关联，AST 解析结果持久化到 `workspace/ast/{version_id}/`，支持跨版本复用
- **提示词 DB 化**：`TaskPromptService` 从 DB `prompt_templates` 加载提示词，通过 `IPromptMergeService` 五层拼装（角色→上下文→指令→约束→自查清单）

## 修改惯例

- 所有新增文档、注释、说明文字必须使用中文
- C# 运行时固定为 `.NET 10`
- 不得引入 Python 业务代码
- 新服务需在 `Program.cs` 中注册 DI
- 不要删除数据库已有表（通过 SqlSugar CodeFirst 增量同步）
- 不要创建 Core → Api 方向的项目引用
- 不得引入 `Microsoft.EntityFrameworkCore.*` 包
- 所有 LLM 调用通过 MEAI `IChatClient`，不得引入自研 `IChatProvider`

## 核心目录

| 目录 | 用途 |
|------|------|
| `backend/Heimdall.Api/Controllers/` | API 控制器 |
| `backend/Heimdall.Core/Services/` | 业务服务实现 |
| `backend/Heimdall.Core/Services/Tasks/` | 管线编排（WikiTaskService、AgentOrchestratorService） |
| `backend/Heimdall.Core/Services/Repository/` | 代码分析（CodeIndexService、TreeSitterAnalyzer） |
| `backend/Heimdall.Core/Interfaces/` | 接口定义 |
| `backend/Heimdall.Core/Entities/` | 领域实体（SqlSugar `[SugarTable]` 标注） |
| `backend/Heimdall.Infrastructure/Providers/` | MEAI IChatClient Provider 适配 + 工厂 |
| `backend/Heimdall.Infrastructure/Providers/CustomBackends/` | 自定义适配器（Ollama/Gemini/MiniMax） |
| `backend/Heimdall.Infrastructure/Search/` | BM25 搜索引擎 |
| `backend/Heimdall.Repository/Repositories/` | 数据访问（注入 `ISqlSugarClient`） |
| `backend/Heimdall.Repository/Data/SeedScripts/` | SQL 种子脚本（提示词、扩展） |
| `frontend/src/app/` | Next.js 页面与 API 路由 |
| `frontend/src/components/` | 前端组件 |
| `frontend/src/hooks/` | 自定义 Hook |
| `frontend/src/contexts/` | Auth/Language/Repository 上下文 |
| `scripts/` | 开发调试脚本 |
| `openspec/` | OpenSpec 规范驱动（26 个域 spec + changes/） |
| `docs/architecture/` | 系统架构设计文档（12 个专题） |
| `workspace/` | 运行时文件存储（ast/、wiki/、repos/、artifacts/、logs/、cache/） |

## 验证命令

```bash
# 后端构建
dotnet build backend/Heimdall.Api/Heimdall.Api.csproj

# 前端构建与 Lint
cd frontend && npm run build && npm run lint
```

## Wiki 生成管线（8 阶段）

仓库准备 → 代码索引（Tree-sitter AST + BM25）→ 代码理解（Stage 3，可选 Tool Call 增强）→ 结构规划（三策略：LlmJson/Deterministic/LlmEnhanced）→ 页面生成（Stage 5，BM25 检索注入 + 可选 Tool Call 增强）→ 质量审查（算法评分 + 弱页重生成）→ 渲染后处理 → 持久化（Workspace 文件 + DB 元数据）

核心文件：
- `backend/Heimdall.Core/Services/Tasks/WikiTaskService.cs` — 8 阶段管线编排
- `backend/Heimdall.Core/Services/Tasks/TaskPromptService.cs` — 提示词构建（DB 驱动）
- `backend/Heimdall.Core/Services/Repository/CodeIndexService.cs` — Tree-sitter AST 代码索引
- `backend/Heimdall.Core/Services/Repository/TreeSitterAnalyzer.cs` — AST 解析（10 字段符号提取 + 调用图 + 设计模式检测）
- `backend/Heimdall.Infrastructure/Search/Bm25SearchService.cs` — BM25 检索
- `backend/Heimdall.Core/Services/Tasks/DeterministicStructurePlanner.cs` — 结构规划（Deterministic/LlmEnhanced）
- `backend/Heimdall.Core/Services/Tasks/AgentOrchestratorService.cs` — 大仓库子代理协调（检测已就绪，分发待激活）
- `backend/Heimdall.Core/Services/Workspace/WorkspaceService.cs` — Workspace 文件系统管理

架构设计文档：[`docs/architecture/architecture.md`](docs/architecture/architecture.md)
OpenSpec 域规格：[`openspec/specs/`](openspec/specs/)

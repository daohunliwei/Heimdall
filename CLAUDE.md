# CLAUDE.md

## 项目概况

Heimdall — 把代码仓库自动转换为中文 Wiki、问答、演示文稿与训练营材料。

- 后端：C# / ASP.NET Core / `.NET 10`
- 前端：Next.js 16 (App Router)
- 数据库：PostgreSQL + pgvector
- 开发平台：Windows 11 / PowerShell 7+

## 调试与开发工作流

### 脚本体系（均在 `scripts/` 目录）

| 脚本 | 用途 |
|------|------|
| `dev-start.ps1` | 一键启动：检查 PostgreSQL → 生成 .env → 迁移 → 启动前后端 |
| `setup-env.ps1` | 交互式配置 Provider 密钥，生成 `.env` 文件 |
| `dev-reset.ps1` | 清空 Wiki 缓存、任务、索引（保留仓库和用户） |
| `dev-stop.ps1` | 优雅停止前后端进程 |
| `dev.ps1` | 经典启动脚本（带 DryRun / BackendOnly 等参数） |

### 常见调试流程

```
# 首次设置
.\scripts\setup-env.ps1          # 填写密钥，生成 .env

# 日常调试
.\scripts\dev-reset.ps1 -Force   # 清空数据，恢复到干净状态
.\scripts\dev-start.ps1          # 一键启动

# 调试结束后停止服务
.\scripts\dev-stop.ps1
```

### AI 工具（Claude Code）辅助调试注意事项

- 环境变量通过 `.env` 文件注入，脚本自动加载，不需要在终端手动 export
- `.env` 已在 `.gitignore` 中，不会提交到仓库
- 不要直接 echo/printf 密钥到终端
- 运行 `dotnet` 或 `npm` 命令前确保 `.env` 已加载（执行 `dev-start.ps1` 自动处理）
- 后端启动后会自动执行数据库迁移，无需手动 ef 命令

## 架构约束

```
Heimdall.Api (API 层)         →  控制器、DTO、中间件
    ↓
Heimdall.Core (业务层)        →  实体、业务接口/实现、领域模型
    ↓
Heimdall.Repository (数据层)  →  EF Core、仓储、迁移、向量查询
    ↘              ↙
Heimdall.Infrastructure (工具层) →  Provider、配置、仓库源、文本工具
```

依赖规则：Api → Core → Repository；全部 → Infrastructure。Core 不依赖 Api。层间通过接口通信，DI 注入。

## 修改惯例

- 所有新增文档、注释、说明文字必须使用中文
- C# 运行时固定为 `.NET 10`
- 不得引入 Python 业务代码
- 数据层变更需生成 EF Core 迁移
- 新服务需在 `Program.cs` 中注册 DI
- 不要删除数据库已有表（只做增量迁移）
- 不要创建 Core → Api 方向的项目引用

## 核心目录

| 目录 | 用途 |
|------|------|
| `backend/Heimdall.Api/Controllers/` | API 控制器 |
| `backend/Heimdall.Core/Services/` | 业务服务实现 |
| `backend/Heimdall.Core/Interfaces/` | 接口定义 |
| `backend/Heimdall.Core/Entities/` | 领域实体 |
| `backend/Heimdall.Infrastructure/Providers/` | LLM Provider 适配 |
| `backend/Heimdall.Repository/Data/` | AppDbContext |
| `backend/Heimdall.Repository/Repositories/` | 数据访问 |
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

# 数据库迁移
dotnet ef migrations add <Name> --project backend/Heimdall.Repository --startup-project backend/Heimdall.Api
dotnet ef database update --project backend/Heimdall.Repository --startup-project backend/Heimdall.Api
```

## Wiki 生成管线（10 阶段）

仓库准备 → 代码索引（本地，无 LLM）→ 深度代码理解 → 结构规划 → 页面生成（混合检索注入）→ 质量审查 → 渲染后处理 → 持久化 → 向量嵌入 → 完成

核心文件：
- `backend/Heimdall.Core/Services/Tasks/WikiTaskService.cs` — 管线编排
- `backend/Heimdall.Core/Services/Tasks/TaskPromptService.cs` — 提示词构建
- `backend/Heimdall.Core/Services/Search/HybridSearchService.cs` — BM25 + 向量混合检索

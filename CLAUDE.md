## 优先  

请优先查看`AGENTS.md`文件

## 架构速览

四层分离架构（Api → Core → Repository，全部 → Infrastructure）：

| 层 | 项目 | 职责 |
|----|------|------|
| API | `Heimdall.Api` | 控制器、DTO、中间件、Mappings |
| 业务 | `Heimdall.Core` | 实体、服务接口与实现 |
| 数据 | `Heimdall.Repository` | EF Core DbContext、仓储、迁移 |
| 工具 | `Heimdall.Infrastructure` | Provider 适配、配置、仓库源 |

## 当前分支

`feature/chonggou` — 架构重构分支（四层分离 + PostgreSQL/pgvector + JWT/RBAC + 管理后台）

## 数据库

- 生产库：`ai_heimdall_base` @ `10.189.10.252:5432`
- 本地开发：`docker compose up -d postgres`
- 迁移项目：`backend/Heimdall.Repository`
- 启动项目：`backend/Heimdall.Api`

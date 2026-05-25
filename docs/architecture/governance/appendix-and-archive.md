# Heimdall 架构专题：附录与归档

> 文档类型：专题文档
>
> 所属分组：治理
>
> 最后更新：2026-05-25
>
> 返回入口页：[`architecture.md`](../architecture.md)
>
> 顺序导航：上一篇 [`演进路线图`](../governance/evolution-roadmap.md) ｜ 下一篇 无，已到当前阅读序列末尾

## 文档范围

本文集中承载不适合放入主叙事正文、但又需要长期保留的支撑信息，包括核心技术依赖、调试工作流以及历史架构文档归档说明。

## 核心职责

| 主题 | 内容 |
|------|------|
| 技术依赖 | 记录当前后端与前端关键依赖及其职责 |
| 调试工作流 | 汇总常用开发脚本、构建命令与本地启动方式 |
| 历史归档 | 说明哪些旧文档已被新专题体系替代 |

## 关键结构

### 技术依赖摘要

| 类型 | 依赖 | 用途 |
|------|------|------|
| NuGet | `SqlSugarCore` | ORM 与 CodeFirst |
| NuGet | `Microsoft.Extensions.AI` | 标准化模型调用抽象 |
| NuGet | `Microsoft.Extensions.AI.OpenAI` | OpenAI 兼容 Provider 接入 |
| NuGet | `TreeSitter.DotNet` | 跨语言 AST 解析 |
| NuGet | `Swashbuckle.AspNetCore.Swagger` | API 文档 |
| npm | `next` | 前端应用框架 |
| npm | `react` | 组件与交互基础 |
| npm | `tailwindcss` | 样式体系 |
| npm | `mermaid` | 图表渲染 |
| npm | `katex` | 数学公式渲染 |

### 调试工作流

| 主题 | 当前约定 | 维护目的 |
|------|------|------|
| 启动入口 | Windows 优先使用 `scripts/dev-start.ps1` / `scripts/dev.ps1`，macOS/Linux 使用 `scripts/dev.sh` | 保证本地环境启动方式一致 |
| 敏感配置 | 统一通过 `scripts/dev.env` 注入 | 避免密码和密钥在命令行明文暴露 |
| 后端验证 | `dotnet build backend/Heimdall.Api/Heimdall.Api.csproj` | 快速确认后端编译与引用关系正常 |
| 前端验证 | `npm run build`、`npm run lint` | 统一前端构建与风格校验入口 |

1. 本地调试统一优先使用 `scripts/dev-start.ps1`、`scripts/dev.ps1` 或 `scripts/dev.sh`。
2. 所有敏感配置通过 `scripts/dev.env` 注入，不在命令行中明文展开密码和密钥。
3. 后端构建命令使用 `dotnet build backend/Heimdall.Api/Heimdall.Api.csproj`。
4. 前端验证命令以 `npm run build` 和 `npm run lint` 为主。

### 历史文档归档说明

当前专题化架构文档体系已经替代旧的单文件或阶段性升级方案文档。后续新增架构说明时，应优先判断是补充到既有专题，还是在 `governance/` 下沉淀新的 ADR/演进记录，而不是重新创建平行的总文档。

## 关键流程

### 附录更新与归档维护流程

1. 当技术栈、开发脚本或调试入口发生稳定变更时，先确认该信息是否属于“长期支撑信息”，而不是某次临时操作说明。
2. 若属于长期事实，则优先更新本文中的技术依赖、调试工作流或归档说明，并同步检查 `AGENTS.md`、`CLAUDE.md` 与脚本说明是否一致。
3. 当出现新的专题文档或旧文档退场时，应先在对应专题中落权威正文，再在本文补充归档与迁移说明，避免附录反向承载主叙事。
4. 如果某项内容已经不再稳定、只在某个专题内部生效，应将其移回对应专题正文，而不是继续堆积在附录中。

## 依赖关系

| 依赖项 | 说明 |
|------|------|
| `CLAUDE.md` / `AGENTS.md` | 调试工作流与仓库约束的事实来源 |
| 入口页与专题文档 | 历史归档说明需要与当前文档体系保持一致 |
| 开发脚本 | 启停、重置、环境变量加载流程都依赖 `scripts/` 下脚本 |

## 设计取舍

| 取舍点 | 当前选择 | 理由 |
|------|------|------|
| 附录位置 | 独立专题承载支撑信息 | 避免入口页和运行时文档被大量附加信息稀释 |
| 归档方式 | 明确“旧文档被新专题替代” | 让读者知道权威事实应到哪里查 |
| 调试说明 | 只保留稳定入口和关键命令 | 细节依旧以脚本和仓库根文档为准，避免重复维护 |

## 导航与关联阅读

### 返回入口

- [`architecture.md`](../architecture.md)

### 顺序导航

- 上一篇：[`演进路线图`](../governance/evolution-roadmap.md)
- 下一篇：无，已到当前阅读序列末尾

### 关联阅读

- [`governance/evolution-roadmap.md`](./evolution-roadmap.md)
- [`persistence/configuration-and-env.md`](../persistence/configuration-and-env.md)
- [`overview/system-overview.md`](../overview/system-overview.md)
- [`../split-plan.md`](../split-plan.md)

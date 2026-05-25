# Heimdall 架构专题：配置与环境变量

> 文档类型：专题文档
>
> 所属分组：持久化
>
> 最后更新：2026-05-25
>
> 返回入口页：[`architecture.md`](../architecture.md)
>
> 顺序导航：上一篇 [`数据库设计`](../persistence/database-design.md) ｜ 下一篇 [`架构决策`](../governance/architecture-decisions.md)

## 文档范围

本文说明 Heimdall 的配置加载来源、优先级、关键环境变量分组、配置文件入口和运行时覆盖策略，帮助开发、调试与部署人员判断一个配置值最终从哪里生效。

## 核心职责

| 配置域 | 主要载体 | 职责 |
|------|------|------|
| 基础运行 | `appsettings.json`、命令行参数 | 提供应用默认值与启动时覆盖 |
| 环境注入 | `HEIMDALL_*`、Provider 密钥 | 为不同环境注入数据库、认证和模型配置 |
| 结构化配置文件 | `backend/Heimdall.Api/config/*.json` | 提供生成器、嵌入器、语言和仓库过滤规则 |
| 运行时治理 | `system_settings`、后台设置页 | 对部分可热更新配置进行平台级管理 |

## 关键结构

```mermaid
flowchart LR
    Cmd[命令行参数] --> Env[环境变量]
    Env --> Runtime[运行时 JSON 配置]
    Runtime --> AppSettings[appsettings.json]
```

### 核心配置分组

| 分组 | 关键项 | 主要影响 |
|------|------|------|
| 数据库 | `HEIMDALL_CONNECTION_STRING` | 决定后端数据库连接与启动可用性 |
| 认证 | `HEIMDALL_AUTH_MODE`、`HEIMDALL_JWT_SECRET` | 决定是否启用 JWT 与签名密钥 |
| ORM | `HEIMDALL_CODEFIRST_AUTOSYNC` | 决定启动时是否自动同步表结构 |
| 生成策略 | `HEIMDALL_STRUCTURE_STRATEGY`、`HEIMDALL_QUALITY_REGEN_ENABLED` | 决定结构规划和质量补强策略 |
| 大模型 | `HEIMDALL_DEFAULT_PROVIDER`、`HEIMDALL_DEFAULT_MODEL`、各类 API Key | 决定默认模型与可用 Provider |
| 调试与观测 | `HEIMDALL_DEBUG_MODE`、`HEIMDALL_LOG_SQL`、`HEIMDALL_TOKEN_ESTIMATION_MODE` | 决定调试便利性和可观测性 |

## 关键流程

### 1. 启动时配置合并

1. 应用先读取 `appsettings.json` 作为最低优先级默认值。
2. 如果指定运行时配置文件路径，则加载对应 JSON 并覆盖默认值。
3. 环境变量继续覆盖前述值，适合部署和本地调试时注入敏感配置。
4. 命令行参数拥有最高优先级，通常用于临时覆盖。

### 2. Provider 与检索配置生效链路

- `generator.json` 定义可用 Chat Provider 与模型集合。
- `embedder.json` 定义嵌入器、检索和向量维度相关配置。
- `repo.json` 决定仓库文件过滤、忽略规则等行为。
- `lang.json` 决定前端语言配置入口，当前实际支持中文。

## 依赖关系

| 依赖项 | 说明 |
|------|------|
| AI Provider 架构 | 默认 Provider、模型与密钥全来自配置体系 |
| Wiki 管线 | 结构规划、大仓库并发、质量补强等行为由配置控制 |
| 前端 BFF | `SERVER_BASE_URL` 等配置决定代理目标 |
| 数据库设计 | CodeFirst 自动同步与连接串决定持久化是否可用 |

## 设计取舍

| 取舍点 | 当前选择 | 理由 |
|------|------|------|
| 配置优先级 | 命令行 > 环境变量 > 运行时 JSON > 默认配置 | 兼顾安全性、部署灵活性与默认值可维护性 |
| 敏感信息管理 | 密钥走环境变量，不写入仓库 | 减少泄漏风险，符合本地脚本注入模式 |
| 配置拆分 | 结构化 JSON 文件承载主题配置 | 便于独立维护模型、语言和仓库规则 |
| 治理方式 | 静态配置与后台设置并存 | 既保留代码级默认值，也支持运行时调整 |

## 导航与关联阅读

### 返回入口

- [`architecture.md`](../architecture.md)

### 顺序导航

- 上一篇：[`数据库设计`](../persistence/database-design.md)
- 下一篇：[`架构决策`](../governance/architecture-decisions.md)

### 关联阅读

- [`runtime/ai-provider-architecture.md`](../runtime/ai-provider-architecture.md)
- [`runtime/frontend-architecture.md`](../runtime/frontend-architecture.md)
- [`persistence/database-design.md`](./database-design.md)
- [`governance/appendix-and-archive.md`](../governance/appendix-and-archive.md)

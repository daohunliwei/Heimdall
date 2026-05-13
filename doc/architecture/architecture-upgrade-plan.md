# Heimdall 全面架构升级方案

## 背景与动机

Heimdall 当前是一个 AI 智能生成仓库 Wiki 的 MVP 服务，已实现核心的 Wiki 生成、问答、幻灯片、工作坊四大任务能力，支持 8 种 LLM Provider 可插拔。但作为 MVP，存在以下 8 项结构性不足需要全面升级：

1. **无后台仓储数据库**，没有任务系统，对重复请求无法合并和验证
2. **生成 Wiki 结果的结构很复杂**，脱离系统后不可读
3. **对仓库类型有强耦合逻辑**，对仓库地址有强耦合逻辑
4. **无管理能力**，无全局统控设置和管理能力
5. **无用户系统**，无角色划分和权限控制
6. **前端无法适应长时异步任务**，UI 渲染错乱
7. **无法按任务或者按仓库注入提示词或者 Workflow 要求**，无系统提示词和生成提示词等各种提示词分层设计
8. **无真实向量库**，依靠本地文件系统性能低下准确率不足

## 当前架构摘要

| 维度 | 现状 |
|------|------|
| **技术栈** | C# / ASP.NET Core (.NET 10) + Next.js 16 (App Router) |
| **服务注册** | 全部 Singleton，Program.cs 中手动注册 |
| **数据存储** | 文件系统 JSON（`data/wikicache/*.json`、`storage/databases/*.json`） |
| **仓库访问** | `RepositoryAccessService` 硬编码 GitHub/GitLab/Bitbucket 三种 API + git clone |
| **Provider** | 8 种 Chat Provider、4 种 Embedding Provider，通过接口注入 |
| **任务类型** | Wiki 生成、Ask 问答（含 DeepResearch）、Slides 幻灯片、Workshop 工作坊 |
| **RAG 管道** | 仓库文件 → 文本切分 → 嵌入向量 → JSON 缓存 → 余弦相似度检索 |
| **认证** | 简单 auth_code 比对，无用户身份 |
| **前端配置** | URL Query 参数传递、localStorage 缓存、Next.js BFF 代理 |

---

## 目标架构全景

```
┌─────────────────────────────────────────────────────────────┐
│  前端层 (Next.js 16)                                        │
│  ┌────────────┐ ┌──────────────┐ ┌──────────────────────┐  │
│  │ 管理后台    │ │ 仓库 Wiki 页  │ │ 任务进度 SSE 订阅     │  │
│  │ /admin/*   │ │ /[owner]/[repo]│ │ EventSource 监听     │  │
│  └────────────┘ └──────────────┘ └──────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│  API 网关层 (ASP.NET Core Middleware)                       │
│  ┌────────────┐ ┌──────────────┐ ┌──────────────────────┐  │
│  │ JWT Bearer │ │ RBAC 中间件   │ │ 请求去重 / 合并       │  │
│  │ Token 认证  │ │ Admin/Editor/│ │ hash(repo+type+cfg)  │  │
│  │            │ │ Viewer 三级   │ │                      │  │
│  └────────────┘ └──────────────┘ └──────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│  业务服务层                                                 │
│  ┌──────────────┐ ┌────────────┐ ┌──────────────────────┐  │
│  │ 任务编排      │ │ Prompt 服务 │ │ 仓库抽象层            │  │
│  │ Channel 队列  │ │ 4 层模板    │ │ IRepositorySource    │  │
│  │ BackgroundSvc │ │ DB 存储     │ │ GitHub/GitLab/       │  │
│  │ SSE 进度推送  │ │ 变量替换    │ │ Bitbucket/Local      │  │
│  └──────────────┘ └────────────┘ └──────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│  数据与 AI 层                                               │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────────┐    │
│  │ PostgreSQL   │ │ pgvector     │ │ Provider 适配层   │    │
│  │ 用户/任务/   │ │ 代码向量存储  │ │ IChatProvider     │    │
│  │ 仓库/设置    │ │ 余弦相似搜索  │ │ IEmbeddingProvider│    │
│  └──────────────┘ └──────────────┘ └──────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

---

## 分阶段实施计划

本方案分为 6 个独立阶段，每个阶段完成后可独立合并部署，不阻塞后续阶段。

---

### 第一阶段：数据基础设施

**解决不足 #1（无数据库+任务系统）和 #8（无真实向量库）**

#### 1.1 引入 PostgreSQL + pgvector

新增 `backend/Heimdall.Infrastructure` 项目，承载数据访问层。

**NuGet 依赖**：
- `Npgsql.EntityFrameworkCore.PostgreSQL`
- `Npgsql.EntityFrameworkCore.PostgreSQL.Vector`（pgvector 支持）

**核心数据表**：

```sql
-- 用户表
CREATE TABLE users (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username      VARCHAR(64) NOT NULL UNIQUE,
    email         VARCHAR(256),                            -- 用户邮箱
    password_hash VARCHAR(256),
    source        SMALLINT NOT NULL DEFAULT 0,            -- 0=本地用户, 1=LDAP用户
    role          VARCHAR(16) NOT NULL DEFAULT 'Viewer',  -- Admin / Editor / Viewer
    is_active     BOOLEAN NOT NULL DEFAULT TRUE,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 仓库表
CREATE TABLE repositories (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    owner           VARCHAR(128) NOT NULL,
    repo_name       VARCHAR(128) NOT NULL,
    repo_type       VARCHAR(16) NOT NULL,  -- github / gitlab / bitbucket / local
    repo_url        TEXT,
    clone_url       TEXT,
    default_branch  VARCHAR(128) DEFAULT 'main',
    default_language VARCHAR(8) DEFAULT 'zh',
    description     TEXT,                  -- 仓库描述
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(owner, repo_name, repo_type)
);

-- 任务表
CREATE TABLE tasks (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    task_type         VARCHAR(16) NOT NULL,  -- wiki / ask / slides / workshop
    status            VARCHAR(16) NOT NULL DEFAULT 'pending',  -- pending/running/completed/failed/cancelled
    repository_id     UUID REFERENCES repositories(id),
    source_branch     VARCHAR(128) NOT NULL DEFAULT 'main',  -- 任务针对的分支
    user_id           UUID REFERENCES users(id),
    request_hash      VARCHAR(64) NOT NULL,  -- SHA256 去重键
    provider          VARCHAR(32),
    model             VARCHAR(64),
    language          VARCHAR(8),
    progress_percent  INT DEFAULT 0,
    progress_message  TEXT,
    total_prompt_tokens   INT DEFAULT 0,     -- 本次任务累计 Prompt Token
    total_completion_tokens INT DEFAULT 0,   -- 本次任务累计 Completion Token
    result_json       JSONB,
    error_message     TEXT,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at        TIMESTAMPTZ,
    completed_at      TIMESTAMPTZ
);
-- 并发控制：同一个仓库+分支，同一时间仅允许一个任务 running
CREATE UNIQUE INDEX idx_one_running_task_per_repo_branch
    ON tasks (repository_id, source_branch)
    WHERE status = 'running';
-- 去重：同一个仓库+分支+任务类型，同一时间仅允许一个 pending 任务
CREATE UNIQUE INDEX idx_one_pending_task_per_repo_branch_type
    ON tasks (repository_id, source_branch, task_type)
    WHERE status = 'pending';

-- Wiki 领域表（一个 Wiki 包含 N 个 WikiPage）
CREATE TABLE wikis (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    title               TEXT NOT NULL,                   -- Wiki 标题，默认取仓库名
    description         TEXT,                             -- Wiki 描述，默认取仓库描述
    source_repository_id UUID NOT NULL REFERENCES repositories(id),
    source_branch       VARCHAR(128) NOT NULL DEFAULT 'main',
    language            VARCHAR(8) DEFAULT 'zh',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(source_repository_id, source_branch, language)  -- 同一仓库+分支+语言仅一份 Wiki
);

-- Wiki 页面表（Wiki 的子对象）
CREATE TABLE wiki_pages (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    wiki_id         UUID NOT NULL REFERENCES wikis(id) ON DELETE CASCADE,
    task_id         UUID REFERENCES tasks(id),          -- 记录由哪个 task 生成（可追溯）
    page_order      INT NOT NULL DEFAULT 0,
    title           TEXT NOT NULL,
    content_markdown TEXT,
    parent_page_id  UUID REFERENCES wiki_pages(id),
    importance      VARCHAR(8) DEFAULT 'medium',
    file_paths      TEXT[],
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- LLM 交互审计表（记录 task 与 LLM 的每次交互）
CREATE TABLE task_llm_call_logs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    task_id             UUID NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    step_order          INT NOT NULL,                   -- 步骤序号
    call_type           VARCHAR(32) NOT NULL,           -- structure_generation / page_generation / rag_query / deep_research / slide_generation / workshop_generation
    provider            VARCHAR(32),
    model               VARCHAR(64),
    prompt_tokens       INT DEFAULT 0,
    completion_tokens   INT DEFAULT 0,
    total_tokens        INT DEFAULT 0,                   -- prompt_tokens + completion_tokens
    request_preview     TEXT,                             -- 请求摘要（截断存储）
    response_preview    TEXT,                             -- 响应摘要（截断存储）
    latency_ms          INT,                             -- 本次调用耗时
    is_error            BOOLEAN DEFAULT FALSE,
    error_message       TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX idx_task_llm_call_logs_task ON task_llm_call_logs (task_id, step_order);

-- 嵌入文档表（pgvector）
CREATE TABLE embedding_documents (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    repository_id   UUID NOT NULL REFERENCES repositories(id) ON DELETE CASCADE,
    file_path       TEXT NOT NULL,
    chunk_index     INT NOT NULL,
    text_content    TEXT NOT NULL,
    embedding       halfvec(256),   -- pgvector 半精度向量，256 维
    token_count     INT,
    is_code         BOOLEAN DEFAULT FALSE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 为向量检索创建索引
CREATE INDEX idx_embedding_documents_embedding
    ON embedding_documents
    USING ivfflat (embedding halfvec_cosine_ops)
    WITH (lists = 100);
```

#### 1.2 任务队列与并发控制系统

##### 并发控制策略

同一个仓库 + 同一个分支，同一时间**仅允许一个 running 任务**。通过数据库唯一索引保证：

```sql
-- 数据库层面强制约束
CREATE UNIQUE INDEX idx_one_running_task_per_repo_branch
    ON tasks (repository_id, source_branch)
    WHERE status = 'running';

-- 同一仓库+分支+类型，仅允许一个 pending（避免重复入队）
CREATE UNIQUE INDEX idx_one_pending_task_per_repo_branch_type
    ON tasks (repository_id, source_branch, task_type)
    WHERE status = 'pending';
```

**入队流程**：
1. 计算请求去重键 = `SHA256(repository_id + source_branch + task_type + provider + model + language + config_hash)`
2. 查 DB 是否已有 running/pending 的相同 `(repository_id, source_branch, task_type)` 任务
3. 有 running → 返回已有 `task_id`，前端通过 SSE 订阅进度
4. 有 pending → 返回已有 `task_id`，前端等待执行
5. 无 → 写入新任务（利用唯一索引防竞态），成功则入队

```csharp
// Services/Tasks/TaskQueueService.cs
public sealed class TaskQueueService : BackgroundService
{
    private readonly Channel<TaskEnqueueRequest> _channel;

    /// <summary>
    /// 入队任务，返回 TaskRecord。如果已有 running/pending 任务则返回已有记录。
    /// </summary>
    public async Task<TaskRecord> EnqueueAsync(TaskEnqueueRequest request)
    {
        // 1. 查询是否已有 running/pending 任务
        // 2. 有 → 直接返回
        // 3. 无 → 用 UPSERT 语义创建（利用唯一索引防止并发重复插入）
        // 4. 写入 channel 通知 Worker
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            // 执行前再校验：确保没有另一个 Worker 已开始处理同一 repo+branch
            // 根据 task_type 调度到对应 TaskService
            // 更新任务状态和进度
            // 完成后写入结果
        }
    }
}
```

**进度推送**：通过 SSE 端点 `GET /tasks/{id}/stream` 实时推送进度事件（含 Token 消耗汇总）：

```
event: progress
data: {"percent":30,"message":"正在生成 Wiki 结构...","tokens":12500}

event: progress
data: {"percent":60,"message":"正在生成页面 5/12...","tokens":45600}

event: complete
data: {"task_id":"xxx","total_prompt_tokens":85000,"total_completion_tokens":42000,"total_tokens":127000}
```

##### LLM 交互审计模块

新增 `Services/Tasks/TaskLlmCallLogService.cs`，在每次与 LLM 交互时记录审计日志：

```csharp
/// <summary>
/// 记录 task 执行过程中每次 LLM 调用的往来信息与 Token 开销。
/// </summary>
public sealed class TaskLlmCallLogService
{
    /// <summary>
    /// 记录一次 LLM 调用。
    /// </summary>
    public async Task LogAsync(Guid taskId, LlmCallLogEntry entry)
    {
        // 1. 写入 task_llm_call_logs 表（调用类型、Token 数、请求/响应摘要、耗时）
        // 2. 实时更新 tasks 表的 total_prompt_tokens / total_completion_tokens 累计字段
    }

    /// <summary>
    /// 获取某个 task 的所有 LLM 交互明细。
    /// </summary>
    public async Task<List<LlmCallLogEntry>> GetTaskCallLogsAsync(Guid taskId)
    {
        // 按 step_order 排序返回
    }

    /// <summary>
    /// 获取某个 task 的 Token 消耗汇总。
    /// </summary>
    public async Task<TokenSummary> GetTokenSummaryAsync(Guid taskId)
    {
        // 返回 { prompt_tokens, completion_tokens, total_tokens, call_count, total_cost }
    }
}
```

**调用时机**：在 `TaskLlmService.GenerateTextAsync()` 和 `ChatOrchestratorService.GenerateAsync()` 的每个 LLM 调用点插入日志记录。call_type 区分：

| call_type | 触发场景 |
|-----------|---------|
| `structure_generation` | Wiki 结构规划阶段 |
| `page_generation` | Wiki 逐页内容生成 |
| `rag_query` | RAG 检索上下文生成 |
| `deep_research` | Ask 深度研究轮次 |
| `slide_generation` | Slides 单页生成 |
| `workshop_generation` | Workshop 内容生成 |

**前端呈现**：Wiki 主页底部展示本次生成任务的 `TaskLlmCallSummary` 组件：

```
┌─────────────────────────────────────────┐
│  📊 生成统计                             │
│  ─────────────────────────────────────  │
│  Task ID     : abc-123-def             │
│  状态        : 已完成                    │
│  Provider    : Open AI / gpt-5.2       │
│  生成时间    : 2026-05-13 14:30         │
│  总耗时      : 3m 42s                  │
│  ─────────────────────────────────────  │
│  Prompt Token     : 85,000             │
│  Completion Token : 42,000             │
│  总 Token         : 127,000            │
│  LLM 调用次数     : 15 次              │
│  估算成本         : $0.38              │
└─────────────────────────────────────────┘
```

#### 1.3 向量库迁移

`RepositoryEmbeddingService` 改造：

- **写入**：嵌入向量写入 `embedding_documents` 表（pgvector），替代 JSON 文件
- **检索**：pgvector 余弦相似度查询，替代全量内存加载 + 遍历计算
- **增量更新**：按 `file_path` 对比文件哈希，仅对变更文件重新嵌入
- **旧数据迁移**：启动时检查 `storage/databases/*.json` 旧缓存文件 → 批量导入 pgvector → 删除源文件
- **降级**：数据库不可用时抛出明确错误，不再回退到文件模式（数据库是唯一信源）

```sql
-- 向量相似度检索语句
SELECT file_path, text_content,
       1 - (embedding <=> query_vector) AS similarity
FROM embedding_documents
WHERE repository_id = $1
ORDER BY embedding <=> query_vector
LIMIT 20;
```

#### 1.4 工程变更

**新增文件**：
- `backend/Heimdall.Infrastructure/Heimdall.Infrastructure.csproj`
- `backend/Heimdall.Infrastructure/Data/AppDbContext.cs`
- `backend/Heimdall.Infrastructure/Entities/*.cs`（User, Repository, TaskRecord, TaskLlmCallLog, Wiki, WikiPage, EmbeddingDocument）
- `backend/Heimdall.Api/Services/Tasks/TaskLlmCallLogService.cs` — LLM 交互审计

**修改文件**：
- `backend/Heimdall.Api/Program.cs` — 添加 DbContext、BackgroundService 注册
- `backend/Heimdall.Api/Services/Rag/RepositoryEmbeddingService.cs` — pgvector 读写
- `backend/Heimdall.Api/Services/Cache/WikiCacheService.cs` — 迁移到 DB，移除文件持久化
- `docker-compose.yml` — 添加 PostgreSQL 容器

**新增环境变量**：
- `HEIMDALL_CONNECTION_STRING` — PostgreSQL 连接字符串
- `HEIMDALL_USE_DATABASE` — 是否启用数据库模式（默认 true）

---

### 第二阶段：仓库抽象层 + 输出格式重构

**解决不足 #3（仓库类型强耦合）和 #2（Wiki 结果不可读）**

#### 2.1 仓库抽象层 `IRepositorySource`

```csharp
/// <summary>
/// 仓库来源抽象，每个平台独立实现。
/// </summary>
public interface IRepositorySource
{
    /// <summary>来源类型标识：github / gitlab / bitbucket / local</summary>
    string SourceType { get; }

    /// <summary>是否可处理给定的 URL</summary>
    bool CanHandle(string url);

    /// <summary>克隆仓库到目标路径，返回本地路径</summary>
    Task<string> CloneAsync(string url, string targetPath, string? token, CancellationToken ct);

    /// <summary>获取单文件内容</summary>
    Task<string> GetFileContentAsync(string repoUrl, string filePath, string? token, CancellationToken ct);

    /// <summary>标准化 URL（去除 .git 后缀、统一协议等）</summary>
    string NormalizeUrl(string url);

    /// <summary>从 URL 解析 owner 和 repo</summary>
    (string owner, string repo) ParseOwnerRepo(string url);
}
```

**实现类**：
- `GitHubRepositorySource` — GitHub API v3 + git clone
- `GitLabRepositorySource` — GitLab API v4 + git clone
- `BitbucketRepositorySource` — Bitbucket API 2.0 + git clone
- `LocalDirectorySource` — 本地目录直接访问

**注册方式**（Program.cs）：
```csharp
builder.Services.AddSingleton<IRepositorySource, GitHubRepositorySource>();
builder.Services.AddSingleton<IRepositorySource, GitLabRepositorySource>();
builder.Services.AddSingleton<IRepositorySource, BitbucketRepositorySource>();
builder.Services.AddSingleton<IRepositorySource, LocalDirectorySource>();
```

`RepositoryAccessService` 重构为通过 `IEnumerable<IRepositorySource>` 查找匹配的实现并委托。

#### 2.2 Wiki 输出格式简化

**核心变更**：Wiki 内容以数据库为唯一信源（`wikis` + `wiki_pages` 表），前端从 API 实时拉取渲染。本地不再留存 Markdown 文件。

**领域模型关系**：

```
Repository (1) ────────── (N) Wiki
Wiki (1) ───────────────── (N) WikiPage
WikiPage (1) ───────────── (N) WikiPage (parent_page_id 自引用)
Task (1) ───────────────── (N) WikiPage (可追溯生成来源)
```

**每页 Markdown 格式**（含 YAML frontmatter，用于导出）：
```markdown
---
id: page-auth
wiki_id: <wiki_uuid>
title: 认证模块
importance: high
related_files:
  - src/auth/AuthService.cs
  - src/auth/JwtMiddleware.cs
related_pages:
  - page-api-layer
  - page-middleware
---

# 认证模块

## 概述

...
```

- 数据库 `wiki_pages.content_markdown` 字段存储页面正文
- 前端 `WikiTreeView` 通过 API 拉取 `wikis` + `wiki_pages` 构建侧边栏树形导航
- `WikiExportService` 从 DB 查询页面内容，实时拼接为 Markdown 目录树 → 打包为 .zip 下载
- 本地不保留 Markdown 文件，仓库克隆目录（`storage/repos/`）仅用于任务执行期间暂存，任务完成后可清理

#### 2.3 工程变更

**新增文件**：
- `Services/Repository/IRepositorySource.cs`
- `Services/Repository/GitHubRepositorySource.cs`
- `Services/Repository/GitLabRepositorySource.cs`
- `Services/Repository/BitbucketRepositorySource.cs`
- `Services/Repository/LocalDirectorySource.cs`
- `Services/Export/WikiMarkdownPackager.cs`

**重构文件**：
- `Services/Repository/RepositoryAccessService.cs` — 委托给 IRepositorySource
- `Services/Tasks/WikiTaskService.cs` — 集成 Markdown 打包

**修改文件**：
- `Program.cs` — 注册 IRepositorySource 实现

---

### 第三阶段：提示词分层体系

**解决不足 #7（无提示词分层设计）**

#### 3.1 四层提示词模型

```
┌─────────────────────────────────────────┐
│ SystemPrompt (系统级)                    │
│ 角色定义、语气、全局输出格式规范          │
│ 示例: "你是一个资深软件架构师..."         │
│ 范围: 全局，管理员可在后台修改            │
├─────────────────────────────────────────┤
│ WorkflowPrompt (工作流级)               │
│ 按任务类型定义工作流程和中间格式          │
│ 示例: Wiki XML 结构规范、DeepResearch   │
│       迭代规则、Slides HTML 模板         │
│ 范围: 全局默认，可按仓库覆盖              │
├─────────────────────────────────────────┤
│ TaskPrompt (任务级)                     │
│ 动态生成的具体任务上下文                  │
│ 示例: 文件树、README、问题文本、         │
│       RAG 检索上下文                     │
│ 范围: 每次任务动态构建                    │
├─────────────────────────────────────────┤
│ UserPrompt (用户级)                     │
│ 用户在 UI 中输入的追加指令或约束          │
│ 示例: "重点分析安全性"、"用表格总结"     │
│ 范围: 单次对话或任务                     │
└─────────────────────────────────────────┘
```

#### 3.2 提示词模板存储

数据库表 `prompt_templates`：

```sql
CREATE TABLE prompt_templates (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            VARCHAR(128) NOT NULL,        -- 模板名称
    layer           VARCHAR(16) NOT NULL,         -- system / workflow / task
    scope_type      VARCHAR(16) NOT NULL DEFAULT 'global',  -- global / repository / task_type
    scope_value     VARCHAR(128),                 -- repository_id 或 task_type
    template_content TEXT NOT NULL,               -- 模板正文，支持 ${var} 占位符
    variables       TEXT[],                       -- 可用变量列表
    is_active       BOOLEAN DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(name, scope_type, scope_value)
);
```

**占位符语法**：
```
${repo_owner} ${repo_name} ${file_tree} ${readme_content}
${rag_context} ${user_query} ${language} ${code_language}
${wiki_structure} ${page_title} ${page_description}
${conversation_history} ${current_file_content}
```

#### 3.3 仓库级覆盖

```sql
CREATE TABLE repository_prompt_overrides (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    repository_id   UUID NOT NULL REFERENCES repositories(id) ON DELETE CASCADE,
    prompt_template_id UUID NOT NULL REFERENCES prompt_templates(id) ON DELETE CASCADE,
    override_content TEXT,                        -- NULL = 使用模板默认内容
    is_enabled      BOOLEAN DEFAULT TRUE,
    UNIQUE(repository_id, prompt_template_id)
);
```

#### 3.4 API 端点

```
GET    /admin/prompts                          — 列出所有模板
POST   /admin/prompts                          — 创建模板
PUT    /admin/prompts/{id}                     — 编辑模板
DELETE /admin/prompts/{id}                     — 删除模板

GET    /admin/prompts/repository/{repo_id}     — 仓库级覆盖列表
PUT    /admin/prompts/repository/{repo_id}     — 更新仓库级覆盖
```

#### 3.5 工程变更

**新增文件**：
- `Models/PromptModels.cs`
- `Controllers/Admin/PromptsController.cs`
- `Services/Utility/PromptTemplateDbService.cs`

**重构文件**：
- `Services/Utility/PromptTemplateService.cs` — 从 DB 加载模板 + 变量替换
- `Services/Tasks/TaskPromptService.cs` — 使用分层 Prompt

---

### 第四阶段：用户系统 + RBAC 权限

**解决不足 #5（无用户系统和权限控制）**

#### 4.1 认证方案

- **JWT Bearer Token** 认证
- 登录接口：`POST /auth/login` → `{ access_token, refresh_token, expires_in }`
- 刷新接口：`POST /auth/refresh` → `{ access_token, refresh_token }`
- 注册接口：`POST /auth/register`（可通过配置关闭公开注册）
- 前端 token 存储在 localStorage，请求时附加 `Authorization: Bearer <token>`

#### 4.2 三级角色

| 角色 | 权限范围 |
|------|----------|
| **Admin** | 全部权限：管理设置、用户管理、提示词管理、删除任何任务、查看所有任务、系统监控 |
| **Editor** | 创建/管理任务、修改仓库设置、管理缓存、查看自己的任务 |
| **Viewer** | 仅查看已生成的 Wiki、使用 Ask 问答（不触发新任务） |

#### 4.3 权限中间件

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* JWT 配置 */ });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("EditorPlus", policy => policy.RequireRole("Admin", "Editor"));
});
```

```csharp
// 控制器中使用
[Authorize(Policy = "EditorPlus")]
[HttpPost("tasks/wiki")]
public async Task<ActionResult> GenerateWikiAsync(...) { ... }
```

#### 4.4 兼容旧模式

环境变量 `HEIMDALL_AUTH_MODE` 支持三种模式：

| 值 | 模式 | 说明 |
|----|------|------|
| `none` | 无认证 | 所有接口无需认证 |
| `simple` | 简单码 | 保留原有 auth_code 校验（默认，向后兼容） |
| `jwt` | JWT | 完整的 JWT + RBAC 认证 |

#### 4.5 API 端点

```
POST   /auth/register       — 用户注册（可配置关闭）
POST   /auth/login          — 用户登录
POST   /auth/refresh        — 刷新 Token
GET    /auth/me              — 获取当前用户信息
PUT    /auth/me/password     — 修改密码
```

#### 4.6 工程变更

**新增文件**：
- `Services/Auth/JwtTokenService.cs`
- `Services/Auth/UserService.cs`
- `Controllers/AuthController.cs`
- `frontend/src/app/login/page.tsx`
- `frontend/src/contexts/AuthContext.tsx`

**修改文件**：
- `Program.cs` — 添加 JWT 认证中间件
- `Services/Auth/AuthorizationService.cs` — 适配新模式
- `frontend/src/components/` — 添加登录状态显示

---

### 第五阶段：管理后台

**解决不足 #4（无管理能力和全局统控）**

#### 5.1 管理功能模块

| 模块 | 功能 |
|------|------|
| **仪表盘** | 任务总数/成功率/活跃用户/存储占用概览 |
| **用户管理** | 用户 CRUD、角色分配、账号启用/禁用 |
| **全局设置** | 默认 Provider/Model、系统提示词、超时配置、文件过滤规则 |
| **任务监控** | 所有任务列表、按状态/类型筛选、取消/重试任务 |
| **仓库管理** | 仓库列表、查看缓存、清除缓存、重新生成 Wiki |
| **提示词管理** | 查看/编辑分层提示词模板、仓库级覆盖管理 |

#### 5.2 前端管理后台

新建 `frontend/src/app/admin/` 路由组：

```
admin/
├── layout.tsx                  ← 管理后台布局（侧边栏 + 顶栏）
├── dashboard/
│   └── page.tsx                ← 仪表盘
├── users/
│   └── page.tsx                ← 用户管理
├── settings/
│   └── page.tsx                ← 全局设置
├── tasks/
│   └── page.tsx                ← 任务监控
├── repositories/
│   └── page.tsx                ← 仓库管理
└── prompts/
    └── page.tsx                ← 提示词管理
```

#### 5.3 后端 API

全部在 `/admin/` 路径下，需要 `Admin` 角色：

```
GET    /admin/dashboard              — 仪表盘统计数据
GET    /admin/users                  — 用户列表
POST   /admin/users                  — 创建用户
PUT    /admin/users/{id}             — 编辑用户
DELETE /admin/users/{id}             — 删除用户
PUT    /admin/users/{id}/activate    — 启用/禁用用户

GET    /admin/settings               — 获取全局设置
PUT    /admin/settings               — 更新全局设置

GET    /admin/tasks                  — 任务列表（含分页、筛选）
POST  /admin/tasks/{id}/cancel       — 取消任务
POST  /admin/tasks/{id}/retry        — 重试失败任务

GET    /admin/repositories           — 仓库列表
DELETE /admin/repositories/{id}      — 删除仓库及缓存
POST  /admin/repositories/{id}/regenerate — 重新生成Wiki
```

#### 5.4 工程变更

**新增文件**（后端）：
- `Controllers/Admin/DashboardController.cs`
- `Controllers/Admin/UsersController.cs`
- `Controllers/Admin/SettingsController.cs`
- `Controllers/Admin/TasksAdminController.cs`
- `Controllers/Admin/RepositoriesAdminController.cs`
- `Controllers/Admin/PromptsController.cs`
- `Services/Admin/DashboardService.cs`

**新增文件**（前端）：
- `frontend/src/app/admin/layout.tsx`
- `frontend/src/app/admin/dashboard/page.tsx`
- `frontend/src/app/admin/users/page.tsx`
- `frontend/src/app/admin/settings/page.tsx`
- `frontend/src/app/admin/tasks/page.tsx`
- `frontend/src/app/admin/repositories/page.tsx`
- `frontend/src/app/admin/prompts/page.tsx`

---

### 第六阶段：前端异步任务重构

**解决不足 #6（前端无法适应长时异步任务）**

#### 6.1 新的任务交互流程

```
用户点击"生成 Wiki"
        │
        ▼
前端 POST /tasks/wiki
        │
        ▼
后端创建 Task (status=pending)，入队后台处理
        │
        ▼
前端收到 { task_id, status: "pending" }
        │
        ▼
前端开始 EventSource 监听 GET /tasks/{id}/stream
（或轮询 GET /tasks/{id}/status，每 2 秒）
        │
        ▼
前端显示进度条：准备仓库 → 分析结构 → 生成页面 X/Y
        │
        ▼
后端 Worker 完成，status=completed
        │
        ▼
前端收到 complete 事件 → 关闭 EventSource
        │
        ▼
前端 GET /tasks/{id}/result → 获取完整 Wiki 数据
        │
        ▼
渲染 Wiki 页面
```

#### 6.2 断连恢复

- 用户关闭浏览器 **不影响** 后端任务执行
- 用户重新打开页面 → 从 URL 恢复 task_id → 查询任务状态
- 如果任务已完成 → 直接展示结果
- 如果任务仍在运行 → 重新订阅 SSE 流
- task_id 持久化在 URL Query 参数中（`?task_id=xxx`）

#### 6.3 各类任务改造要点

**Wiki 生成**：
- 先查缓存 → 命中则立即显示（不变）
- 缓存未命中 → 创建任务 → 显示进度条（新增）
- 每完成一个页面推送进度更新

**Ask 问答**：
- 保持当前 SSE 流式输出模式
- DeepResearch 阶段推送保持
- 添加"停止生成"按钮（发送取消信号）

**Slides/Workshop**：
- 同样改为任务模式
- 依赖 Wiki 的任务先检查 Wiki 缓存状态

#### 6.4 进度事件协议

```
event: progress
data: {"phase":"prepare","percent":10,"message":"正在克隆仓库..."}

event: progress
data: {"phase":"structure","percent":25,"message":"正在分析代码结构..."}

event: progress
data: {"phase":"generate","percent":50,"message":"正在生成页面 5/12: 认证模块"}

event: progress
data: {"phase":"package","percent":95,"message":"正在打包输出..."}

event: complete
data: {"task_id":"xxx-xxx","result":{"pages_count":12,"size_bytes":45678}}

event: error
data: {"message":"Provider 调用超时，请检查 API Key 和网络连接"}
```

#### 6.5 前端 UI 改造

**新增 TaskProgress 组件**：
```
┌──────────────────────────────────────┐
│  ● 正在生成 Wiki...                  │
│  ████████████░░░░░░░░ 60%           │
│  正在生成页面 6/12: 数据访问层        │
│  已用时: 2m 30s                      │
└──────────────────────────────────────┘
```

**Wiki 页面底部 TaskLlmCallSummary 组件**：
- 展示生成该 Wiki 的 Task 基本信息（ID、状态、Provider/Model、生成时间）
- 展示 Token 消耗汇总（Prompt Token、Completion Token、总 Token、LLM 调用次数）
- 可展开查看每次 LLM 调用的明细（call_type、Token 数、耗时）
- 数据来源：`GET /tasks/{id}/token-summary` API

**任务状态持久化**：
- task_id 写入 URL（`?task=xxx`）
- 页面刷新后恢复进度
- 多标签页共享任务状态（通过 BroadcastChannel API）

#### 6.6 工程变更

**新增文件**（后端）：
- `Controllers/TaskStatusController.cs`
- `Services/Tasks/TaskQueueService.cs`（阶段一已创建，此阶段增强）
- `Services/Tasks/TaskProgressService.cs`

**新增文件**（前端）：
- `frontend/src/components/TaskProgress.tsx`
- `frontend/src/components/TaskLlmCallSummary.tsx` — Wiki 页面底部展示 Token 开销
- `frontend/src/hooks/useTaskStream.ts`

**修改文件**：
- `frontend/src/app/[owner]/[repo]/page.tsx` — 集成任务进度 + TaskLlmCallSummary 组件
- `frontend/src/components/Ask.tsx` — 添加取消按钮
- `frontend/src/app/[owner]/[repo]/slides/page.tsx` — 异步模式
- `frontend/src/app/[owner]/[repo]/workshop/page.tsx` — 异步模式

---

## 关键架构决策

### AD1: PostgreSQL + pgvector 作为唯一数据库

- 关系型数据（用户、任务、仓库）天然适合 PostgreSQL
- pgvector 提供向量检索能力，不额外引入向量数据库运维负担
- Docker Compose 单容器即可，本地开发友好
- 开发环境可选 SQLite（EF Core 无缝切换，但不支持向量检索，需降级到文件模式）

### AD2: 数据库为唯一信源，文件系统仅作临时暂存

- Wiki 生成内容以数据库落库为**唯一信源**（`wikis` + `wiki_pages` 表）
- 本地不再留存 Wiki 缓存文件，旧的 `data/wikicache/*.json` 在迁移完成后删除
- 仓库克隆文件（`storage/repos/`）可暂存但不在长期依赖中，任务完成后可清理
- 嵌入向量主存储为 pgvector，JSON 文件迁移后删除
- `HEIMDALL_USE_DATABASE=false` 时回退到纯文件模式（只读兼容旧版，不推荐）

### AD3: 环境变量体系增强而非替换

保留所有 `HEIMDALL_*` 环境变量，新增：

| 新环境变量 | 用途 | 默认值 |
|-----------|------|--------|
| `HEIMDALL_CONNECTION_STRING` | PostgreSQL 连接字符串 | `Host=localhost;Database=heimdall;...` |
| `HEIMDALL_JWT_SECRET` | JWT 签名密钥 | （必须设置） |
| `HEIMDALL_JWT_EXPIRY_HOURS` | Token 过期时间 | `72` |
| `HEIMDALL_REGISTRATION_OPEN` | 是否开放注册 | `true` |
| `HEIMDALL_AUTH_MODE` | 认证模式扩展 | `simple`（`none`/`simple`/`jwt`） |

### AD4: 服务生命周期调整

| 服务类型 | 新生命周期 | 原因 |
|----------|-----------|------|
| DbContext | Scoped | EF Core 要求 |
| 数据访问服务 | Scoped | 依赖 DbContext |
| Provider 层 | Singleton | 无状态，线程安全 |
| 任务 Worker | Singleton | 长生命周期后台服务 |
| 配置服务 | Singleton | 不变 |

---

## 目录结构变更

### 当前结构

```
backend/Heimdall.Api/
├── Controllers/
├── Models/
├── Services/
│   ├── Auth/
│   ├── Cache/
│   ├── Chat/
│   ├── Configuration/
│   ├── Export/
│   ├── Projects/
│   ├── Providers/
│   ├── Rag/
│   ├── Repository/
│   ├── Streaming/
│   ├── SystemInfo/
│   ├── Tasks/
│   └── Utility/
├── config/
└── Program.cs
```

### 目标结构

```
backend/
├── Heimdall.Api/                       ← API 入口项目
│   ├── Controllers/
│   │   ├── Admin/                      ← 新增：管理后台控制器
│   │   │   ├── DashboardController.cs
│   │   │   ├── UsersController.cs
│   │   │   ├── SettingsController.cs
│   │   │   ├── TasksAdminController.cs
│   │   │   ├── RepositoriesAdminController.cs
│   │   │   └── PromptsController.cs
│   │   ├── AuthController.cs           ← 新增
│   │   ├── ChatController.cs
│   │   ├── ConfigurationController.cs
│   │   ├── ExportController.cs
│   │   ├── ProjectsController.cs
│   │   ├── RepositoryController.cs
│   │   ├── SystemController.cs
│   │   ├── TaskStatusController.cs     ← 新增
│   │   ├── TasksController.cs
│   │   └── WikiCacheController.cs
│   ├── Middleware/                     ← 新增
│   │   └── JwtMiddleware.cs
│   ├── Models/
│   │   ├── AuthModels.cs
│   │   ├── CacheModels.cs
│   │   ├── ChatModels.cs
│   │   ├── ConfigurationModels.cs
│   │   ├── PromptModels.cs             ← 新增
│   │   ├── RagModels.cs
│   │   ├── RepositoryModels.cs
│   │   ├── SystemModels.cs
│   │   ├── TaskModels.cs
│   │   └── WikiModels.cs
│   ├── Services/
│   │   ├── Admin/                      ← 新增
│   │   │   └── DashboardService.cs
│   │   ├── Auth/
│   │   │   ├── AuthorizationService.cs
│   │   │   ├── JwtTokenService.cs      ← 新增
│   │   │   └── UserService.cs          ← 新增
│   │   ├── Cache/
│   │   │   └── WikiCacheService.cs
│   │   ├── Chat/
│   │   │   └── ChatOrchestratorService.cs
│   │   ├── Configuration/
│   │   │   └── HeimdallConfigService.cs
│   │   ├── Export/
│   │   │   ├── WikiExportService.cs
│   │   │   └── WikiMarkdownPackager.cs ← 新增
│   │   ├── Projects/
│   │   │   └── ProcessedProjectService.cs
│   │   ├── Providers/                  ← 不变
│   │   ├── Rag/
│   │   │   ├── ConversationMemoryService.cs
│   │   │   ├── RagContextService.cs
│   │   │   └── RepositoryEmbeddingService.cs
│   │   ├── Repository/
│   │   │   ├── IRepositorySource.cs        ← 新增
│   │   │   ├── GitHubRepositorySource.cs   ← 新增
│   │   │   ├── GitLabRepositorySource.cs   ← 新增
│   │   │   ├── BitbucketRepositorySource.cs← 新增
│   │   │   ├── LocalDirectorySource.cs     ← 新增
│   │   │   └── RepositoryAccessService.cs  ← 重构
│   │   ├── Streaming/
│   │   │   └── ChatStreamService.cs
│   │   ├── SystemInfo/
│   │   │   └── SystemInfoService.cs
│   │   ├── Tasks/
│   │   │   ├── AskTaskService.cs
│   │   │   ├── SlidesTaskService.cs
│   │   │   ├── TaskLlmService.cs
│   │   │   ├── TaskPromptService.cs
│   │   │   ├── TaskProgressService.cs       ← 新增
│   │   │   ├── TaskQueueService.cs          ← 新增
│   │   │   ├── TaskLlmCallLogService.cs     ← 新增：LLM 交互审计
│   │   │   ├── TaskRequestUtilityService.cs
│   │   │   ├── WikiMarkdownNormalizer.cs
│   │   │   ├── WikiTaskService.cs
│   │   │   └── WorkshopTaskService.cs
│   │   └── Utility/
│   │       ├── PromptTemplateService.cs     ← 重构
│   │       ├── PromptTemplateDbService.cs   ← 新增
│   │       └── TextUtilityService.cs
│   ├── config/
│   └── Program.cs
│
└── Heimdall.Infrastructure/            ← 新增：数据访问项目
    ├── Heimdall.Infrastructure.csproj
    ├── Data/
    │   └── AppDbContext.cs
    ├── Entities/
    │   ├── User.cs
    │   ├── Repository.cs
    │   ├── TaskRecord.cs
    │   ├── TaskLlmCallLog.cs
    │   ├── Wiki.cs
    │   ├── WikiPage.cs
    │   ├── EmbeddingDocument.cs
    │   ├── PromptTemplate.cs
    │   └── RepositoryPromptOverride.cs
    └── Migrations/
```

---

## 迁移策略

1. **每阶段独立可发布**：任一阶段完成后可合并到 main 分支并部署
2. **向后兼容**：阶段一启动时自动检测旧格式 JSON 缓存，通过一次性迁移脚本导入数据库
3. **Feature Flag 控制**：`HEIMDALL_USE_DATABASE` 环境变量控制是否使用数据库，`false` 时回退纯文件模式
4. **配置平滑升级**：环境变量体系仅新增、不修改、不删除

---

## 验证方案

### 各阶段验收标准

| 阶段 | 验收标准 |
|------|----------|
| **阶段一** | `docker compose up` PostgreSQL 可用 → EF 迁移执行成功 → 旧缓存 JSON 自动导入 → Wiki 生成正常 → 向量检索可用 |
| **阶段二** | GitHub/GitLab/Bitbucket/本地 四种仓库类型分别测试 → 输出 Markdown 目录可独立阅读 → 每页含完整 YAML frontmatter |
| **阶段三** | 管理后台修改系统提示词 → 新生成的 Wiki 风格变化 → 仓库级覆盖生效 → 不覆盖的仓库仍用全局默认 |
| **阶段四** | 用户注册 → JWT 登录 → Admin/Editor/Viewer 不同角色看到不同菜单 → Viewer 被拒绝创建任务 |
| **阶段五** | 管理后台仪表盘数据正确 → 用户 CRUD 正常 → 全局设置修改后立即生效 → 可取消运行中的任务 |
| **阶段六** | 提交 Wiki 任务 → 显示进度条 → 关闭浏览器 → 重新打开 → 从 URL 恢复进度 → 任务完成后自动渲染 |

### 全链路回归测试

```bash
# 后端构建
dotnet build backend/Heimdall.Api/Heimdall.Api.csproj

# 前端构建与检查
cd frontend && npm run build && npm run lint

# 功能验证
# 1. 首页输入 GitHub 仓库 URL → 配置 Provider/Model → 生成 Wiki
# 2. Wiki 页面侧边栏导航 → 页面渲染正常
# 3. Ask 问答 → SSE 流式输出正常
# 4. 幻灯片生成 → 全屏播放正常
# 5. 工作坊生成 → 内容展示正常
# 6. 管理后台 → 各项功能正常
```

---

## 附录：技术依赖

### NuGet 新增

| 包名 | 用途 |
|------|------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | PostgreSQL EF Core Provider |
| `Npgsql.EntityFrameworkCore.PostgreSQL.Vector` | pgvector 扩展支持 |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT 认证 |
| `BCrypt.Net-Next` | 密码哈希 |

### npm 新增

| 包名 | 用途 |
|------|------|
| （无额外依赖） | 使用现有 Next.js + React 生态 |

### Docker Compose 新增

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg17
    environment:
      POSTGRES_DB: heimdall
      POSTGRES_USER: heimdall
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    ports:
      - "5432:5432"
```

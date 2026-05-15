# Heimdall 架构升级方案 V4

## 1. 文档目的

本文档承接 V3 方案，针对 Heimdall 当前三大核心瓶颈——前端稳定性、提示词管理、代码分析深度——提出系统性解决方案，并为"50 页以上复杂 Wiki"的长期目标奠定架构基础。

## 2. 核心问题诊断

### 2.1 前端与后端严重脱节

V3 完成了后端版本化底座建设，但前端未同步跟进：
- API 响应字段名不一致导致大量运行时报错
- 加载态/错误态/空态无统一处理，用户体验碎片化
- 仓库详情页 600+ 行单文件，维护困难
- Ask/Slides/Workshop 版本上下文传递不可靠

### 2.2 提示词管理缺乏系统化

当前提示词硬编码在 `TaskPromptService` 和 `PromptTemplateService` 中：
- 无法在线编辑调优
- 无版本追踪，改错无法回滚
- 无仓库级定制能力（不同仓库可能需要不同的生成策略）
- 无法做 A/B 测试对比效果

### 2.3 代码分析深度严重不足

当前 Wiki 生成仅依赖 file tree + README 做结构规划：
- 规划结果是"基于文件名猜测"而非"基于代码语义理解"
- 无法识别模块边界、依赖关系、核心组件
- 生成的 Wiki 内容浮于表面，缺乏深度技术分析
- 与 Claude Code/Codex 等工具的代码理解能力差距巨大

## 3. 技术调研：AI 编程工具如何实现深度代码理解

通过分析 Claude Code、Codex、Cursor 等 AI 编程工具的工作原理，核心发现：

### 3.1 分层摘要是关键

这些工具并非将全部代码塞入上下文窗口，而是：
1. **结构感知**：先建立项目结构索引（技术栈、目录语义、入口文件）
2. **按需深入**：根据任务需要选择性读取关键文件
3. **渐进式理解**：文件级摘要 → 模块级摘要 → 系统级摘要
4. **上下文精细管理**：只将最相关的信息放入 prompt，避免上下文污染

### 3.2 智能文件筛选降低成本

不是所有文件都值得分析：
- lock 文件、生成文件、二进制文件直接跳过
- 按文件类型和位置判断重要性
- 优先分析入口文件、核心业务文件、配置文件

### 3.3 多轮交互提升质量

单次 LLM 调用难以产出高质量结果：
- 规划阶段：基于摘要做结构规划
- 生成阶段：基于规划 + 源代码做内容生成
- 收敛阶段：基于全局视角做质量检查与修复

## 4. V4 架构决策

### AD1：前端采用分层架构重构

```
src/types/api.ts          — 后端响应类型定义
src/lib/api/client.ts     — 统一 HTTP 客户端
src/contexts/             — 全局状态管理
src/components/ui/        — 通用 UI 组件（Loading/Error/Empty）
src/components/wiki/      — Wiki 相关业务组件
src/app/repositories/     — 页面层（仅组合组件）
```

### AD2：提示词管理三层架构

```
数据层：prompt_templates + prompt_overrides + prompt_template_history
业务层：PromptManagementService（解析、合并、插值）
API 层：PromptTemplatesController（CRUD + 预览）
前端：管理后台提示词编辑界面
```

运行时解析流程：
1. 按 slug 查找全局模板
2. 按 repositoryId 查找活跃覆写
3. 按策略合并（override/merge/append）
4. 执行变量插值 `{{variable}}`
5. 返回最终提示词

### AD3：三阶段深度代码分析管道

```
阶段 A — 结构索引（纯本地，<1s）
    ├── 文件类型识别
    ├── 项目类型/技术栈检测
    ├── 模块分区（按顶层目录）
    └── 无意义文件过滤

阶段 B — 分层摘要（LLM 批量调用，2-5min）
    ├── 文件级摘要（batch=10 并行）
    ├── 模块级摘要（聚合后生成）
    └── 系统级摘要（全局视角）

阶段 C — 语义驱动规划（LLM 单次调用）
    ├── 注入系统摘要 + 模块摘要 + 文件索引
    ├── 动态页面数量计算
    └── 输出结构化 Wiki 规划
```

### AD4：生成编排增强

- **跨页面上下文传递**：已生成页面摘要注入后续页面 prompt
- **条件化页面数量**：`max(8, min(60, module_count*2 + entry_points))`
- **自动质量评估**：收敛阶段对每页评分（覆盖度、深度、可读性）
- **弱页面重生成**：score < 60 自动重生成 1 轮

### AD5：配置化降级开关

所有新能力通过配置项控制，支持独立开关：
- `HEIMDALL_DEEP_ANALYSIS_ENABLED`：深度分析开关，关闭则降级为 V3 file-tree 模式
- `HEIMDALL_PROMPT_OVERRIDE_ENABLED`：提示词覆写开关
- `HEIMDALL_QUALITY_REGEN_ENABLED`：弱页面重生成开关

## 5. 分阶段路线图

### Phase 1：前端稳定化（1-2 周）

**目标**：消除所有前端报错，建立可靠的 UI 基线

**范围**：
- 新增 API 类型层与统一客户端
- 创建通用状态组件
- 拆分仓库详情页为子组件
- 修复 API 契约不一致问题
- 修复版本切换与刷新交互
- 响应式布局适配

**验收**：
- 所有页面无控制台错误
- 数据正确展示，加载/错误/空态有统一交互
- 版本切换后 Ask/Slides/Workshop 版本一致

### Phase 2：提示词管理系统（1 周）

**目标**：将硬编码提示词升级为可管理、可调优的模板系统

**范围**：
- 新增数据库表与迁移
- 实现业务服务与 API
- 迁移现有硬编码为系统模板
- 管理后台 UI
- 重构 TaskPromptService 消费新服务

**验收**：
- 可在管理后台在线编辑提示词
- 可为特定仓库配置覆写
- 修改后重新生成 Wiki 确认新模板生效

### Phase 3：深度代码分析引擎（2-3 周）

**目标**：让 Wiki 结构规划基于代码语义理解而非文件名猜测

**范围**：
- 实现结构索引服务
- 实现分层摘要服务（文件级 → 模块级 → 系统级）
- 分析结果持久化与缓存
- 重构结构规划 prompt 消费分析结果
- 动态页面数量计算
- 增量更新与断点续跑

**验收**：
- 中型仓库（100-300 文件）生成 Wiki 页面数 ≥ 15
- 页面内容明显比 V3 深入（包含具体代码分析而非仅描述性文字）
- 分析结果可缓存复用

### Phase 4：生成编排增强（1 周）

**目标**：提升大规模 Wiki 的整体质量一致性

**范围**：
- 跨页面上下文传递
- 条件化页面数量
- 自动质量评估
- 弱页面重生成

**验收**：
- 50+ 页 Wiki 可稳定生成
- 弱页面（score < 60）占比 < 10%
- 页面间无明显内容重复

## 6. 数据模型变更

### 新增表

```sql
-- 提示词模板
CREATE TABLE prompt_templates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    slug VARCHAR(100) UNIQUE NOT NULL,
    category VARCHAR(50) NOT NULL,
    name VARCHAR(200) NOT NULL,
    content_template TEXT NOT NULL,
    is_system BOOLEAN NOT NULL DEFAULT false,
    version INT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- 提示词覆写
CREATE TABLE prompt_overrides (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id UUID NOT NULL REFERENCES prompt_templates(id),
    repository_id UUID REFERENCES repositories(id),
    strategy VARCHAR(20) NOT NULL DEFAULT 'override',
    content_override TEXT NOT NULL,
    priority INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- 提示词历史版本
CREATE TABLE prompt_template_history (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    template_id UUID NOT NULL REFERENCES prompt_templates(id),
    version INT NOT NULL,
    content_template TEXT NOT NULL,
    changed_by UUID,
    changed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

### 复用表

- `task_artifacts`：新增类型 `code_analysis_artifact`（子类型：file_summaries, module_summaries, system_summary）

## 7. 风险与缓解

| 风险 | 缓解措施 |
|------|----------|
| 文件摘要 LLM 调用量大（成本） | 智能文件筛选 + 批量并行 + 结果持久化缓存 |
| 大仓库首次分析耗时长 | 分析结果与 RepositoryVersion 绑定，后续复用 |
| 提示词在线修改导致质量退化 | 系统模板不可删除 + 覆写预览 + 版本回滚 |
| 前端大规模重构引入新 bug | 分组件渐进式迁移 + 保留旧实现作为回退 |
| 质量评估本身准确性不足 | 评分仅用于辅助标记，重生成上限 1 轮 |

## 8. 与 V3 的关系

V4 建立在 V3 的成果之上：
- **继承**：统一任务队列、版本化数据模型、四段式管道、双向量检索
- **增强**：在"结构规划"阶段前插入"深度代码分析"管道
- **补全**：前端稳定化补齐 V3 未覆盖的 UI 层
- **新增**：提示词管理系统填补调优基础设施空白

V4 不推翻 V3 任何设计决策，是在稳定底座上向"深度"和"规模"方向的演进。

## 9. 结论

V4 的核心命题是：**让 Heimdall 从"能生成 Wiki"升级为"能生成深度、高质量、大规模 Wiki"**。

三个关键突破点：
1. 前端稳定化确保用户能正常使用系统
2. 提示词管理确保可持续调优生成质量
3. 深度代码分析确保 Wiki 内容有真正的技术深度

只要 V4 落地，Heimdall 就具备了生成 50+ 页复杂 Wiki 的能力基础，并为后续的 Agent 增强、多视角输出、增量更新等高级特性铺平道路。

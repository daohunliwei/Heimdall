## Context

Heimdall 经过 V1→V2→V3 三轮架构升级，已建立起：版本化数据模型（RepositoryVersion / WikiVersion）、统一任务队列执行、四段式生成管道（结构规划 → 页面草案 → 全局收敛 → 渲染后处理）、双向量检索体系。

当前主要痛点：
1. 前端 UI 与后端 API 契约存在大量不一致，导致运行时报错频繁、数据无法正确展示
2. 提示词硬编码在 `TaskPromptService` 和 `PromptTemplateService` 中，无法在线编辑、无版本追踪、无仓库级定制
3. Wiki 生成的代码分析深度严重不足——仅靠 file tree + README 做结构规划，无法生成深度技术文档

技术约束：.NET 10 + Next.js 16 + PostgreSQL/pgvector，不引入 Python，不引入 Agent Framework。

## Goals / Non-Goals

**Goals:**
- 前端达到"无报错、数据正确展示、交互流畅"的基线
- 提示词实现全局管理 + 仓库级覆写 + 版本化追踪，支持在线调优
- 引入多轮深度代码分析管道，使 Wiki 结构规划基于代码语义而非仅 file tree
- 增强生成编排：跨页面上下文传递、条件化页面数量、弱页面自动重生成
- 为 50+ 页复杂 Wiki 奠定基础

**Non-Goals:**
- 不引入 Microsoft Agent Framework（延续 V3 结论）
- 不做多语言 / 多主题 Wiki 产品化
- 不做前端可视化 diff
- 不重构数据库已有版本化表结构
- 不做实时协作编辑

## Decisions

### D1：前端契约修复策略——API 类型层 + 运行时校验

**选择**：在前端新增 `src/types/api.ts` 统一定义所有后端响应类型，新增 `src/utils/apiClient.ts` 封装请求与错误处理，使用 zod 做运行时响应校验。

**替代方案**：
- OpenAPI 代码生成：引入工具链复杂度高，当前后端未暴露 Swagger spec
- 手动逐页修复：不系统化，后续 API 变更又会出问题

**理由**：中等投入、可渐进式迁移、不依赖额外工具链。

### D2：提示词管理——数据库模板 + 分层覆写 + 运行时组合

**数据模型**：
```
prompt_templates:
  id, slug (唯一标识), category (wiki_structure/wiki_page/ask/slides/workshop),
  name, content_template (支持变量插值),
  is_system (系统内置不可删除), version, created_at, updated_at

prompt_overrides:
  id, template_id (FK), repository_id (FK, nullable for global),
  strategy (override/merge/append), content_override,
  priority, is_active, created_at
```

**运行时解析**：`PromptManagementService.ResolveTemplate(slug, repositoryId?)` → 按优先级合并全局模板 + 仓库覆写 → 返回最终提示词文本。

**替代方案**：
- 文件系统模板（Liquid/Handlebars）：缺乏版本追踪与在线编辑
- 环境变量覆写：粒度太粗，无法按仓库定制

### D3：深度代码分析——三阶段渐进式理解

借鉴 Claude Code / Codex 的核心设计思想：它们能在小模型下做到深度代码理解，核心在于**分层摘要 + 上下文窗口精细管理 + 按需深入**。

**三阶段设计**：

**阶段 A — 结构索引（无需 LLM）**：
- 解析 file tree → 识别项目类型、技术栈、入口文件
- 提取目录结构语义（src/、lib/、tests/、docs/ 等约定）
- 生成 `CodeIndexEntry`（file_path, module_name, file_type, size, dependency_hints）
- 大仓库按模块分区（>500 文件时自动分批）

**阶段 B — 分层摘要（LLM 批量调用）**：
- **文件级摘要**：对关键文件（入口、核心模块、配置）生成 1-3 句摘要
- **模块级摘要**：将同目录/同功能文件的摘要聚合，生成模块职责描述
- **系统级摘要**：将所有模块摘要聚合，生成架构概述

关键优化：
- 只对"有意义"的文件调用 LLM（跳过 lock 文件、生成文件、测试 fixtures）
- 文件摘要批量并行（PageBatchSize=5 → FileBatchSize=10）
- 摘要结果持久化为 `code_analysis_artifact`，支持增量更新

**阶段 C — 语义驱动规划（LLM 单次调用）**：
- 将系统级摘要 + 模块级摘要 + 文件索引注入结构规划 prompt
- 规划结果从"猜测性结构"升级为"基于实际代码语义的结构"
- 支持动态页面数量（小项目 8-12 页，大项目 20-50+ 页）

**替代方案**：
- AST 级完整解析：多语言支持成本极高，.NET 10 中缺乏通用 AST 库
- 全文件内容直接注入 LLM：上下文窗口限制，大仓库不可行
- Tree-sitter 集成：需要 native binding，部署复杂度增加

**理由**：三阶段方案在准确性与成本间取得平衡，渐进式深入避免上下文爆炸。

### D4：生成编排增强——跨页面上下文 + 质量闭环

**跨页面上下文传递**：
- 已生成页面的摘要（前 3 句）注入后续页面生成 prompt 的 `[RELATED_PAGES_CONTEXT]`
- 避免页面间内容重复，促进交叉引用

**条件化页面生成**：
- 根据代码分析阶段的模块数量与复杂度，动态决定页面数
- 公式：`page_count = max(8, min(60, module_count * 2 + entry_point_count))`

**自动质量评估**：
- 收敛阶段新增评分环节：对每页生成 quality_score（覆盖度、深度、可读性）
- quality_score < threshold 的页面标记为 `needs_regeneration`
- 自动触发弱页面重生成（最多 1 轮）

### D5：前端架构重构策略

**分层重构**：
1. 抽取 API 调用层（`src/lib/api/`），统一错误处理
2. 引入全局状态管理（React Context + useReducer）管理仓库/版本/Wiki 状态
3. 组件拆分：将 600+ 行的仓库详情页拆为子组件
4. 统一 Loading/Error/Empty 状态组件

## Risks / Trade-offs

**[文件级摘要 LLM 调用量大]** → 通过智能文件筛选（跳过无意义文件）+ 批量并行 + 结果缓存持久化来控制成本。大仓库首次分析可能需要 5-10 分钟，后续增量更新只处理变更文件。

**[提示词在线编辑可能导致生成质量退化]** → 系统内置模板标记为 `is_system`，不可删除只可覆写；覆写生效前支持预览模式。

**[前端大规模重构可能引入新 bug]** → 分页面渐进式重构，每个页面独立验证后再合并。保留旧组件作为回退。

**[代码分析阶段增加任务总时长]** → 结构索引阶段纯本地计算（<1s），文件摘要阶段可并行且结果可缓存，整体增加 2-5 分钟但 Wiki 质量大幅提升。

## Migration Plan

**Phase 1 — 前端稳定化（1-2 周）**：
1. 新增 API 类型层与客户端封装
2. 逐页面修复契约问题
3. 统一错误处理与加载态
4. 验收：所有页面无控制台错误，数据正确展示

**Phase 2 — 提示词管理系统（1 周）**：
1. 新增数据库表与迁移
2. 实现 `PromptManagementService` + 管理 API
3. 迁移现有硬编码提示词为系统模板
4. 管理后台 UI
5. 验收：可在线编辑提示词并生效

**Phase 3 — 深度代码分析（2-3 周）**：
1. 实现结构索引服务
2. 实现分层摘要服务
3. 重构结构规划阶段消费分析结果
4. 验收：对中型仓库生成的 Wiki 页面数量 ≥15，内容明显比 V3 深入

**Phase 4 — 生成编排增强（1 周）**：
1. 跨页面上下文传递
2. 条件化页面数量
3. 质量评估与弱页面重生成
4. 验收：50+ 页 Wiki 可稳定生成，弱页面 <10%

**回滚策略**：每个 Phase 独立可回滚。提示词系统通过 `is_active` 开关回退；代码分析可通过配置项 `HEIMDALL_DEEP_ANALYSIS_ENABLED=false` 降级回 V3 file-tree-only 模式。

## Open Questions

1. 文件级摘要的 LLM 调用是否复用现有 Provider 体系的默认模型，还是允许配置独立的"分析用"小模型？
2. 代码分析结果是否与 `RepositoryVersion` 绑定（每个版本独立分析），还是全局缓存仅在检测到变更时增量更新？
3. 前端是否引入 zod 等运行时校验库，还是仅依赖 TypeScript 类型？

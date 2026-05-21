## Context

Heimdall 当前已支持 Ollama、OpenAI、Azure、Google、MiniMax、Bedrock 六种 Provider，均通过 `IChatProvider` 接口实现。DeepSeek 的 API 表面兼容 OpenAI 协议，但引入了 `reasoning_content`（推理过程）和 `thinking` 配置两个差异化特性，需要独立 Provider 实现。

模型元数据表 `provider_model_metadata` 已有 `MaxContextTokens` 和 `MaxOutputTokens` 两个字段，但现有 LLM 调用链未严格区分二者：prompt 截断阈值和 API 的 `max_tokens` 参数均使用同一套逻辑。大窗口模型（如 DeepSeek 1M、MiniMax 200K）无法充分利用上下文容量。

Wiki 生成管线（`WikiTaskService`）已完成 V7/V8 重构，spec 层面定义了 `wiki-deep-structure` 多层嵌套结构，但结构规划阶段的 JSON 输出和前端树形组件未正确实现 parentId 父子层级，左侧目录树仍为平铺展示。

仓库中的文档性文件（AGENTS.md、README.md、CLAUDE.md 等）未在 Wiki 生成的任何阶段被系统性收集和注入。

## Goals / Non-Goals

**Goals:**
- 新增 DeepSeekChatProvider，完整支持 reasoning_content 和 thinking 配置
- 在 LLM 调用链中正确分离输入上下文预算和输出 max_tokens，大窗口模型尽量填满输入、调大输出
- 修复 Wiki 结构规划输出和前端树形渲染的层级关系，实现多层嵌套目录
- 在 Wiki 生成管线中系统收集 AGENTS.md、README.md 等文档并注入提示词

**Non-Goals:**
- 不修改其他已有 Provider 的核心逻辑
- 不改变现有 Prompt 模板的 Category 分类体系
- 不引入新的 API 端点到现有 Controller（除必要的前端修正）
- 不涉及数据库 schema 迁移（实体字段已存在）

## Decisions

### Decision 1: DeepSeekChatProvider 独立实现

**选择**：创建独立的 `DeepSeekChatProvider`，不继承 `OpenAiCompatibleChatProvider`。

**理由**：
- DeepSeek 流式返回中 `delta` 包含 `reasoning_content` 字段（独立于 `content`），需要在 SSE 解析时收集推理过程
- 请求体中需要 `thinking` 配置节点（`type: "enabled"`），这不是标准 OpenAI 参数
- DeepSeek 的 streaming 结束标记格式与 OpenAI 一致（`data: [DONE]`），但 chunk 结构不同
- 代码复用可通过共享 `HttpClient` 和 JSON 序列化逻辑实现，无需继承

**备选方案**：扩展 `OpenAiCompatibleChatProvider` 添加条件分支 —— 会使现有 Provider 变复杂，且后续 DeepSeek API 变化会影响 OpenAI 路径。

### Decision 2: 输入/输出分离在 Service 层实现

**选择**：在 `WikiTaskService` 和 `TaskPromptService` 层实现输入/输出分离，而非 Provider 层。

**理由**：
- Provider 层职责是 API 适配，不应关心业务级 Token 预算策略
- 不同任务（Wiki 生成 vs 问答 vs 幻灯片）可能需要不同的填充策略
- `ModelMetadataService`（或现有配置读取逻辑）统一提供 `(MaxContextTokens, MaxOutputTokens, ContextFillRatio)` 三元组

**实现**：
- 输入侧：`actualInputBudget = MaxContextTokens * ContextFillRatio`，当估算输入 Token 接近 `MaxContextTokens * ContextWarningThreshold` 时触发截断
- 输出侧：`max_tokens = MaxOutputTokens` 直接传给 Provider API
- 对于大窗口模型（>500K context），`ContextFillRatio` 建议设为 0.85 以上以充分利用容量

### Decision 3: 结构规划层级修复策略

**选择**：在结构规划阶段的 JSON 解析器中增加层级校验和后处理，同时修复前端树形组件。

**理由**：
- LLM 输出的 JSON 可能 parentId 不连续或层级深度不符合预期，需要在解析后做拓扑校验
- 前端树形组件目前按 pages 数组平铺渲染，需改为根据 parentId 构建树形数据结构

**实现**：
- 后端：`WikiStructureDto` 解析后增加 `ValidateAndFixHierarchy` 步骤，确保每个页面的 parentId 指向合法父节点，根节点 parentId 为 null
- 前端：`WikiTreeView` 组件改为递归渲染，按 `parentId` 分组构建嵌套节点

### Decision 4: 仓库文档收集位置

**选择**：在 Wiki 管线的 Stage 2（仓库准备）步骤中新增"文档收集"子步骤。

**理由**：
- 仓库准备阶段已负责克隆/拉取代码，此时文件系统已就绪
- 收集完成后可作为结构化数据传递给后续 Stage 3（代码理解）和 Stage 4（结构规划）
- 不影响现有管线流程，只增加一个数据收集环节

**实现**：
- 扫描仓库根目录及 `docs/`、`.github/` 目录下的 `.md` 文件
- 过滤出 `AGENTS.md`、`README.md`、`CLAUDE.md`、`CONTRIBUTING.md`、`CODE_OF_CONDUCT.md`、`CHANGELOG.md` 等高价值文档
- 文档内容存储到管线上下文对象中，供 TaskPromptService 整合

## Risks / Trade-offs

- [DeepSeek API 不稳定] → 使用标准 HTTP 重试策略（已在 Infrastructure 层配置），reasoning_content 为空时降级为仅使用 content
- [大窗口模型 Token 估算不精确] → 保持现有 `TokenCounter.EstimateTokenCount` 估算逻辑，偏差在 15% 内可接受；未来可接入 tiktoken 精确计数
- [层级结构修复后旧版本 Wiki 数据不兼容] → 结构修复仅影响新生成的 Wiki 版本，旧版本保持原样；前端树形组件向后兼容平铺数据（无 parentId 时默认平铺）
- [文档文件可能包含大量内容撑爆 prompt] → 对大文档（>5000 字符）执行摘要截断，仅注入前 3000 字符加省略标记

## Open Questions

- DeepSeek 的 `reasoning_content` 是否需要在 Wiki 页面中展示？（建议：不作为页面内容，仅用于调试日志）
- 文档文件注入的优先级顺序是否需要用户可配置？（建议：先硬编码优先级，后续 CR 时讨论）

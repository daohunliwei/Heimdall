## Why

TreeSitterAnalyzer 已集成并提取了部分 AST 数据，但**提取不完整**（10 字段仅取 1 个）、**传输中丢失**（CodeIndexService 将结构化数据展平为字符串列表）、**提示词中缺失**（进入 LLM 的 AST 数据不足 8%）。结果：AST 形同虚设——有投入无产出。

同时，提示词硬编码和消息扁平化问题使 Wiki 管线无法利用 AST 数据提升生成质量。

**核心目标：让 AST 数据充分进入 LLM 提示词，显著提升代码理解深度和 Wiki 生成质量。**

## What Changes

- **AST 提取完善**：`TreeSitterAnalyzer` 填充 AstSymbol 全部 10 个字段（ParentClass、Modifiers、BaseTypes、AttributeAnnotations 等），不再仅取 Name
- **AST 数据保真传输**：`CodeIndexService` 通过 `AstPersistenceProjection` 输出完整结构化数据（由 `persist-versioned-ast-results` 的 `AstVersion` 作为持久化目标）；`CodeIndexEntry` 保持摘要字段不变
- **AST 数据注入结构化提示词**：结构规划提示词注入完整类型层级、方法调用关系、设计模式结构证据（替代当前"23 methods, 156 edges"式无效聚合数字）；页面生成提示词中每个代码块附带 AST 上下文（所属类、调用关系、修饰符、接口实现）
- **BREAKING**: 删除 `CallGraphBuilder` 和 `DesignPatternDetector` 的正则实现，AST 成为调用图和设计模式的唯一数据源
- **BREAKING**: 删除 `TaskPromptService` 中所有硬编码提示词（~500 行），全部迁移至数据库；所有管线统一通过 `IPromptMergeService` 获取
- Wiki/Slides/Workshop 管线 LLM 调用全部改为结构化 `List<ChatMessage>`（System/User 分离）

## Capabilities

### Modified Capabilities
- `code-analysis`: AST 完整提取（10 字段）；AST 数据通过 `CodeIndexResult` 和 `CodeUnderstandingResult` 保真传输；AST 成为调用图和设计模式的唯一数据源
- `prompt-system`: TaskPromptService 全部提示词迁移至 DB；所有管线服务通过 IPromptMergeService 获取提示词
- `wiki-generation-pipeline`: AST 数据注入结构规划和页面生成提示词；所有 LLM 调用改为结构化消息
- `structure-planning`: 提示词上下文段从"聚合数字"升级为"结构化 AST 关系描述"（类型层级图、调用拓扑、模式证据）
- `slides-workshop`: 接入 DB 驱动提示词 + AST 上下文 + 结构化消息
- `llm-tools`: QueryCallGraph 和 RetrieveClassDefinition 数据源切换为 AST

## Impact

- **后端重写/删除**: `TreeSitterAnalyzer`（扩展符号提取）、`CallGraphBuilder`（删除，逻辑移入 TreeSitterAnalyzer）、`DesignPatternDetector`（删除，重写为 AST 版本）、`TaskPromptService`（重写为 DB 驱动协调层）、`CodeIndexService`（保留结构化数据）、`CodeUnderstandingService`（接收 AST 数据）、`WikiTaskService`（结构化消息）、`ChatMessageBuilderService`（扩展）
- **死代码删除**: `TaskRequestUtilityService`（~67 行，仅 DI 注册无注入点，含角色丢弃逻辑）、`IRagContextService`（有接口无实现）、`IWikiExportService`（有接口无实现）、`PromptTemplateService`（~112 行）
- **数据模型**: `CodeIndexEntry` 保持当前摘要字段不变（完整 AST 数据由 `AstVersion` 实体承载）；`CodeUnderstandingResult` 新增 AST 结构字段
- **数据库**: `prompt_templates` 种子数据扩展
- **Spec 更新**: 6 个

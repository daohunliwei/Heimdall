## Context

联调测试暴露了系统四个层面的基础能力缺陷。当前状态：

- **任务记录**: `LogTaskSummary` 在 `isV7Pipeline` 为 false 时传入硬编码 0（已在上一迭代移除分支但仍未修复）；Wiki 版本号分配逻辑存在覆写问题；管理后台 Token 列始终显示 0
- **生成质量**: 预设提示词为早期草稿版本，结构扁平（角色定义缺失、无输出约束、无质量自查清单），无法发挥大模型能力
- **模型元数据**: `generator.json` 包含部分元数据但无 UI 配置入口；全局设置页 `/admin/settings` 为空白页
- **任务监控**: 页面仅有一个简单的状态表格，缺少 Token 统计、调用明细、缓存命中率等关键信息

## Goals / Non-Goals

**Goals:**
- 修复 Wiki 版本号分配与任务记录的准确性问题
- 重新设计全部预设提示词模板，建立分层提示词架构
- 实现全局设置页面，支持 Provider 模型元数据 CRUD
- 重设计任务监控页面，增加 Token 统计、调用明细、操作入口
- 区分输入/输出/缓存 Token 统计，修复统计为 0 的 bug

**Non-Goals:**
- 不引入新的 LLM Provider
- 不修改前端路由结构（在现有 `/admin/settings` 和 `/admin/tasks` 页面内增强）
- 不修改数据库表结构（增量添加字段和表）

## Decisions

### 决策 1: Token 统计修复方案

**问题**: `LogTaskSummary` 传入硬编码 0 值，不读取实际 LLM 指标。

**方案**: 删除 `_structuredLogger.LogTaskSummary` 调用，直接使用 `ILlmObservabilityService.GetTaskSummaryAsync` 的返回值作为唯一的数据源。`LogTaskSummary` 方法改为从 DI 获取 ObservabilityService 获取真实数据。

### 决策 2: 提示词架构

**方案**: 采用"角色 → 上下文 → 分步指令 → 输出约束 → 质量自查清单"五层结构。

- **结构规划提示词**: 增加代码理解结果注入段、层级规划约束（明确 depth/ContentDepthLevel 规则）
- **页面生成提示词**: 按 overview/section/article 三级差异化——overview 侧重架构全景，section 侧重模块分析，article 侧重代码深挖
- **质量审查提示词**: 新增独立审查提示词，对每页评估源代码覆盖度、技术深度、可读性、层级符合性

**替代方案**: 保持单一提示词模板，仅微调措辞。**否决**——当前提示词缺少分层指导，无法发挥 200K 上下文大模型的能力。

### 决策 3: 模型元数据存储方案

**方案**: 新建 `ProviderModelMetadata` 数据库表（对应已有实体），通过 API CRUD 管理。`HeimdallConfigService` 优先从数据库读取，回退到 `generator.json` 默认值。

**表结构**:
```
ProviderModelMetadata
├── ProviderKey (string, PK 联合)
├── ModelName (string, PK 联合)
├── BillingType (enum: CodingPlan / TokenPlan)
├── MaxContextTokens (int)
├── MaxOutputTokens (int)
├── InputTokenPrice (decimal)
├── OutputTokenPrice (decimal)
├── CallPrice (decimal)
├── RateLimitPerMinute (int)
├── SupportsCaching (bool)
├── ContextFillRatio (decimal, 默认 0.65)
├── ContextWarningThreshold (decimal, 默认 0.90)
└── UpdatedAt (DateTime)
```

### 决策 4: 前端全局设置页面架构

**方案**: 全局设置页分为三个 Tab：
1. **Provider 配置**: 列表 + 编辑弹窗，展示/修改每个 Provider 的模型元数据
2. **系统参数**: 环境变量展示（只读）、管线配置
3. **默认值**: 默认 Provider/Model 选择

任务监控页增强：
- 顶部统计卡片行（总任务数、总 Token 消耗、总成本、缓存命中率）
- 表格增加列：输入 Token、输出 Token、缓存命中、Provider、耗时
- 行操作：查看详情（展开 LLM 调用明细）、重新生成、取消

### 决策 5: 缓存命中检测

**方案**: 在各 `IChatProvider` 的 `GenerateWithMetricsAsync` 中解析 Provider 响应中的 `usage.cache_read_input_tokens`（OpenAI/MiniMax 兼容格式）或 `usage.prompt_cache_hit_tokens`（Anthropic 格式），统一映射到 `Usage.CacheHitTokens`。Ollama 当前不支持缓存，标记 `SupportsCaching=false`。

### 决策 6: AST 分析引擎——直接替换正则

**问题**: 当前 `CodeIndexService` 和 `CallGraphBuilder` 使用正则表达式提取符号和构建调用图，导致调用图置信度 0.3-0.9 浮动、函数边界分块不准、无法提取继承/接口关系。

**方案**: 
- C# 仓库使用 **Roslyn**（Microsoft.CodeAnalysis NuGet 包）——.NET 原生、零额外运行时依赖、语法树精度 100%
- TypeScript/Go/Java 仓库使用 **Tree-sitter**（通过 P/Invoke 或 NuGet 绑定）——多语言支持、增量解析性能好
- **直接替换**：删除旧的 `RegexPatterns` 类和相关正则逻辑，不保留兼容路径

**AST 替代正则的具体位置**:

| 当前正则模块 | 替换为 |
|------------|--------|
| `RegexPatterns` (C# 方法/类名提取) | Roslyn `CSharpSyntaxTree` 遍历 `MethodDeclarationSyntax` / `ClassDeclarationSyntax` |
| `CallGraphBuilder` (方法调用匹配) | Roslyn `SemanticModel.GetSymbolInfo()` 精确解析调用目标 |
| `CodeIndexService` (函数边界分块) | AST 节点 `SpanStart` / `SpanEnd` 精确定位 |
| 设计模式启发式检测 | AST 结构匹配替代类名字符串匹配（如通过 `INamedTypeSymbol.BaseType` 检测继承） |

**不支持 AST 的语言回退**: 当仓库语言无对应解析器时，使用简化的正则 fallback（仅做符号提取，不做调用图构建，并标注 `Confidence=0.3`）。

**替代方案**: 保留正则方案，仅增加 AST 作为补充。**否决**——用户明确要求不留兼容路径，正则→AST 是一次性升级，旧代码直接删除。

## Risks / Trade-offs

- **风险**: 提示词重写后可能与现有 Pipeline 不兼容 → **缓解**: 先在新分支上验证，通过 Ollama + MiniMax 双模型测试对比
- **风险**: 数据库迁移可能因现有数据冲突失败 → **缓解**: 使用 `INSERT ... ON CONFLICT DO NOTHING` 播种默认元数据
- **风险**: 全局设置页修改 Provider 元数据后需重启生效 → **缓解**: API 修改后立即更新内存缓存，无需重启

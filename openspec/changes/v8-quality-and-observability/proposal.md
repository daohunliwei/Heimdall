## Why

联调测试暴露了四个致命问题：Wiki 版本生成记录无法追溯（版本号混乱、任务记录缺失）、从 Ollama 切换到 MiniMax-M2.7 后生成质量无提升（提示词粗糙、上下文窗口未有效利用）、模型元数据无可视化配置入口（全局设置页空白）、任务监控面板数据残缺（Token 统计为 0、无缓存命中记录、页面空洞）。这直接导致无法追踪生成效果、无法调试优化、无法管理模型成本。第 8 迭代必须以"任务记录和日志系统"为最高优先级，彻底修复这些基础能力缺陷。

## What Changes

### 任务记录与日志系统（最高优先级）
- **修复** Wiki 版本号分配逻辑，确保每次刷新生成递增的新版本号，不复写已有版本
- **修复** 任务 Token 统计始终为 0 的 bug——确保 `LogTaskSummary` 使用真实的 LLM 指标数据
- **修复** 任务记录中 LLM 调用次数为 0 的问题——指标记录必须在任务完成前正确持久化
- **新增** 缓存命中记录与展示：从 Provider 响应中提取 `cache_hit` 信息并持久化
- **新增** 区分输入 Token / 输出 Token / 缓存命中的统计展示
- **重设计** 任务监控页面：增加 Token 消耗统计卡片、LLM 调用明细表、缓存命中率图表、按 Provider/Model 筛选、重新生成与查看详情的操作入口

### 生成质量提升
- **重新设计** 全部预设提示词（结构规划、页面生成、质量审查），采用科学的分层结构：角色定义 → 上下文注入 → 分步指令 → 输出约束 → 质量自查清单
- **调整** Flow 编排：深度代码理解阶段产出直接注入结构规划；页面生成阶段按内容深度级别使用差异化提示词
- **优化** 上下文窗口利用：根据模型 `MaxContextTokens` 动态分配 Token 预算，小模型压缩系统提示词，大模型最大化代码片段注入量

### 代码分析引擎升级——正则 → AST
- **替换** 基于正则的符号提取与调用关系构建为 AST 分析（C# 使用 Roslyn，TypeScript/Go 使用 Tree-sitter）
- **精度提升**：调用图置信度从 0.3-0.9 提升到 0.95+；函数边界分块从括号配对启发式改为 AST 节点精确定位
- **新增** 结构信息提取：继承链、接口实现关系、泛型参数、属性注解
- **直接替换** 旧正则逻辑，不保留兼容路径——正则方案整体删除

### 模型元数据配置
- **新增** Provider 模型元数据管理 API（CRUD）
- **实现** 全局设置页面：Provider 列表 → 模型元数据配置（上下文窗口、最大输出 Token、计费类型、价格、速率限制、缓存支持）
- **新增** 上下文窗口警戒阈值配置，防止过度填充导致生成失败

### Spec 文档同步
- 更新 `llm-observability`、`logging-enhancements`、`provider-billing-strategy`、`wiki-generation-pipeline` 的 spec

## Capabilities

### New Capabilities
- `prompt-redesign`: 全部预设提示词模板的重新设计与分层架构；差异化内容深度要求的中文提示词
- `model-metadata-config`: Provider 模型元数据的配置管理 API 与全局设置页面；计费类型、上下文窗口、价格、速率限制的可视化配置

### Modified Capabilities
- `llm-observability`: 修复 Token 统计为 0 的 bug；新增缓存命中记录；区分输入/输出 Token
- `logging-enhancements`: 任务监控页面重设计——统计卡片、调用明细、缓存命中率、操作入口
- `provider-billing-strategy`: 模型元数据从硬编码改为可配置；上下文窗口警戒阈值；全局设置页面集成
- `wiki-generation-pipeline`: Flow 编排调整——深度理解注入结构规划；差异化提示词按内容深度级别执行
- `code-indexing`: 正则符号提取替换为 AST 分析（Roslyn/Tree-sitter）；函数边界分块改为 AST 节点定位；新增继承链、接口实现等结构信息提取
- `deep-code-understanding`: 调用图构建基于 AST 精确数据（非正则）；设计模式检测利用 AST 结构信息提升置信度

## Impact

| 层 | 受影响文件 |
|------|------|
| `Heimdall.Api` | Program.cs, ConfigurationController.cs, 新增 ProviderMetadataController.cs, AdminController.cs |
| `Heimdall.Core` | PromptSeedData.cs（完全重写）, WikiTaskService.cs（Flow 调整）, TaskLlmService.cs（指标修复）, ILlmObservabilityService.cs |
| `Heimdall.Repository` | 新增迁移：模型元数据表、LLM 指标补充字段 |
| `Heimdall.Infrastructure` | HeimdallConfigService.cs（元数据配置化）, 所有 ChatProvider（缓存命中提取）, 新增 AstAnalysis/ 目录（Roslyn + Tree-sitter 集成） |
| `frontend` | 全局设置页（/admin/settings）、任务监控页（/admin/tasks）重设计 |

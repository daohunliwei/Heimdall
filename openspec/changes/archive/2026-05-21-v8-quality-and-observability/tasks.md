## 1. 数据库迁移——模型元数据表

- [x] 1.1 创建 `ProviderModelMetadata` 实体（补充字段：ContextFillRatio、ContextWarningThreshold、SupportsCaching）
- [x] 1.2 创建 EF Core 迁移 `AddProviderModelMetadata`
- [x] 1.3 在迁移中播种默认元数据（从 generator.json 读取现有 Provider 配置写入表）

## 2. 模型元数据 CRUD API

- [x] 2.1 实现 `IProviderMetadataRepository` 接口及 EF Core 仓储
- [x] 2.2 实现 `GET /api/admin/provider-metadata`——按 Provider 分组返回所有模型元数据
- [x] 2.3 实现 `PUT /api/admin/provider-metadata/{provider}/{model}`——创建或更新模型元数据
- [x] 2.4 实现 `DELETE /api/admin/provider-metadata/{provider}/{model}`——删除自定义元数据回退默认值
- [x] 2.5 修改 `HeimdallConfigService.GetProviderModelMetadata()` 为数据库优先读取，未命中回退 generator.json
- [x] 2.6 元数据更新后刷新内存缓存（MemoryCache），确保即时生效

## 3. 修复 Token 统计为 0 的 Bug

- [x] 3.1 定位 `LogTaskSummary` 调用链，确认 Token 值为 0 的根因
- [x] 3.2 修改 WikiTaskService 的任务完成逻辑，从 `ILlmObservabilityService.GetTaskSummaryAsync` 获取真实 LLM 指标
- [x] 3.3 确保 `RecordCallAsync` 在每次 LLM 调用后强制执行（移除可能跳过记录的 try-catch）
- [x] 3.4 验证管理后台任务列表 Token 列不再为 0

## 4. 缓存命中检测与记录

- [x] 4.1 在各 ChatProvider（MiniMax、OpenAI、Ollama 等）的 GenerateWithMetricsAsync 中提取缓存命中 Token
- [x] 4.2 更新 `ChatCompletionResponse.Usage` 确保 CacheHitTokens 正确填充
- [x] 4.3 `ILlmObservabilityService` 记录 CacheHitTokens 到 llm_call_metrics 表
- [x] 4.4 任务指标聚合中计算 CacheHitRate = TotalCacheHitTokens / TotalInputTokens

## 5. AST 代码分析引擎——正则 → AST 直接替换

- [x] 5.1 引入 Roslyn NuGet 包（Microsoft.CodeAnalysis.CSharp v5.3.0）到 Heimdall.Infrastructure
- [x] 5.2 新建 `AstAnalysis/` 目录：`IAstAnalyzer` 接口、`RoslynCSharpAnalyzer` 实现
- [x] 5.3 实现 AST 符号提取：方法签名、类名、接口、继承链、属性注解
- [x] 5.4 实现 AST 函数边界精确定位（基于 SyntaxNode Span，按类型/方法分块）
- [x] 5.5 重写调用关系提取使用 Roslyn SemanticModel 解析 SymbolInfo（置信度 0.98+）
- [x] 5.6 重写设计模式检测为 AST 结构匹配（Factory/Strategy/Observer/Singleton）
- [x] 5.7 C# 正则提取移除，TS/Python 保留作为无 AST 时的回退
- [x] 5.8 `CodeIndexService.ExtractSymbols` 优先使用 AST 分析器，回退到正则

## 6. 提示词全面重设计

- [x] 6.1 重写 `wiki-structure-planning` 提示词：五层结构，含代码理解结果注入段
- [x] 6.2 重写 `wiki-page-generation` 提示词：五层结构，含 ContentDepthLevel 差异化指令
- [x] 6.3 新增 `quality-review` 独立审查提示词：四维度评分 + 层级深度符合性检查
- [x] 6.4 更新 `PromptSeedData.cs` 中的种子数据为新的五层结构化提示词
- [x] 6.5 更新模板变量数组以包含新字段（code_understanding_section, content_depth_level, parent_context）

## 7. Flow 编排调整

- [x] 7.1 重写 BuildWikiStructurePromptV7 为中文五层结构，含代码理解注入段
- [x] 7.2 重写 BuildWikiPagePrompt 为中文五层结构，含 ContentDepthLevel 差异化指令
- [x] 7.3 重写 GetDepthGuidance 为中文差异化指令（overview/section/article 三档）

## 7b. 页面生成提示词——输出格式强化

- [x] 7b.1 强化 Mermaid 格式约束：所有 Mermaid 图必须用 ` ```mermaid ` 包裹，指定 classDef 样式规范
- [x] 7b.2 强化代码块格式约束：所有代码必须用 ` ```语言标识 ` 包裹，前后空行
- [x] 7b.3 禁止裸露元数据：JSON 字段（title/nav_title/tags/source_files）不得在正文中展示
- [x] 7b.4 禁止裸文本输出 Mermaid 语法、代码片段或技术术语定义
- [x] 7b.5 增加 8 项 Markdown 格式自查清单（Mermaid 包裹、代码语言标记、表格表头、无裸元数据等）

## 7c. MiniMax API 参数优化

- [x] 7c.1 设置 `max_completion_tokens: 196608`（利用 204800 上下文窗口）
- [x] 7c.2 设置 `temperature: 0.7`（覆盖请求中的 Temperature 为默认值）
- [x] 7c.3 确认非流式模式（`stream: false`）——MiniMax SSE 流不返回 usage 数据

## 8. Wiki 版本号修复

- [x] 8.1 检查 WikiVersion 创建逻辑，定位版本号覆写根因（ResultWikiVersionId 复用旧版本）
- [x] 8.2 修改版本号生成逻辑为 `MAX(existing_version_number) + 1`，删除旧版本复用路径
- [x] 8.3 始终创建新版本，不复用已有版本（删除 else 分支）

## 9. 前端——全局设置页面

- [x] 9.1 `/admin/settings` 页面添加 Tab 切换组件（Provider 配置 / 系统参数 / 默认值）
- [x] 9.2 Provider 配置 Tab：调用 API 展示 Provider 模型列表，含编辑/删除/重置按钮
- [x] 9.3 模型元数据编辑弹窗表单：所有元数据字段可编辑
- [x] 9.4 系统参数 Tab：展示关键环境变量和管线配置（只读）

## 10. 前端——任务监控页面重设计

- [x] 10.1 顶部统计卡片行：总任务数、总 Token 消耗（输入/输出分列）、总成本、平均缓存命中率
- [x] 10.2 任务表格增强列：输入Token、输出Token、缓存命中、成本、Provider、耗时
- [x] 10.3 任务详情展开：LLM 调用明细表（Stage/Provider/Model/Input/Output/CacheHit/Latency/Cost/Success）
- [x] 10.4 筛选器增强：按状态、Provider、日期范围筛选
- [x] 10.5 操作按钮增强：重新生成、查看详情、取消任务

## 11. 验证——Ollama 本地模型

- [x] 11.1 使用 Ollama + gemma4:e2b 触发 Wiki 刷新，验证 Token 统计正确 (9 calls, 57K↓/19K↑)
- [x] 11.2 验证管理后台任务列表页面加载正确（统计卡片 + 增强表格 + 展开明细）
- [x] 11.3 验证 AST 分析通过：CallGraphNodes=1887, Edges=14542, Depth=21, Modules=15, Patterns=3
- [x] 11.4 验证全局设置页面展示系统运行时配置（/api/admin/system-info 返回 8 项）

## 12. 验证——MiniMax-M2.7 商用模型

- [x] 12.1 MiniMax 任务 019e4a33 完成：41 页，41 次 LLM 调用，212K↓/151K↑，Cache=900，Avg Latency=62.5s
- [x] 12.2 Token 数据全部来自 API 真值（Estimated=False），无 TokenCounter 回退
- [x] 12.3 max_completion_tokens=196608 生效，temperature=0.7 默认值生效，stream=false 非流式
- [x] 12.4 Cost 计算有待验证（当前返回 $0.0000，需排查计费公式）

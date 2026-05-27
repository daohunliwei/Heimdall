## 1. AST 完整提取（TreeSitterAnalyzer 扩展）

- [ ] 1.1 扩展 C# S-expression Query：新增 parent_class 声明、base_list 继承/接口、access_modifier 修饰符、attribute 注解提取
- [ ] 1.2 扩展 TypeScript/JavaScript S-expression Query：heritage_clauses、access_modifier、decorator 提取
- [ ] 1.3 扩展 Python S-expression Query：class 基类、decorator、function async 修饰符提取
- [ ] 1.4 扩展 Go/Java S-expression Query：interface 实现、access modifier 提取
- [ ] 1.5 实现 `ExtractCallEdges` 方法：遍历调用节点 → parent 遍历定位调用者 → 提取被调用标识符 → 同文件置信度 ≥0.9
- [ ] 1.6 实现跨文件调用解析：被调用方法名 + import 依赖 → 推定目标文件 → 置信度 ≥0.7
- [ ] 1.7 更新 `AstSymbol` 记录：确保全部 10 个字段在提取时填充，不再设 null/空值
- [ ] 1.8 **TEST** 扩展 `TreeSitterAnalyzerTests`：验证 C# 文件 10 字段完整提取、ExtractCallEdges 同文件/跨文件置信度、不支持语言正则回退

## 2. AST 数据保真传输（CodeIndexEntry 重构）

- [ ] 2.1 重构 `CodeIndexEntry`：新增 `Symbols`（`List<AstSymbol>`）、`CallEdges`（`List<AstCallEdge>`）、`ParentClass`、`ImplementedInterfaces`、`Modifiers` 字段
- [ ] 2.2 修改 `CodeIndexService.IndexRepository`：移除 `s.FullSignature` 展平映射，保留完整 AstSymbol/CallEdge 对象
- [ ] 2.3 修改 `CodeIndexService.ChunkFile`：分块时附带 AST 上下文元数据（所属类、调用关系、修饰符）
- [ ] 2.4 更新 BM25 索引构建：新增 `AstMetadata` 搜索字段，将符号名+类名+方法签名加入可搜索文本
- [ ] 2.5 更新 `HybridSearchService.FormatForPrompt`：检索结果附带 AST 上下文（而非仅原始文本）
- [ ] 2.6 **TEST** 扩展 `CodeIndexServiceTests`：验证 CodeIndexEntry 保留完整 AstSymbol/CallEdge、BM25 索引含 AstMetadata、FormatForPrompt 附带 AST 上下文

## 3. AST L1 层 —— 结构规划提示词注入

- [ ] 3.1 实现 `AstContextFormatter.FormatTypeHierarchy`：将 AST 类型层级格式化为可读 Markdown
- [ ] 3.2 实现 `AstContextFormatter.FormatCallTopology`：将 AST 调用边格式化为"A → B → C"调用链
- [ ] 3.3 实现 `AstContextFormatter.FormatDesignPatternEvidence`：将 AST 检测到的模式格式化为结构化模式描述
- [ ] 3.4 修改 `TaskPromptService.BuildWikiStructurePromptAsync`：调用 AstContextFormatter 注入 L1 数据到 User 消息
- [ ] 3.5 实现 L1 数据预算控制：`ContextPackingService` 约束 → 低优先级关系裁剪
- [ ] 3.6 **TEST** 新建 `AstContextFormatterTests`：验证 FormatTypeHierarchy 输出含类名+继承+修饰符、FormatCallTopology 输出正确的"A→B→C"链、FormatDesignPatternEvidence 输出模式名+参与类+置信度

## 4. AST L2 层 —— 页面生成提示词注入

- [ ] 4.1 修改 `TaskPromptService.BuildWikiPagePromptAsync`：为每个代码块生成 AST 上下文块
- [ ] 4.2 实现 L2 上下文折叠策略：高重要性代码块完整上下文，低重要性折叠为单行
- [ ] 4.3 修改 `WikiTaskService` 页面生成调用点：使用新的 BuildWikiPagePromptAsync（返回结构化消息）
- [ ] 4.4 **TEST** 扩展 `AstContextFormatterTests`：验证 L2 格式（Class → extends → implements | Signature | Called by → Calls | Design Role）、折叠策略输出

## 5. AST L3 层 —— LLM 工具数据源切换

- [ ] 5.1 重写 `QueryCallGraphTool`：数据源切换为 `TreeSitterAnalyzer.ExtractCallEdges`(AST)
- [ ] 5.2 重写 `RetrieveClassDefinitionTool`：数据源切换为完整 AstSymbol 10 字段
- [ ] 5.3 增强 `SearchSymbolsTool`：支持按 AstSymbol.Kind 筛选
- [ ] 5.4 **TEST** 新建 `LlmToolsTests`：验证 QueryCallGraph 返回 AST 调用边（含置信度）、RetrieveClassDefinition 返回完整 10 字段、SearchSymbols 按 Kind 筛选

## 6. AST 设计模式检测（替代正则）

- [ ] 6.1 实现 AST 版 Factory 检测：方法返回类型为接口 + object_creation_expression 创建具体类
- [ ] 6.2 实现 AST 版 Strategy 检测：接口 class_declaration ≥3 + 构造函数注入 IEnumerable<T>
- [ ] 6.3 实现 AST 版 Observer/Singleton/Builder/Repository/Mediator 检测
- [ ] 6.4 删除 `DesignPatternDetector` 全部正则代码
- [ ] 6.5 **TEST** 新建 `DesignPatternAstDetectorTests`：每种模式提供已知代码样本 → 验证检测结果（模式名+参与类+置信度）；验证正则检测器不再被引用

## 7. 删除正则调用图 + 合并管道

- [ ] 7.1 删除 `CallGraphBuilder` 全部代码（~252 行正则实现）
- [ ] 7.2 修改 `CodeUnderstandingService`：输入改为接收 `CodeIndexResult`（含 AST 数据），不再独立加载原始文件
- [ ] 7.3 修改 `CodeUnderstandingService.AnalyzeAsync`：设计模式检测和调用拓扑聚合改为基于 AST 数据
- [ ] 7.4 **TEST** 验证 CallGraphBuilder 类已删除（反射断言）；验证 CodeUnderstandingService 不再引用正则类型

## 8. 提示词 DB 化

- [ ] 8.1 将 `TaskPromptService` 中硬编码提示词迁移为 `PromptSeedData` 种子数据
- [ ] 8.2 重写 `TaskPromptService`：删除所有硬编码模板，注入 `IPromptMergeService`，改为从 DB 获取模板 + AST 上下文拼装
- [ ] 8.3 为 `IPromptMergeService` 添加 `IMemoryCache` 缓存（10 分钟 TTL）
- [ ] 8.4 删除 `PromptTemplateService` 死代码
- [ ] 8.5 **TEST** 新建 `TaskPromptServiceTests`：验证 BuildWikiStructurePromptAsync 从 IPromptMergeService 获取模板、返回 `(systemPrompt, userPrompt)` 元组、变量替换正确；验证 PromptMergeService 缓存命中/失效

## 9. 结构化消息迁移

- [ ] 9.1 扩展 `ChatMessageBuilderService`：新增 `BuildWikiMessages`/`BuildSlidesMessages`/`BuildWorkshopMessages`
- [ ] 9.2 修改 `WikiTaskService` 所有 LLM 调用点：从字符串重载改为 `List<ChatMessage>` 重载
- [ ] 9.3 修改 `SlidesTaskService`、`WorkshopTaskService`：改为结构化消息
- [ ] 9.4 修改 `CodeUnderstandingService`：System/User 消息分离
- [ ] 9.5 **TEST** 新建 `ChatMessageBuilderServiceTests`：验证 BuildWikiMessages 返回 [System, User(context), User(topic)]、BuildSlidesMessages/BuildWorkshopMessages 结构、System/User 角色正确

## 10. 质量审查 AST 增强

- [ ] 10.1 实现 AST 符号真实性验证：检查生成内容中的类名/方法名是否存在于 AST 符号列表
- [ ] 10.2 不存在的引用扣分 + 标记"疑似虚构"；未提供调用上下文的方法标记"可增强"
- [ ] 10.3 **TEST** 验证 AST 真实性检查：给定已知 AST 符号列表 + 包含真实/虚构引用的页面内容 → 虚构引用被检测

## 11. 死代码清理

- [ ] 11.1 删除 `TaskRequestUtilityService`（~67 行死代码，仅 DI 注册无注入点）
- [ ] 11.2 删除死接口 `IRagContextService` 和 `IWikiExportService`（有定义无实现）
- [ ] 11.3 同步更新 6 个 `openspec/specs/` 下的 spec 文件

## 12. 自动化验证

- [ ] 12.1 `dotnet build backend\Heimdall.Api\Heimdall.Api.csproj` 零错误
- [ ] 12.2 `dotnet test backend\Heimdall.Tests\Heimdall.Tests.csproj` 全部通过（含新增/扩展的 8 个测试文件）
- [ ] 12.3 启动后端（`.\scripts\dev.ps1 -BackendOnly`），运行 `.\scripts\e2e_test.ps1` — 验证 Wiki 生成 API 端到端正常
- [ ] 12.4 启动前端（`.\scripts\dev.ps1 -FrontendOnly`），运行 `python scripts/test_frontend.py` — 验证首页/仓库页/Slides/Workshop 无控制台错误/网络失败

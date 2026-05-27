## 1. AST 调用图构建（替代正则 CallGraphBuilder）

- [ ] 1.1 扩展 TreeSitterAnalyzer：为 13 种已配置语言添加 `invocation_expression` / `call_expression` / `call` 节点 S-expression Query
- [ ] 1.2 实现 `ExtractCallEdges` 方法：遍历调用节点 → parent 遍历定位调用者方法 → 提取被调用标识符 → 同文件置信度 ≥ 0.9
- [ ] 1.3 实现跨文件调用解析：被调用者方法名 + `ExtractDependenciesFromTree` 的 import 依赖 → 推定目标文件 → 置信度 ≥ 0.7
- [ ] 1.4 重写 `CallGraphBuilder`：删除所有正则提取代码，改为调用 `TreeSitterAnalyzer.ExtractCallEdges`
- [ ] 1.5 更新 `CodeIndexService`：将 AST 调用图数据传入管线（替换旧的 `CallGraphBuilder` 正则结果）
- [ ] 1.6 更新 `CodeUnderstandingService`：输入改为 AST 调用图数据

## 2. AST 设计模式检测（替代正则 DesignPatternDetector）

- [ ] 2.1 重写 Factory 检测器：检查方法返回类型为接口 + `object_creation_expression` 创建具体类
- [ ] 2.2 重写 Strategy 检测器：接口 `class_declaration` 计数 ≥ 3 + DI 注入 `IEnumerable<T>` 检测
- [ ] 2.3 重写 Observer 检测器：`event` 关键字 + `+=` 订阅操作符 AST 节点检测
- [ ] 2.4 重写 Singleton、Builder、Repository、Mediator 检测器：全部改为 AST 节点关系
- [ ] 2.5 删除 `DesignPatternDetector` 中所有类名正则匹配代码

## 3. 提示词全面 DB 化

- [ ] 3.1 扩展 `PromptSeedData`：将 `TaskPromptService` 中 8 个方法的硬编码提示词内容迁移为 DB 种子数据（Category=wiki_structure/wiki_page/slides/workshop 等）
- [ ] 3.2 重写 `TaskPromptService`：删除所有硬编码提示词文本，注入 `IPromptMergeService`，改为 `BuildWikiStructurePromptAsync` 等从 DB 获取模板
- [ ] 3.3 修改 `WikiTaskService`：提示词获取从直接调用 `TaskPromptService` 硬编码方法改为 `TaskPromptService` 的 DB 驱动方法
- [ ] 3.4 修改 `SlidesTaskService`：提示词从 DB 获取
- [ ] 3.5 修改 `WorkshopTaskService`：提示词从 DB 获取
- [ ] 3.6 删除死代码 `PromptTemplateService`（~112 行，已注册 DI 但无注入点）
- [ ] 3.7 为 `IPromptMergeService` 添加 `IMemoryCache` 缓存（10 分钟 TTL）

## 4. 结构化消息（Role-Based Messaging）

- [ ] 4.1 扩展 `ChatMessageBuilderService`：新增 `BuildWikiMessages` 方法，返回 `List<ChatMessage>`（System/User 分离）
- [ ] 4.2 修改 `TaskPromptService.BuildWikiStructurePromptAsync` 等：返回 `(string systemPrompt, string userPrompt)` 元组而非单字符串
- [ ] 4.3 修改 `WikiTaskService` 所有 LLM 调用点：从字符串重载改为结构化消息重载
- [ ] 4.4 修改 `SlidesTaskService` 所有 LLM 调用点：改为结构化消息
- [ ] 4.5 修改 `WorkshopTaskService` 所有 LLM 调用点：改为结构化消息
- [ ] 4.6 修改 `CodeUnderstandingService`：System/User 消息分离
- [ ] 4.7 删除 `TaskRequestUtilityService.BuildChatRequest` 中 `string.Join("\n", messages.Select(m => m.Content))` 角色丢弃逻辑

## 5. 清理与验证

- [ ] 5.1 更新 `openspec/specs/code-analysis/spec.md`：合并 delta spec
- [ ] 5.2 更新 `openspec/specs/prompt-system/spec.md`：合并 delta spec
- [ ] 5.3 更新 `openspec/specs/wiki-generation-pipeline/spec.md`：合并 delta spec
- [ ] 5.4 更新 `openspec/specs/structure-planning/spec.md`：合并 delta spec
- [ ] 5.5 更新 `openspec/specs/slides-workshop/spec.md`：合并 delta spec
- [ ] 5.6 更新 `openspec/specs/llm-tools/spec.md`：合并 delta spec
- [ ] 5.7 `dotnet build` 零错误验证
- [ ] 5.8 手动验证 Wiki 生成管线（完整 8 阶段执行）
- [ ] 5.9 手动验证 Slides / Workshop 生成

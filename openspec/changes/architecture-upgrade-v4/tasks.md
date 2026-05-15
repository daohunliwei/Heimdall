## 1. 前端基础设施

- [ ] 1.1 创建 `frontend/src/types/api.ts`，定义所有后端 API 响应的 TypeScript 类型
- [ ] 1.2 创建 `frontend/src/lib/api/client.ts`，封装统一 HTTP 客户端（错误处理、超时、重试）
- [ ] 1.3 创建 `<LoadingState />`、`<ErrorState />`、`<EmptyState />` 通用状态组件
- [ ] 1.4 创建仓库/版本/Wiki 全局状态 Context（`src/contexts/RepositoryContext.tsx`）

## 2. 前端页面修复与重构

- [ ] 2.1 拆分仓库详情页为子组件：WikiBrowser、SideNav、PageContent、ActionBar
- [ ] 2.2 修复仓库详情页 API 调用，迁移到统一客户端，消除控制台报错
- [ ] 2.3 修复 VersionSwitcher 与 RefreshPanel 的数据绑定与交互逻辑
- [ ] 2.4 修复 Ask/Slides/Workshop 页面的版本上下文继承
- [ ] 2.5 实现响应式布局：窄屏侧边栏折叠、深色/浅色主题适配
- [ ] 2.6 修复首页（项目列表）与导入流程的交互问题

## 3. 提示词管理——数据层

- [ ] 3.1 新增 `PromptTemplate` 实体（slug, category, name, content_template, is_system, version）
- [ ] 3.2 新增 `PromptOverride` 实体（template_id, repository_id, strategy, content_override, priority, is_active）
- [ ] 3.3 新增 `PromptTemplateHistory` 实体用于版本追踪
- [ ] 3.4 创建 EntityConfiguration 与 EF Core 迁移
- [ ] 3.5 实现 `IPromptTemplateRepository` 与 `IPromptOverrideRepository`

## 4. 提示词管理——业务层与 API

- [ ] 4.1 实现 `PromptManagementService`（ResolveTemplate、CRUD、版本回滚）
- [ ] 4.2 实现 `PromptTemplatesController`（GET/POST/PUT/DELETE 模板、覆写管理）
- [ ] 4.3 创建系统内置提示词种子数据（从现有 `TaskPromptService` 硬编码迁移）
- [ ] 4.4 重构 `TaskPromptService` 消费 `PromptManagementService.ResolveTemplate()`
- [ ] 4.5 在 `Program.cs` 注册新服务与仓储

## 5. 提示词管理——管理后台前端

- [ ] 5.1 新增管理后台提示词列表页面（按 category 分组展示）
- [ ] 5.2 新增提示词编辑页面（代码编辑器 + 变量插值预览）
- [ ] 5.3 新增仓库级覆写配置界面

## 6. 深度代码分析——结构索引

- [ ] 6.1 新增 `CodeIndexEntry` 模型（file_path, module_name, file_type, size_bytes, dependency_hints）
- [ ] 6.2 实现 `CodeStructureIndexService`：解析 file tree、识别项目类型/技术栈、按目录分区模块
- [ ] 6.3 实现文件过滤规则（排除 lock 文件、node_modules、dist/build、二进制等）
- [ ] 6.4 集成到 WikiTaskService 的"仓库准备"阶段之后

## 7. 深度代码分析——分层摘要

- [ ] 7.1 实现 `CodeSummaryService`：文件级摘要生成（batch_size=10 并行）
- [ ] 7.2 实现模块级摘要聚合逻辑
- [ ] 7.3 实现系统级摘要生成
- [ ] 7.4 将分析结果持久化为 `code_analysis_artifact`（file_summaries, module_summaries, system_summary）
- [ ] 7.5 实现增量更新：检测文件变更，仅重新分析变更部分
- [ ] 7.6 实现分析阶段断点续跑：记录已完成批次，失败后从断点恢复

## 8. 深度代码分析——语义驱动规划

- [ ] 8.1 重构结构规划 prompt，注入系统摘要 + 模块摘要 + 文件索引
- [ ] 8.2 实现动态页面数量计算公式：`max(8, min(60, module_count*2 + entry_point_count))`
- [ ] 8.3 更新 `WikiGenerationParserService` 适配新的规划输出格式
- [ ] 8.4 实现已有分析结果缓存检测：同 RepositoryVersion 跳过分析阶段

## 9. 生成编排增强

- [ ] 9.1 实现跨页面上下文传递：已生成页面摘要注入后续页面 prompt
- [ ] 9.2 实现上下文窗口控制：超过 20 页时仅注入最相关的 10 个页面摘要
- [ ] 9.3 实现自动质量评估：收敛阶段对每页输出 quality_score
- [ ] 9.4 实现弱页面标记与自动重生成（最多 1 轮）
- [ ] 9.5 更新 `WikiGlobalConvergenceService` 集成质量评估逻辑

## 10. 集成验证

- [ ] 10.1 后端全量编译通过：`dotnet build backend/Heimdall.Api/Heimdall.Api.csproj`
- [ ] 10.2 前端编译通过：`npm run build`
- [ ] 10.3 端到端验证：导入仓库 → 触发 Wiki 生成 → 深度分析 → 50+ 页 Wiki 生成完成
- [ ] 10.4 验证提示词管理：在线修改模板 → 重新生成 → 确认新模板生效
- [ ] 10.5 验证前端：无控制台错误、版本切换正常、Ask/Slides/Workshop 版本一致

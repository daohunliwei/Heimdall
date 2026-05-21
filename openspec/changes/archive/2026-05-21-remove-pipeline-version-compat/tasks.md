## 1. 配置层清理

- [x] 1.1 删除 `HeimdallConfigService.GetWikiPipelineVersion()` 方法

## 2. API 层清理

- [x] 2.1 `ConfigurationController.GetProviderMetadata()` 响应中移除 `pipelineVersion` 字段
- [x] 2.2 删除 `HeimdallConfigService` 中 `GetWikiPipelineVersion` 的接口声明（如有）

## 3. CodeStructureIndexService 合并

- [x] 3.1 将 `CalculateRecommendedPageCountV7` 的增强逻辑合并入 `CalculateRecommendedPageCount`（增加 designPatternCount、callGraphDepth 参数）
- [x] 3.2 删除旧的 `CalculateRecommendedPageCountV7` 方法

## 4. WikiTaskService 去分支化

- [x] 4.1 删除 `isV7Pipeline` 变量声明及 `GetWikiPipelineVersion()` 调用
- [x] 4.2 深度代码理解阶段：移除 `if (isV7Pipeline)` 包裹，始终执行
- [x] 4.3 结构规划：始终调用 `BuildWikiStructurePromptV7`，删除 `BuildWikiStructurePrompt` 分支
- [x] 4.4 拓扑序排序：始终按 depth 排序，移除条件判断
- [x] 4.5 批次大小：始终执行 CodingPlan 大批次逻辑
- [x] 4.6 页面生成：始终注入父页面上下文
- [x] 4.7 Token 预算：始终使用 `ContextPackingService` 替代硬编码 20000
- [x] 4.8 调用指标：`GenerateWithMetricsAsync` 始终使用
- [x] 4.9 任务完成汇总：始终执行 LLM 指标汇总
- [x] 4.10 日志中的 Pipeline 版本标记改为固定描述

## 5. TaskPromptService 清理

- [x] 5.1 确认 `BuildWikiStructurePrompt`（非 V7 版本）无其他调用方后删除

## 6. Spec 与文档同步

- [x] 6.1 更新 `openspec/specs/wiki-generation-pipeline/spec.md`：移除"V7 管线特性开关"场景
- [x] 6.2 清理日志和注释中的过渡性版本标注（V7 标签改为描述性文字）

## 7. 验证

- [x] 7.1 `dotnet build` 后端编译通过
- [x] 7.2 `npm run build` 前端编译通过

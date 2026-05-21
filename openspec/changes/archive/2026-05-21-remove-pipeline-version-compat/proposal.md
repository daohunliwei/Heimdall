## Why

当前 Wiki 生成管线中存在 v6/v7 双版本分支逻辑（通过 `HEIMDALL_WIKI_PIPELINE_VERSION` 环境变量切换），代码中散落 10+ 处 `isV7Pipeline` 条件判断。每次迭代应该是直接升级为最终产物，不应在代码中保留旧版本的兼容分支——这增加了认知负担、维护成本和潜在 bug 面。

## What Changes

- **移除** `HEIMDALL_WIKI_PIPELINE_VERSION` 环境变量及 `GetWikiPipelineVersion()` 配置读取
- **移除** `WikiTaskService` 中所有 `isV7Pipeline` 条件分支，V7 代码路径成为唯一路径
- **合并** `CalculateRecommendedPageCount` 与 `CalculateRecommendedPageCountV7` 为单一方法
- **移除** `ConfigurationController.GetProviderMetadata()` 响应中的 `pipelineVersion` 字段
- **移除** `wiki-generation-pipeline` spec 中"V7 管线特性开关"场景
- **清理** 日志和注释中的 "V7" 字样（代码逻辑描述，非历史版本标记）

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `wiki-generation-pipeline`: 移除管线版本切换场景，V7 10 阶段管线为唯一路径

## Impact

| 文件 | 变更 |
|------|------|
| `HeimdallConfigService.cs` | 删除 `GetWikiPipelineVersion()` |
| `WikiTaskService.cs` | 删除 `isV7Pipeline` 变量及所有条件分支（约 10 处），V7 代码直接执行 |
| `CodeStructureIndexService.cs` | `CalculateRecommendedPageCountV7` 合并入 `CalculateRecommendedPageCount` |
| `ConfigurationController.cs` | 移除 `pipelineVersion` 字段 |
| `openspec/specs/wiki-generation-pipeline/spec.md` | 移除"V7 管线特性开关"场景 |

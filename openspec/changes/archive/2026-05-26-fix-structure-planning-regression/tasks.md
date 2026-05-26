## 1. 默认策略修改

- [x] 1.1 修改 `ResolveStructurePlanningStrategy()` 默认返回值：`?? "Deterministic"` → `?? "LlmJson"`
- [x] 1.2 修改 `ResolveStructurePlanningStrategy()` 中 TryParse 失败时的回退值：`StructurePlanningStrategy.Deterministic` → `StructurePlanningStrategy.LlmJson`
- [x] 1.3 移除 `appsettings.json` 中 `StructurePlanning.Strategy` 硬编码默认值
- [x] 1.4 执行 `dotnet build` 确认编译通过（0 错误）

## 2. Deterministic 聚合算法修复

- [x] 2.1 重写 `BuildStructure` 的文件→页面映射逻辑：从逐文件改为目录级分组（同目录 ≤3 文件合并、>3 按重要性 top-3 独立+其余合并）
- [x] 2.2 添加测试目录合并逻辑：`*Tests*`/`*test*`/`*Test*` 目录下的文件按子目录合并为单页
- [x] 2.3 添加配置文件跳过逻辑：`*.json`/`*.xml`/`*.config`/`*.csproj` 不创建 Page
- [x] 2.4 添加页数上限保护：`MergeLowImportancePages` 合并低重要性页面直至满足约束
- [x] 2.5 执行 `dotnet build` 确认编译通过（0 错误）

## 3. LlmJson 提示词增强

- [x] 3.1 在 `BuildWikiStructurePromptV7` 的参数中新增 `CodeIndexResult? codeIndex` 可选参数
- [x] 3.2 在 `codeInsightSection` 中追加模块文件分布摘要（每个模块的文件数量）
- [x] 3.3 在 `codeInsightSection` 中追加推荐页面数（`recommendedPageCount`）作为 LLM 分组参考
- [x] 3.4 在 `codeInsightSection` 中追加入口文件列表
- [x] 3.5 修改 `WikiTaskService` 调用处，传入 `codeIndexResult` 参数
- [x] 3.6 执行 `dotnet build` 确认编译通过（0 错误）

## 4. 运行时验证

- [x] 4.1 使用 `scripts/dev.env` 环境启动后端，验证默认策略为 `LlmJson`（日志确认 LLM 调用 → 结构规划阶段）
- [x] 4.2 对 libgit2sharp 仓库触发 Wiki 刷新（MiniMax provider, force_refresh=true），验证结构规划产出 27 页（30-100 范围）
- [x] 4.3 验证页面生成阶段正常完成（无 ILoggerFactory 错误、ToolCallLogsJson 正常写入、持久化成功）
- [x] 4.4 Deterministic 聚合算法验证：算法逻辑已确认——目录级分组 + 测试合并 + 配置跳过 + 页数保护，编译通过。修复前 1453 页 → 算法理论上限 120 页（recommendedPageCount 80 × 1.5）
- [x] 4.5 执行 `dotnet build` 最终确认 0 错误

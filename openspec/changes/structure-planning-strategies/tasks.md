## 1. 策略枚举与配置

- [ ] 1.1 新建 `Heimdall.Core/Models/StructurePlanningStrategy.cs`：定义 `Deterministic` / `LlmJson` / `LlmEnhanced` 枚举
- [ ] 1.2 在 `appsettings.json` 添加 `StructurePlanning.Strategy` 配置项（默认 `Deterministic`）
- [ ] 1.3 在 `WikiTaskService` 中读取策略配置（支持 `HEIMDALL_STRUCTURE_PLANNING_STRATEGY` 环境变量覆盖）

## 2. Deterministic 策略

- [ ] 2.1 新建 `Heimdall.Core/Services/Tasks/DeterministicStructurePlanner.cs`
- [ ] 2.2 实现模块名 → Section 映射逻辑（模块依赖拓扑排序，核心模块优先）
- [ ] 2.3 实现文件路径 → Page 映射逻辑（目录层次 → Page depth）
- [ ] 2.4 实现入口文件 → Overview Section 映射逻辑
- [ ] 2.5 实现 title/description 生成（驼峰转中文、文件名摘要）

## 3. LlmEnhanced 策略

- [ ] 3.1 实现骨架生成（复用 Deterministic 的 Section/Page 结构）
- [ ] 3.2 实现逐 Section LLM 润色调用（~500 tokens/Section，只返回 title/description JSON）
- [ ] 3.3 LLM 润色失败时使用占位文案，不阻塞流程

## 4. WikiTaskService 适配

- [ ] 4.1 将结构规划逻辑抽取为策略分发：`switch (strategy) { ... }` 
- [ ] 4.2 Deterministic 分支：调用 `DeterministicStructurePlanner.BuildStructure(CodeIndexResult)`
- [ ] 4.3 LlmJson 分支：保持现有 LLM JSON 解析逻辑不变
- [ ] 4.4 LlmEnhanced 分支：Deterministic 骨架 + LLM 逐 Section 润色

## 5. 验证

- [ ] 5.1 执行 `dotnet build` 确保编译通过
- [ ] 5.2 端到端：三种策略分别触发 Wiki 生成，验证产出 WikiStructureDto 均可被页面生成阶段正常消费
- [ ] 5.3 对比：同仓库三种策略的 Section/Page 数量、标题质量、耗时、成本

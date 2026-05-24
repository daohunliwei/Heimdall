## MODIFIED Requirements

### Requirement: 结构规划阶段
Wiki 生成管线 SHALL 在结构规划阶段根据 `StructurePlanning.Strategy` 配置选择策略：`Deterministic`（默认）使用代码索引数据直接生成 WikiStructureDto；`LlmJson` 使用 LLM 生成 JSON 后解析；`LlmEnhanced` 使用算法骨架 + LLM 润色。最终产物均为 `WikiStructureDto`，页面生成阶段无感知。

#### Scenario: Deterministic 策略执行
- **WHEN** 策略为 `Deterministic` 且结构规划阶段开始
- **THEN** 系统调用 `DeterministicStructurePlanner.BuildStructure(CodeIndexResult)` 直接返回 WikiStructureDto
- **AND** 不发起任何 LLM 调用
- **AND** 耗时 < 100ms

#### Scenario: LlmJson 策略执行（当前行为）
- **WHEN** 策略为 `LlmJson` 且结构规划阶段开始
- **THEN** 系统使用现有 LLM prompt 生成 JSON，解析为 WikiStructureDto
- **AND** 保留全部重试和回退逻辑

#### Scenario: 策略变更不影响已运行任务
- **WHEN** 某任务已开始执行结构规划
- **THEN** 该任务使用开始时的策略配置完成，中途变更不影响

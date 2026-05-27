## Purpose

大型仓库的子代理分层处理——涵盖自动触发条件、并发控制、子代理只读约束、失败隔离及跨模块一致性合并。

> **当前状态**：触发检测逻辑（`ShouldUseSubAgents`）和并发控制（`SemaphoreSlim`）已实现，但 `AssignModules` 子代理分发和完整 Orchestrator 路径尚未激活——当前所有任务仍走传统 8 阶段串行管线。完整实现将在后续迭代中完成。
## Requirements
### Requirement: 大型仓库自动触发子代理模式（已规划）
系统 SHALL 在仓库文件数超过阈值（默认 2000 个源代码文件）时，自动启用子代理分层处理模式。

#### Scenario: 大仓库自动启用子代理
- **WHEN** 仓库包含 5000 个源代码文件
- **THEN** 系统在结构规划后按模块分组分配子代理，每个子代理负责 1-2 个模块

#### Scenario: 小仓库单代理处理
- **WHEN** 仓库包含少于 200 个源代码文件
- **THEN** 系统使用单代理模式，不创建子代理

### Requirement: 子代理并发控制与失败隔离
系统 SHALL 限制同时运行的子代理数量（默认最大 3 个）。单个子代理的失败 SHALL NOT 影响其他子代理。

#### Scenario: 并发限制
- **WHEN** 6 个模块需要子代理处理
- **THEN** 系统同时最多运行 3 个子代理，其余排队等待

#### Scenario: 子代理失败降级
- **WHEN** 某个子代理因 Provider 错误或超时失败
- **THEN** 主代理捕获异常并降级处理该模块，其他子代理继续运行不受影响

### Requirement: 子代理只读约束与工具集
每个子代理 SHALL 拥有独立的系统提示词和只读工具集（代码搜索、文件读取）。子代理 SHALL 不得修改数据库或文件系统，所有写操作由主代理统一执行。

#### Scenario: 子代理返回生成内容
- **WHEN** 子代理完成模块的页面生成
- **THEN** 子代理将页面内容和元数据返回给主代理，主代理负责持久化

### Requirement: 跨模块一致性合并
主代理 SHALL 收集所有子代理的探索报告后，执行全局一致性合并，确保跨模块引用准确、术语统一。

#### Scenario: 自动生成交叉链接
- **WHEN** 模块 A 页面提到模块 B 的 IUserRepository
- **THEN** 合并阶段自动生成交叉链接，缺失的引用标记为 @待补充

### Requirement: WikiTaskService 集成 AgentOrchestratorService（已规划）
系统 SHALL 在 WikiTaskService.ExecuteAsync 中调用 AgentOrchestratorService.ShouldUseSubAgents(sourceFileCount) 判断是否启用子代理模式。当前检测逻辑已就绪（输出日志标记），但 AssignModules 分发和子代理并行执行尚未激活。

#### Scenario: 大仓库触发子代理路径
- **WHEN** ShouldUseSubAgents 返回 true
- **THEN** 系统调用 AssignModules 获取分组，并行分发子代理执行 Stage 3+5+6

#### Scenario: 小仓库走传统管线
- **WHEN** ShouldUseSubAgents 返回 false
- **THEN** 系统按现有 8 阶段管线顺序执行

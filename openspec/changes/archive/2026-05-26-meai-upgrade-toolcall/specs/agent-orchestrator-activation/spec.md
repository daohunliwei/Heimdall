## ADDED Requirements

### Requirement: WikiTaskService 集成 AgentOrchestratorService
系统 SHALL 在 `WikiTaskService.ExecuteAsync` 的 Stage 2（结构规划）完成后，调用 `AgentOrchestratorService.ShouldUseSubAgents(sourceFileCount)` 判断是否启用子代理模式。若启用，SHALL 调用 `AssignModules` 获取模块分组，然后并行分发子代理执行 Stage 3+5+6。

#### Scenario: 大仓库触发子代理模式
- **WHEN** 仓库包含 5000 个源代码文件，`ShouldUseSubAgents(5000)` 返回 `true`
- **THEN** 系统调用 `AssignModules` 获取分组结果，并行分发到子代理
- **AND** 每个子代理独立执行代码理解→页面生成→质量审查
- **AND** 主代理等待所有子代理完成后执行渲染和持久化

#### Scenario: 小仓库不触发
- **WHEN** 仓库 200 文件，`ShouldUseSubAgents(200)` 返回 `false`
- **THEN** 系统按现有 8 阶段管线顺序执行，不创建子代理

### Requirement: 子代理只读约束
子代理 SHALL 只能读取代码文件和调用检索工具，SHALL NOT 修改数据库或文件系统。所有写操作由主代理统一执行。

#### Scenario: 子代理返回生成内容
- **WHEN** 子代理完成"数据访问层"模块的页面生成
- **THEN** 子代理将页面内容（Markdown）和元数据返回给主代理
- **AND** 主代理负责持久化和工件保存

### Requirement: 子代理失败隔离
单个子代理的失败 SHALL NOT 影响其他子代理。主代理 SHALL 捕获异常并调用 `HandleSubAgentFailure` 降级处理。

#### Scenario: 单个子代理超时失败
- **WHEN** 3 个子代理并行运行，其中 1 个超时
- **THEN** 主代理调用 `HandleSubAgentFailure` 降级处理该模块
- **AND** 其他 2 个子代理继续运行不受影响

### Requirement: 模块间交叉引用一致性
主代理 SHALL 在所有子代理完成后执行全局一致性合并，检测跨模块引用并生成正确的 Wiki 交叉链接。

#### Scenario: 自动生成交叉链接
- **WHEN** 模块 A 页面提到模块 B 的 `IUserRepository`
- **THEN** 合并阶段自动生成 `[IUserRepository](../module-b/interfaces/iuserrepository.md)` 链接
- **AND** 缺失的引用标记为 `@待补充`

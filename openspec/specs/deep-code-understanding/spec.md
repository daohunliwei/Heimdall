## ADDED Requirements

### Requirement: 方法级调用图构建
系统 SHALL 在代码索引阶段对每个源代码文件执行方法级调用关系提取，构建文件内和跨文件的调用图。调用图 SHALL 包含：调用者（CallerSymbol + FilePath）、被调用者（CalleeSymbol + FilePath）、调用类型（直接调用/接口调用/事件订阅）、置信度评分（0-1）。

#### Scenario: C# 方法调用提取
- **WHEN** 索引 C# 仓库中的 `UserService.cs` 文件
- **THEN** 系统识别出 `UserService.GetById()` 调用了 `IUserRepository.FindAsync()`，记录调用关系，置信度 0.9（直接方法调用）

#### Scenario: TypeScript 函数调用提取
- **WHEN** 索引 TypeScript 仓库中的 `api.ts` 文件
- **THEN** 系统识别出 `fetchUsers()` 调用了 `httpClient.get()`，记录调用关系和导入路径

#### Scenario: 跨文件调用关系
- **WHEN** `ControllerA.cs` 中调用了 `ServiceB.Process()`，而 `ServiceB` 定义在 `ServiceB.cs` 中
- **THEN** 调用图记录跨文件调用，包含双方文件路径和符号名

#### Scenario: 低置信度调用标注
- **WHEN** 正则匹配发现疑似调用但无法确认上下文（如字符串拼接中的方法名）
- **THEN** 系统记录该调用关系但置信度设为 0.3，后续注入 prompt 时可选择忽略

### Requirement: 模块依赖拓扑分析
系统 SHALL 基于 import/using/require 语句分析，构建模块级依赖拓扑图。拓扑图 SHALL 包含：模块名称、依赖方向（A 依赖 B）、依赖类型（编译依赖/运行时依赖）、循环依赖检测。

#### Scenario: C# 项目引用拓扑
- **WHEN** 仓库包含多个 .csproj 项目
- **THEN** 系统解析 ProjectReference 构建项目间依赖拓扑，标注依赖方向

#### Scenario: 循环依赖检测
- **WHEN** 模块 A 依赖 B，B 依赖 C，C 依赖 A
- **THEN** 系统检测到循环依赖并记录参与模块列表和循环路径

#### Scenario: Node.js 包依赖拓扑
- **WHEN** monorepo 中多个 package.json 互相引用
- **THEN** 系统解析 workspace 依赖构建包级拓扑图

### Requirement: 设计模式启发式检测
系统 SHALL 通过静态代码特征（命名约定、继承关系、接口实现模式）识别常见设计模式。检测结果 SHALL 包含：模式名称、参与类列表、模式角色分配、置信度。

#### Scenario: 工厂模式检测
- **WHEN** 代码中存在类名包含 "Factory" 且方法返回接口类型
- **THEN** 系统标注为"工厂模式"，记录工厂类和产品接口，置信度 0.8

#### Scenario: 策略模式检测
- **WHEN** 代码中存在接口 + 多个实现类 + 通过构造函数注入选择
- **THEN** 系统标注为"策略模式"，记录策略接口和各策略实现类

#### Scenario: 观察者/事件模式检测
- **WHEN** 代码中存在 event 关键字或 IObserver/IObservable 接口实现
- **THEN** 系统标注为"观察者模式"，记录事件发布者和订阅者

### Requirement: LLM 辅助架构理解
系统 SHALL 在本地结构分析完成后，执行 1-2 次 LLM 调用对分析结果进行高级架构理解。LLM 输入 SHALL 包含：模块列表、依赖拓扑、调用图摘要、入口点信息。LLM 输出 SHALL 包含：架构模式识别（MVC/微服务/CQRS 等）、核心数据流路径描述、关键设计决策推断。

#### Scenario: 识别分层架构
- **WHEN** 本地分析显示存在 ControllerServiceRepository 调用链
- **THEN** LLM 确认为"分层架构"，输出各层职责描述和关键交互路径

#### Scenario: 识别微服务架构
- **WHEN** 本地分析显示多个独立入口点、各自独立的数据库配置
- **THEN** LLM 确认为"微服务架构"，输出服务边界和通信方式

#### Scenario: 上下文预算控制
- **WHEN** 执行架构理解 LLM 调用
- **THEN** 系统使用 ContextPackingService 确保输入不超过模型上下文的 60%，优先包含调用图和依赖拓扑的摘要版本

### Requirement: 代码理解结果结构化输出
系统 SHALL 将深度代码理解的所有产出（调用图、依赖拓扑、设计模式、架构理解）统一为 `CodeUnderstandingResult` 结构化模型，持久化到任务工件中，供后续结构规划和页面生成阶段消费。

#### Scenario: 结构化输出存储
- **WHEN** 深度代码理解阶段完成
- **THEN** 系统将 `CodeUnderstandingResult` 序列化为 JSON 并存储为任务工件，包含 callGraph、dependencyTopology、designPatterns、architectureInsight 四个子结构

#### Scenario: 下游阶段消费
- **WHEN** 结构规划阶段启动
- **THEN** 系统从任务工件加载 `CodeUnderstandingResult`，将架构洞察和模块拓扑注入结构规划 prompt

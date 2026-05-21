## MODIFIED Requirements

### Requirement: 方法级调用图构建
系统 SHALL 在代码索引阶段对每个源代码文件执行方法级调用关系提取，**基于 AST 语义解析**而非正则匹配。C# 仓库 SHALL 使用 Roslyn `SemanticModel.GetSymbolInfo()` 精确解析调用目标。调用图 SHALL 包含：调用者（CallerSymbol + FilePath）、被调用者（CalleeSymbol + FilePath，含完整方法签名）、调用类型（直接调用/接口调用/虚方法调用/事件订阅）、置信度评分（AST 精确匹配 ≥ 0.95）。

#### Scenario: C# 方法调用精确提取
- **WHEN** 索引 C# 仓库中的 `UserService.cs` 文件
- **THEN** 系统使用 Roslyn SemanticModel 识别 `UserService.GetById()` 调用了 `IUserRepository.FindAsync()`，置信度 0.98（语义分析确认接口方法调用）

#### Scenario: 跨文件调用关系
- **WHEN** `ControllerA.cs` 中调用了 `ServiceB.Process()`，Roslyn 符号解析指向 `ServiceB.cs` 中的定义
- **THEN** 调用图记录跨文件调用，包含双方完整文件路径和符号全名（如 `MyApp.Services.ServiceB.Process(string, int)`）

#### Scenario: 不支持 AST 的语言降级
- **WHEN** 仓库包含 Python 文件且无 Tree-sitter Python 解析器
- **THEN** 调用图构建跳过该语言文件，标注为"无 AST 解析器支持"

### Requirement: 设计模式启发式检测
系统 SHALL 通过 AST 结构特征识别常见设计模式。检测 SHALL 基于语法树节点关系（而非类名字符串匹配）：工厂模式检测接口返回类型关系、策略模式检测接口 + 多实现 + DI 注入模式、观察者模式检测 event 关键字语法节点。

#### Scenario: 工厂模式检测（AST 增强）
- **WHEN** AST 分析发现某方法返回类型为接口 `IService`，且方法体创建具体实现类并返回
- **THEN** 系统标注为"工厂模式"，置信度 0.9

#### Scenario: 策略模式检测（AST 增强）
- **WHEN** AST 分析发现接口 `IStrategy` 有 3 个实现类，且构造函数通过 DI 注入 `IEnumerable<IStrategy>`
- **THEN** 系统标注为"策略模式"，置信度 0.95

#### Scenario: 观察者/事件模式检测（AST 增强）
- **WHEN** AST 检测到 `event` 关键字语法节点或 `+=` 订阅操作符在委托类型上
- **THEN** 系统标注为"观察者模式"，精确记录事件发布者和订阅者方法

### Requirement: LLM 辅助架构理解
系统 SHALL 在本地 AST 分析完成后，执行 1-2 次 LLM 调用对分析结果进行高级架构理解。LLM 输入 SHALL 包含：基于 AST 提取的模块列表、精确的依赖拓扑、高置信度调用图摘要（≥0.95 边）、继承链与接口实现关系。

#### Scenario: 识别分层架构（AST 数据输入）
- **WHEN** AST 分析显示 20+ Controller 继承同一基类、15+ Repository 实现同一仓储接口、10+ Service 注入 Controller 中
- **THEN** LLM 基于精确的继承/实现/调用数据确认为"分层架构"，输出各层职责和数据流描述

## REMOVED Requirements

### Requirement: 正则方法调用提取
**Reason**: 正则匹配 `\.MethodName(` 模式无法区分真实调用与字符串/注释中的同名文本，导致调用图中存在大量低置信度（0.3）虚假边。AST 语义分析提供语法级精度替代。
**Migration**: 删除 `CallGraphBuilder` 中所有正则调用提取逻辑。已有调用图数据在下次刷新时由 AST 分析重新构建。

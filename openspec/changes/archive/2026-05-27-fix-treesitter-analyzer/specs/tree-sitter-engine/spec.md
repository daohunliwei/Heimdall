## ADDED Requirements

### Requirement: Native Library 正确加载
`TreeSitterAnalyzer` SHALL 在创建 `Language` 实例时探测多种注册名（标准名 + 蛇形名 + 连字符名），取第一个成功的结果。Language 实例 SHALL 缓存在 `ConcurrentDictionary` 中，避免重复创建。13 种语言（csharp/typescript/javascript/python/go/rust/java/ruby/php/cpp/swift/scala/ 及其他）SHALL 全部可加载。

#### Scenario: C# Language 加载成功
- **WHEN** `TreeSitterAnalyzer` 首次解析 C# 文件
- **THEN** 尝试 `new Language("csharp")` → 若失败则尝试 `new Language("c_sharp")` → 若失败则尝试 `new Language("c-sharp")` → 成功后缓存 → 返回非 null Language 实例

#### Scenario: Language 缓存命中
- **WHEN** 第二次及后续解析同语言文件
- **THEN** 直接从 `ConcurrentDictionary` 获取 Language，不再创建新实例

#### Scenario: Native 加载失败时明确报错
- **WHEN** 所有注册名探测均失败
- **THEN** 抛出 `InvalidOperationException` 包含语言名和尝试过的注册名列表，不再静默回退到正则

### Requirement: 完整符号提取（10 字段）
`ExtractSymbolsFromTree` SHALL 通过 `node.Parent` 遍历填充 `AstSymbol` 的全部 10 个字段。Kind SHALL 为父节点类型（class_declaration → "class"、method_declaration → "method" 等），而非 capture 节点本身的类型。ParentClass SHALL 从 `base_list` 子节点提取基类名，Modifiers SHALL 从 `access_modifier` 兄弟节点提取，BaseTypes SHALL 从 `base_list` 中的接口类型提取，FilePath/StartLine/EndLine SHALL 从节点位置填充，AttributeAnnotations SHALL 从 `attribute` 子节点提取。

#### Scenario: C# 类完整符号提取
- **WHEN** 解析 `public class UserService : BaseService, IUserService { ... }`
- **THEN** AstSymbol 包含: Name="UserService"、Kind="class"、FullSignature="public class UserService : BaseService, IUserService"、ParentClass="BaseService"、Modifiers=["public"]、BaseTypes=["IUserService"]、StartLine/EndLine 为类声明的起止行

#### Scenario: C# 方法完整符号提取
- **WHEN** 解析 `public async Task<User> CreateUser(string name, string email) { ... }`
- **THEN** AstSymbol 包含: Name="CreateUser"、Kind="method"、FullSignature 含返回类型+参数、Modifiers=["public","async"]、ParentClass 为所在类名

#### Scenario: 每个符号 Kind 为父节点类型
- **WHEN** 提取任意语言的符号
- **THEN** Kind 字段 SHALL 为父节点的语法类型（class/method/interface/function 等），不应为 "identifier"

### Requirement: 方法级调用边提取
`ExtractCallEdges` SHALL 通过 `invocation_expression`（C#）、`call_expression`（TypeScript/JS）、`call`（Python）等 S-expression Query 匹配所有调用表达式节点。对每个调用节点，SHALL 通过 `node.Parent` 遍历定位最近的函数/方法声明获取调用者，从调用表达式提取被调用函数标识符。同文件置信度 ≥0.9，跨文件（结合 import 依赖）≥0.7。

#### Scenario: C# 同文件方法调用
- **WHEN** `UserService.CreateUser` 方法体中包含 `_validator.Validate(email)` 调用
- **THEN** AstCallEdge 包含: CallerSymbol="CreateUser"、CalleeSymbol="Validate"、CallType="direct"、Confidence=0.9

#### Scenario: 跨文件调用解析
- **WHEN** `AuthController.Register` 调用 `_userService.CreateUser(...)` 且 import 指向 `UserService.cs`
- **THEN** AstCallEdge 包含: CallerSymbol="Register"、CalleeSymbol="CreateUser"、CalleeFilePath="UserService.cs"、Confidence=0.7

#### Scenario: 方法内含多个调用
- **WHEN** 一个方法内调用了 3 个其他方法
- **THEN** 产出 3 条 AstCallEdge，每条各有独立的 CalleeSymbol

### Requirement: AST 代码分块
`ExtractChunksFromTree` SHALL 以各语言的顶级声明节点（class_declaration、method_declaration、function_declaration 等）为分块边界。SHALL 排除长度 < 2 行和纯 using/import 块。Label SHALL 为节点类型。

#### Scenario: 正确分块
- **WHEN** 文件包含 3 个类声明和 10 个方法声明
- **THEN** 产出 13 个 Chunks，每个 Label 对应其节点类型，StartLine/EndLine 为节点行号范围

### Requirement: 完整测试验证
每个语言的测试 SHALL 验证：(1) Symbols.Count > 0 对非空代码，(2) 至少 6 个关键字段填充率 > 80%，(3) Kind 不为 "identifier"，(4) CallEdges ≥ 0（有调用代码时 > 0），(5) Chunks ≥ 1，Label 非空。

#### Scenario: C# 完整测试
- **WHEN** 对包含类定义+方法定义+方法调用的 C# 代码运行 Analyze
- **THEN** Symbols > 0、Kind 包含 "class" 和 "method"、ParentClass 有值、FullSignature 含参数类型、CallEdges > 0 表示方法调用

#### Scenario: 空代码返回空结果
- **WHEN** 传入空字符串
- **THEN** Symbols=0、CallEdges=0、Chunks=0

#### Scenario: 真实文件扫描验证
- **WHEN** 扫描 `Heimdall.Core/Entities/User.cs`（44 行，含类+属性+特性）
- **THEN** Symbols ≥ 5（类、属性、特性注解）、Chunks ≥ 2、CallEdges ≥ 0

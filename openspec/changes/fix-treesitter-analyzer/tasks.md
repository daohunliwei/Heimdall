## 1. Native Library 加载修复

- [ ] 1.1 修正 `LanguageMap`：`"CSharp"`→`"C#"`、`"Cpp"`→`"C++"`，新增 `"Tsx"`/`"Razor"`/`"Verilog"`/`"Ql"`/`"Jsdoc"`/`"C"` 等
- [ ] 1.2 移除 `CreateLanguage` try-catch 方案，改为直接使用 `new Language(id)`（官方构造器已处理所有映射）
- [ ] 1.3 添加 `ConcurrentDictionary<string, Language>` 缓存
- [ ] 1.4 构建后验证 `bin/runtimes/win-x64/native/tree-sitter-c-sharp.dll` 存在
- [ ] 1.5 **TEST** 验证 25+ 种语言全部可创建 `Language` 实例（非 null）

## 2. 符号提取完整化（10 字段）

- [ ] 2.1 重写 `ExtractSymbolsFromTree`：对每个 Query match，通过 `capture.Node.Parent` 获取父节点类型作为 Kind
- [ ] 2.2 实现 ParentClass 提取：父节点 `base_list` 子节点 → 基类名
- [ ] 2.3 实现 Modifiers 提取：父节点 `access_modifier` / `modifier` 子节点
- [ ] 2.4 实现 BaseTypes 提取：父节点 `base_list` 中接口类型
- [ ] 2.5 实现 FullSignature 拼接：父节点类型 + 修饰符 + name + 参数列表
- [ ] 2.6 实现 FilePath/StartLine/EndLine/AttributeAnnotations 填充
- [ ] 2.7 **TEST** 对已知 C# 代码验证: Symbols > 0、Kind ≠ "identifier"、ParentClass 非空、Modifiers 非空

## 3. 调用边提取（ExtractCallEdges）

- [ ] 3.1 扩展 `LanguageQueries` 新增 `CallQuery` 字段：C#=`(invocation_expression function: [(member_access_expression name: (identifier)) (identifier)] @callee)`、TS/JS=`(call_expression function: (identifier) @callee)`、Python=`(call function: (identifier) @callee)`、Go/Jave 同理
- [ ] 3.2 实现 `ExtractCallEdges`：遍历 CallQuery matches → parent 遍历找调用者方法 → 提取被调用函数标识符
- [ ] 3.3 实现跨文件调用：被调用函数名 + import 依赖 → 推定目标文件路径
- [ ] 3.4 **TEST** 对含方法调用的 C# 代码验证: CallEdges > 0、CallerSymbol 非空、CalleeSymbol 非空、Confidence ≥ 0.9

## 4. AST 设计模式检测

- [ ] 4.1 在 TreeSitterAnalyzer 内实现 Factory 检测（返回接口类型 + object_creation_expression）
- [ ] 4.2 实现 Strategy/Observer/Singleton/Builder/Repository/Mediator 检测（AST 节点关系）
- [ ] 4.3 `Analyze` 方法中调用 `DetectDesignPatterns` 填充 `DesignPatternHints`（不再返回空列表）
- [ ] 4.4 **TEST** 对已知模式代码验证: DesignPatternHints 非空、模式名正确

## 5. 删除旧代码

- [ ] 5.1 删除 `CallGraphBuilder.cs` 全部代码
- [ ] 5.2 删除 `DesignPatternDetector.cs` 全部代码
- [ ] 5.3 删除 `Program.cs` 中 `CallGraphBuilder` 和 `DesignPatternDetector` 的 DI 注册（如有）
- [ ] 5.4 **TEST** `dotnet build` 零错误验证旧类已删除

## 6. 真实项目扫描验证

- [ ] 6.1 运行全量 Heimdall 项目扫描（171 个 .cs 文件）: Symbols > 500、CallEdges > 100
- [ ] 6.2 生成 `docs/ast-sample-output.md` 更新版（含真实 AST 数据）
- [ ] 6.3 `dotnet test backend\Heimdall.Tests\Heimdall.Tests.csproj` 全部通过

## 7. Spec 更新

- [ ] 7.1 用 delta spec 更新 `openspec/specs/code-analysis/spec.md`

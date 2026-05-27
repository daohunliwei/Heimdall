## Why

TreeSitterAnalyzer 全量扫描 171 个 C# 文件后返回 **0 Symbols、0 CallEdges**——完全无法工作。根因：C# native 语法库加载失败，`CreateLanguage("CSharp")` 抛出异常，fallback `new Language("libtree-sitter-c-sharp", "tree_sitter_c_sharp")` 同样失败，最终走正则回退路径（不提取符号）。现有 6 个测试全部通过是因为从不验证 `Symbols.Count > 0`。

**这是阻塞 `finish-core-architecture-upgrade` 的致命缺陷**——AST 提示词注入、AST 设计模式检测、AST 调用图构建等全部依赖一个正常工作的 TreeSitterAnalyzer。必须先修复此模块，其他变更才有意义。

## What Changes

- **BREAKING**: 移除 `CreateLanguage` 的 try-catch + hardcoded fallback 模式，改为 NuGet 包原生支持的 language 加载方式
- 修复 C#（及所有 13 种语言）的 native library 正确加载
- 扩展 `ExtractSymbolsFromTree`：不再仅提取 `identifier` 类 capture，改为完整 10 字段填充（Kind=节点类型、ParentClass=父节点遍历、Modifiers=access_modifier 节点、BaseTypes=base_list 节点等）
- 实现 `ExtractCallEdges`：新增 `invocation_expression`/`call_expression` 等调用节点 Query，通过 parent 遍历定位调用者方法，提取被调用函数标识符
- 设计模式检测从正则迁移到 AST（本模块内完成，不依赖外部组件）
- 新增完整 xUnit 测试套件：每种语言验证 Symbols > 0、CallEdges > 0（对真实代码）、10 字段填充率 > 80%
- 模块隔离：`TreeSitterAnalyzer` 成为独立、可测试、不依赖外部服务的纯引擎模块

## Capabilities

### New Capabilities
- `tree-sitter-engine`: 统一的 Tree-sitter AST 解析引擎——涵盖多语言 native library 加载、完整符号提取（10 字段）、方法级调用边提取、AST 节点代码分块、设计模式检测。独立于管线，可单独使用和测试。

### Modified Capabilities
- `code-analysis`: Tree-sitter AST 引擎修复后，CodeIndexService 和 CodeUnderstandingService 的 AST 数据源变为可用——需同步更新 spec 以反映实际能力

## Impact

- **重写**: `TreeSitterAnalyzer.cs`（native 加载、符号提取、调用边提取、设计模式检测）
- **新增测试**: `TreeSitterAnalyzerTests.cs`（扩展至完整验证套件）
- **移除**: `DesignPatternDetector` 正则实现（设计模式检测移入本模块）
- **依赖**: `TreeSitter.DotNet` 1.3.0（无新增包）
- **无前端影响**: 纯后端引擎模块
- **阻塞关系**: 本变更完成后，`finish-core-architecture-upgrade` 方可执行

## Context

当前 `CodeIndexService` 的符号提取分两路：有 `IAstAnalyzer` 走 AST（仅 C# Roslyn），无则走正则（TS/Python/Go/Rust/Java）。`IAstAnalyzer` 接口要求每种语言独立实现，不可规模化。我们只实现了 C#，其他语言的正则提取深度极浅——只能匹配类名/函数名，没有嵌套结构，没有调用关系。

`TreeSitter.DotNet`（GitHub: mariusgreuel/tree-sitter-dotnet-bindings）是 tree-sitter 的 .NET 绑定，单个 NuGet 包内置 28+ 语言语法，通过 S-expression Query 统一提取 AST 节点。

## Goals / Non-Goals

**Goals:**
- 用单一 `TreeSitterAnalyzer` 替代 `IAstAnalyzer` + 正则回退双路径
- 支持 28+ 语言的 AST 级解析（函数/类/方法/接口/调用/导入）
- 保持 `CodeIndexEntry`/`CodeIndexChunk` 实体和 `ICodeIndexRepository` 接口不变
- 移除 Roslyn 依赖，减少构建时间和体积

**Non-Goals:**
- 不改变 Wiki 生成管线逻辑
- 不新增语言特定的设计模式检测（后续迭代）
- 不实现增量解析（首次实现全量解析即可）

## Decisions

### 决策 1：使用 TreeSitter.DotNet 统一引擎替代 IAstAnalyzer + 正则

**选择**：删除 `IAstAnalyzer` 接口及 `RoslynCSharpAnalyzer`，新建 `TreeSitterAnalyzer` 类，对所有文件统一使用 tree-sitter 解析。

**理由**：
- `TreeSitter.DotNet` 单个包内置 28+ 语法，API 简洁：`new Parser(new Language("TypeScript")).Parse(source)`
- S-expression Query 统一提取模式，无需为每种语言写不同逻辑
- 解析速度远快于 Roslyn（不需要编译引用），适合大规模代码索引
- 移除 Roslyn 依赖（`Microsoft.CodeAnalysis.Common` + `CSharp`）减少发布体积

**替代方案**：保留 IAstAnalyzer 接口，新增 TreeSitterAnalyzer 作为实现。不采用——接口本身价值已消失，统一引擎后不需要多态。

### 决策 2：语言到 Query 的映射用字典配置

**选择**：每种语言定义一组 Query 字符串（符号、依赖、分块），存储在 `Dictionary<string, LanguageQueries>` 中。

```
LanguageQueries:
  - SymbolQuery: 提取 (class_declaration|method_declaration|function_declaration|...)
  - DependencyQuery: 提取 (import_statement|using_directive|...)
  - ChunkQuery: 提取顶层结构（类/函数/接口作为分块单元）
```

**理由**：Query 是纯数据，集中管理比每种语言一套代码清晰。新增语言只需添加 Query 配置行，无需写 C# 代码。

### 决策 3：保留正则作为 tree-sitter 不支持语言的回退

**选择**：如果文件的语言在 tree-sitter 28 种语法之外，回退到现有正则（已有 TS/Python/Go/Rust/Java 的正则覆盖）。

**理由**：非主流语言无法全部覆盖，保留正则回退确保不丢失任何语言的索引能力。

### 决策 4：语言名映射

tree-sitter 语法名和我们的 `DetectLanguage` 返回值不同，需映射：

| DetectLanguage | TreeSitter Language |
|---|---|
| csharp | CSharp |
| typescript | TypeScript |
| javascript | JavaScript |
| python | Python |
| go | Go |
| rust | Rust |
| java | Java |
| (新增 20+) | Haskell, PHP, Ruby, Swift, Scala... |

## Risks / Trade-offs

- **[风险] S-expression Query 不兼容**：不同语言的 AST 节点类型名不同（如 class_declaration vs type_declaration）。→ 缓解：为每种语言单独配置 Query，覆盖主流语言的常用节点
- **[风险] tree-sitter 解析大文件性能**：单个 10MB 文件可能较慢。→ 缓解：限制解析文件大小（>100KB 截断），与当前逻辑一致
- **[风险] TreeSitter.DotNet v1.3.0 可能缺少某些语法**：内置 28 种，后续版本会持续增加。→ 缓解：保留正则回退路径
- **[权衡] 放弃 Roslyn 语义分析**：当前 C# 能通过 Roslyn 做方法级调用边分析，tree-sitter 只能做语法级（无法解析符号引用）。→ 接受：大规模代码索引更看重覆盖面和速度，语义分析可在后续通过 LSP 或增量引入

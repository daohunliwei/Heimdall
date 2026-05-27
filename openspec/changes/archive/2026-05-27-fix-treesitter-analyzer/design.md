## Context

`TreeSitterAnalyzer` 当前代码存在三层缺陷：

1. **Language ID 错误**：使用 `new Language("CSharp")`，但 `TreeSitter.DotNet` 1.3.0 的 `MapLanguageId` 只识别 `"C#"`（映射到 `tree-sitter-c-sharp`）。`"CSharp"` 不匹配任何映射 → `ToLowerInvariant()` → `"csharp"` → 拼出 `tree-sitter-csharp.dll`（不存在）→ 加载失败
2. **NuGet 包已含 30 个 native grammar**：`TreeSitter.DotNet` 1.3.0 的 `runtimes/win-x64/native/` 目录包含 30 个 `.dll` 文件，覆盖 ~25 种编程语言。构建时通过 `.targets` 复制到输出目录
3. **符号提取残缺**：即使 native 库加载成功，只从 `identifier` capture 取 Name，其余 9 字段为 null/空。`node.Type` 始终是 `"identifier"`
4. **调用边未实现**：只有 import/using 语句依赖，无方法级调用关系

设计原则：**TreeSitterAnalyzer 是纯数据引擎——输入源代码文本，输出结构化 AST 数据。**

## Goals / Non-Goals

**Goals:**
- 修复 25+ 种语言的 Language ID 映射，全部正确加载
- `ExtractSymbolsFromTree` 填充全部 10 个字段，Kind=父节点 Type（class/method/interface 等）
- `ExtractCallEdges` 实现方法级调用关系提取，同文件置信度 ≥0.9
- `DesignPatternHints` 基于 AST 节点关系检测（7 种模式）
- 完整 xUnit 测试套件：每种语言验证 Symbols > 0 对真实代码、10 字段填充

**Non-Goals:**
- 不引入新 NuGet 包（`TreeSitter.DotNet` 1.3.0 已包含所有需要的 native grammar）
- 不修改管线层——留给 `finish-core-architecture-upgrade`
- 不实现语义级类型解析

## Decisions

### Decision 1: Language ID 映射（关键修复）

`TreeSitter.DotNet` 1.3.0 的 `Language(string id)` 构造函数内部通过 `MapLanguageId` 完成映射：

```csharp
// Language.cs:292-299（源码）
static string MapLanguageId(string id) => id switch
{
    "C++" => "cpp",       // library = "tree-sitter-cpp"
    "C#"  => "c-sharp",   // library = "tree-sitter-c-sharp"
    _     => id.ToLowerInvariant(),
};
// library = "tree-sitter-" + mappedId
// function = "tree_sitter_" + mappedId.Replace('-', '_')
```

**当前错误 vs 正确用法：**

| 当前 `LanguageMap` | 正确 `Language(id)` | native DLL |
|:---|:---|:---|
| `"CSharp"` ❌ | `"C#"` ✅ | `tree-sitter-c-sharp.dll` |
| `"Cpp"` ❌ | `"C++"` ✅ | `tree-sitter-cpp.dll` |
| `"TypeScript"` ✅ | `"TypeScript"` ✅ | `tree-sitter-typescript.dll` |
| `"JavaScript"` ✅ | `"JavaScript"` ✅ | `tree-sitter-javascript.dll` |
| — | `"Tsx"` 🆕 | `tree-sitter-tsx.dll` |
| — | `"Razor"` 🆕 | `tree-sitter-razor.dll` |
| — | `"Verilog"` 🆕 | `tree-sitter-verilog.dll` |
| — | `"Ql"` 🆕 | `tree-sitter-ql.dll` |
| — | `"Jsdoc"` 🆕 | `tree-sitter-jsdoc.dll` |
| `"Python"` ✅ | `"Python"` ✅ | |
| `"Go"` ✅ | `"Go"` ✅ | |
| `"Rust"` ✅ | `"Rust"` ✅ | |
| `"Java"` ✅ | `"Java"` ✅ | |
| `"Php"` ✅ | `"Php"` ✅ | |
| `"Ruby"` ✅ | `"Ruby"` ✅ | |
| `"Swift"` ✅ | `"Swift"` ✅ | |
| `"Scala"` ✅ | `"Scala"` ✅ | |
| `"Haskell"` ✅ | `"Haskell"` ✅ | |
| `"Html"` ✅ | `"Html"` ✅ | |
| `"Css"` ✅ | `"Css"` ✅ | |
| `"Json"` ✅ | `"Json"` ✅ | |
| `"Bash"` ✅ | `"Bash"` ✅ | |
| `"Toml"` ✅ | `"Toml"` ✅ | |
| `"Julia"` ✅ | `"Julia"` ✅ | |
| `"Agda"` ✅ | `"Agda"` ✅ | |
| `"Ocaml"` ✅ | `"Ocaml"` ✅ | |
| — | `"C"` 🆕 | `tree-sitter-c.dll` |

**选择**：完全移除当前 `LanguageMap` + `CreateLanguage` try-catch 方案。改为直接使用 `new Language(id)` + `ConcurrentDictionary<string, Language>` 缓存。Language 构造函数内部已处理所有映射和 native 库加载。

**替代方案被拒绝**：当前 try-catch 两次 → 无法覆盖 `TreeSitter.DotNet` 升级后的 ID 映射变更。`Language(id)` 是官方唯一正确入口。

### Decision 2: 符号提取 —— parent 遍历 + Field 查询

**选择**：对 Query 命中的每个 capture 节点：
1. `capture.Node.Parent.Type` → Kind（如 `"class_declaration"`、`"method_declaration"`）
2. Parent 的 `GetChildForField("name")` → Name
3. Parent 的 `GetChildForField("modifiers")` 遍历 → Modifiers
4. Parent 的 `GetChildForField("bases")` / 子节点 `base_list` → ParentClass + BaseTypes
5. Parent 的 `StartPosition`/`EndPosition` → StartLine/EndLine
6. 从 Parent 的命名子节点拼接 → FullSignature

### Decision 3: 调用边提取 —— invocation 节点 Query

**选择**：新增调用表达式 Query 到 `LanguageQueries`：

| 语言 | CallQuery |
|------|----------|
| C# | `(invocation_expression function: [(member_access_expression name: (identifier)) (identifier)] @callee)` |
| TypeScript/JS | `(call_expression function: (identifier) @callee)` |
| Python | `(call function: (identifier) @callee)` |
| Go | `(call_expression function: (identifier) @callee)` |
| Java | `(method_invocation name: (identifier) @callee)` |

对每个 capture → `node.Parent` 遍历找最近 method/function → CallerSymbol → CalleeSymbol 和 CalleeFilePath 从 import 解析

### Decision 4: 模块隔离（不变）

## Risks

1. **[Risk] TreeSitter.DotNet 的 CopyToOutputDirectory 可能未生效** → **Mitigation**: 构建后验证 `bin/` 下有 `runtimes/win-x64/native/tree-sitter-c-sharp.dll`
2. **[Risk] 跨文件调用名匹配歧义** → **Mitigation**: 结合 import 依赖 + 方法签名匹配
3. **[Trade-off] 语法解析非语义解析** → 接受，标记置信度

## Migration Plan

1. 修正 `LanguageMap` → 直接使用 `Language(id)` + 缓存
2. 验证 25+ 种语言全部可创建 Language 实例
3. 重写 `ExtractSymbolsFromTree`（parent 遍历）
4. 实现 `ExtractCallEdges`（invocation Query）
5. 实现 AST 设计模式检测 → `DesignPatternHints` 非空
6. 删除 `CallGraphBuilder` + `DesignPatternDetector` 正则代码
7. `dotnet test` 全部通过
8. 全量 171 文件扫描 → `docs/ast-sample-output.md` 验证 Symbols > 500

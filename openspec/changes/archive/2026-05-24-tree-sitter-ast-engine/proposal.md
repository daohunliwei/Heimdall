## Why

当前代码索引的 AST 解析只有 C# 一种语言（基于 Roslyn），其余 6 种语言回退到正则表达式——只能肤浅地匹配符号名，没有嵌套结构、没有调用关系。`IAstAnalyzer` 接口设计为每种语言独立实现，方案根本不可行：手写 100+ 个解析器不现实。

Tree-sitter 是 GitHub/Neovim 等工具使用的工业级多语言解析引擎，已有 .NET 绑定 `TreeSitter.DotNet`（28+ 语言内置），一次集成即可覆盖所有主流语言的真 AST 解析。

## What Changes

- **移除**：`IAstAnalyzer` 接口和 `RoslynCSharpAnalyzer` 实现（~160 行）
- **移除**：`Microsoft.CodeAnalysis.Common` 和 `Microsoft.CodeAnalysis.CSharp` NuGet 引用
- **新增**：`TreeSitter.DotNet` NuGet 包，提供 28+ 语言的真 AST 解析
- **重写**：`CodeIndexService` 的符号提取、依赖提取、分块逻辑——从"AstAnalyzer + 正则回退"改为"TreeSitter 统一引擎"
- **新增**：通用 S-expression Query 模式库，覆盖函数、类、方法、调用、导入等常用 AST 节点类型
- **保留**：`CodeIndexEntry` / `CodeIndexChunk` 实体、`ICodeIndexRepository` 接口不变，仅修改内部实现

## Capabilities

### New Capabilities
- `tree-sitter-ast-engine`：基于 TreeSitter.DotNet 的统一多语言 AST 解析引擎，支持 28+ 语言的真语法树解析，取代当前 C#-only Roslyn + 正则回退方案

### Modified Capabilities
- `code-indexing`：符号提取、依赖提取、代码分块逻辑从 AstAnalyzer/Regex 切换到 TreeSitter Query；语言支持从 7 种（识别）/ 1 种（AST）扩展到 28+ 种（全部 AST）

## Impact

- **依赖**：移除 `Microsoft.CodeAnalysis.Common`（4.14.0）、`Microsoft.CodeAnalysis.CSharp`；新增 `TreeSitter.DotNet`
- **基础设施**：删除 `Infrastructure/AstAnalysis/IAstAnalyzer.cs`、`RoslynCSharpAnalyzer.cs`；新增 `Infrastructure/AstAnalysis/TreeSitterAnalyzer.cs`
- **业务层**：`CodeIndexService.ExtractSymbols` / `ExtractDependencies` / `ChunkFile` 实现切换到 TreeSitter Query
- **DI 注册**：`Program.cs` 移除 `IAstAnalyzer` 注册，改为注册 `TreeSitterAnalyzer`（Singleton）

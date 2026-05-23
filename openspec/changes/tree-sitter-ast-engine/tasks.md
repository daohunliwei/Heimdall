## 1. NuGet 包变更

- [x] 1.1 在 `Heimdall.Infrastructure.csproj` 中添加 `TreeSitter.DotNet` 包引用
- [x] 1.2 在 `Heimdall.Api.csproj` 和 `Heimdall.Infrastructure.csproj` 中移除 `Microsoft.CodeAnalysis.Common` 和 `Microsoft.CodeAnalysis.CSharp` 包引用
- [x] 1.3 执行 `dotnet restore` 验证包引用正确

## 2. 删除旧代码

- [x] 2.1 删除 `Infrastructure/AstAnalysis/IAstAnalyzer.cs`（接口和 DTO 记录类）
- [x] 2.2 删除 `Infrastructure/AstAnalysis/RoslynCSharpAnalyzer.cs`
- [x] 2.3 移除 `Program.cs` 中的 `IAstAnalyzer` DI 注册行
- [x] 2.4 移除项目中所有 `using Microsoft.CodeAnalysis;` 引用

## 3. TreeSitter 统一引擎

- [x] 3.1 新建 `Infrastructure/AstAnalysis/TreeSitterAnalyzer.cs`：封装 `Language`/`Parser`/`Query`，接收文件路径和语言名，返回 `AstFileResult`
- [x] 3.2 实现 `LanguageQueries` 配置类：为每种支持语言定义 SymbolQuery、DependencyQuery、ChunkQuery 三组 S-expression 字符串
- [x] 3.3 实现语言名映射表：`DetectLanguage` 返回值 → tree-sitter 语法名（"csharp"→"CSharp", "typescript"→"TypeScript" 等 28 种）
- [x] 3.4 实现 `ExtractSymbols`：运行 SymbolQuery，提取 capture 节点文本，去重取 Top 100
- [x] 3.5 实现 `ExtractDependencies`：运行 DependencyQuery，提取 import/using 来源，去重取 Top 30
- [x] 3.6 实现 `ChunkFile`：运行 ChunkQuery 或取根节点的一级命名子节点，按 `StartPosition`/`EndPosition` 计算行号边界
- [x] 3.7 实现正则回退路径：tree-sitter 不支持的 Language 回退到现有正则逻辑（保留 `RegexPatterns` 类）

## 4. CodeIndexService 适配

- [x] 4.1 将 `CodeIndexService` 构造函数从注入 `IEnumerable<IAstAnalyzer>` 改为注入 `TreeSitterAnalyzer`
- [x] 4.2 更新 `ExtractSymbols` 方法：删除 `_astAnalyzers` 查找逻辑，统一调用 `TreeSitterAnalyzer.ExtractSymbols`
- [x] 4.3 更新 `ExtractDependencies` 方法：切换各语言的 switch-case 正则为 TreeSitterAnalyzer 统一提取
- [x] 4.4 更新 `ChunkFile` 方法：删除 `FindChunkBoundaries` 的语言分支，统一用 TreeSitter 节点边界

## 5. DI 与配置

- [x] 5.1 在 `Program.cs` 中注册 `TreeSitterAnalyzer` 为 Singleton
- [x] 5.2 移除 `Program.cs` 中的 `IAstAnalyzer` 注册（`RoslynCSharpAnalyzer`）
- [x] 5.3 更新 `appsettings.json` 添加 tree-sitter 相关配置项（可选，如文件大小限制）

## 6. 验证

- [x] 6.1 执行 `dotnet build` 确保编译通过
- [ ] 6.2 编写/更新单元测试：验证 C#/TypeScript/Python/Go/Rust/Java 六种语言的符号提取结果正确
- [x] 6.3 端到端测试：对 libgit2sharp 仓库执行代码索引，验证 `code_index_entries` 表数据完整
- [x] 6.4 对比验证：TreeSitter C# 提取结果与旧 Roslyn 提取结果基本一致（允许方法签名差异）

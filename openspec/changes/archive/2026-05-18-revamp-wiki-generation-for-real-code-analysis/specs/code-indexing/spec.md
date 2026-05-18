## ADDED Requirements

### Requirement: 本地代码结构索引
系统 SHALL 在不调用 LLM 的情况下，对仓库中所有源代码文件进行结构化索引。索引结果 SHALL 包含：文件路径、编程语言、模块归属、导出的类/函数/接口符号、导入依赖关系、文件重要性评分。

#### Scenario: 索引 C# 仓库
- **WHEN** 仓库包含 .cs 文件且入口点为 Program.cs
- **THEN** 索引结果包含每个 .cs 文件的类名、方法签名、命名空间，且 Program.cs 被标记为入口点

#### Scenario: 索引混合语言仓库
- **WHEN** 仓库包含 C#、TypeScript、Python 等多种语言文件
- **THEN** 系统为每种语言使用对应的符号提取规则，索引结果按语言分类

#### Scenario: 跳过非代码文件
- **WHEN** 仓库包含 node_modules、.git、bin、obj 等目录
- **THEN** 索引过程跳过这些目录，不生成索引条目

#### Scenario: 大文件处理
- **WHEN** 单个文件超过 10MB
- **THEN** 系统仅索引文件路径和元数据，不对内容做符号提取，标记为"超大文件"

### Requirement: 向量代码嵌入
系统 SHALL 对索引后的代码文件进行分块向量嵌入，支持后续语义检索。嵌入的分块策略 SHALL 保留文件路径和行号元数据。

#### Scenario: 代码分块嵌入
- **WHEN** 代码文件被索引后
- **THEN** 系统按函数/类边界分块（每块不超过 80 行），生成向量嵌入并存储

#### Scenario: 嵌入复用
- **WHEN** 同一版本的文件内容未变化
- **THEN** 嵌入结果从缓存加载，不重复调用嵌入提供程序

### Requirement: BM25 文本索引
系统 SHALL 为所有源代码文件构建 BM25 倒排索引，支持基于关键词和符号名的精确匹配检索。索引 SHALL 支持按文件路径和模块名过滤。

#### Scenario: 精确符号搜索
- **WHEN** 搜索 "UserService.GetById"
- **THEN** BM25 检索返回包含该精确方法名的代码片段，且排名高于仅包含 "UserService" 的片段

#### Scenario: 中文关键词搜索
- **WHEN** 搜索中文词"认证"
- **THEN** BM25 检索返回注释中包含"认证"以及符号名匹配的代码文件

### Requirement: 索引持久化与版本绑定
系统 SHALL 将代码索引结果持久化到数据库，与 RepositoryVersion 绑定。同一仓库版本重复刷新时复用已有索引。

#### Scenario: 索引持久化
- **WHEN** 代码索引完成
- **THEN** 索引数据（符号表、文件元数据、BM25 倒排索引）存储到 code_index 相关表中

#### Scenario: 版本变更重新索引
- **WHEN** 同一仓库的新版本（不同 commit）触发刷新
- **THEN** 系统检测文件变更，仅对变更文件重新索引

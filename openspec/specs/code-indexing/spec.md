## ADDED Requirements

### Requirement: 本地代码结构索引
系统 SHALL 在不调用 LLM 的情况下，对仓库中所有源代码文件进行结构化索引。V7 中索引结果 SHALL 在原有基础上新增：方法级调用关系（CallerSymbol↔CalleeSymbol）、模块间依赖方向、设计模式特征标注。

#### Scenario: 索引 C# 仓库含调用关系
- **WHEN** 仓库包含 .cs 文件
- **THEN** 索引结果除原有的类名、方法签名外，新增每个方法对外调用的目标方法列表（基于正则匹配 `\.MethodName(` 模式）

#### Scenario: 索引含依赖拓扑
- **WHEN** 仓库包含多个项目/模块
- **THEN** 索引结果包含模块依赖图：每个模块列出其直接依赖的模块名称和依赖类型

#### Scenario: 跳过非代码文件（不变）
- **WHEN** 仓库包含 node_modules、.git、bin、obj 等目录
- **THEN** 索引过程跳过这些目录，不生成索引条目

#### Scenario: 设计模式特征标注
- **WHEN** 索引发现类名匹配 `*Factory`、`*Strategy`、`*Observer`、`*Builder` 模式
- **THEN** 索引条目中增加 `designPatternHint` 字段标注疑似设计模式

### Requirement: 向量代码嵌入
系统 SHALL 对索引后的代码文件进行分块向量嵌入。V7 中分块策略 SHALL 在原有按行数分块基础上，优先按函数/类边界分块，仅在无法识别边界时回退到按行数分块。

#### Scenario: 函数边界分块
- **WHEN** 代码文件包含明确的函数/方法定义
- **THEN** 系统按函数/方法边界分块，每块对应一个完整的函数实现（不超过 120 行）

#### Scenario: 超长函数分块
- **WHEN** 单个函数超过 120 行
- **THEN** 系统在函数内部按逻辑块（if/else、循环、try/catch 边界）分割，确保每块 ≤ 120 行

#### Scenario: 无边界文件回退
- **WHEN** 文件为配置文件或无明确函数边界的脚本
- **THEN** 系统回退到按 80 行分块（原有策略），保持向后兼容

### Requirement: BM25 文本索引
系统 SHALL 为所有源代码文件构建 BM25 倒排索引。V7 中 tokenization SHALL 增加对中文注释的分词支持和 camelCase/snake_case 拆分的符号变体索引。

#### Scenario: 中文注释检索
- **WHEN** 搜索中文关键词"认证"
- **THEN** BM25 能命中代码注释中包含"认证"的文件，中文分词使用字符级 bigram 索引

#### Scenario: 符号变体匹配
- **WHEN** 搜索 "user service"
- **THEN** BM25 同时匹配 `UserService`（camelCase）、`user_service`（snake_case）、`userService`（lowerCamelCase）

### Requirement: 索引持久化与版本绑定
系统 SHALL 将代码索引结果持久化到数据库，与 RepositoryVersion 绑定。同一仓库版本重复刷新时复用已有索引。

#### Scenario: 索引持久化
- **WHEN** 代码索引完成
- **THEN** 索引数据（符号表、文件元数据、BM25 倒排索引）存储到 code_index 相关表中

#### Scenario: 版本变更重新索引
- **WHEN** 同一仓库的新版本（不同 commit）触发刷新
- **THEN** 系统检测文件变更，仅对变更文件重新索引

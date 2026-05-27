## Why

当前 Heimdall 已经具备完整的 Tree-sitter AST 解析能力，但解析结果仍主要停留在内存对象和任务工件层，尚未形成可追溯、可复用、可多版本共存的持久化资产。这使后续在页面上展示完整语法树、调用图等动态内容缺乏稳定数据底座，也无法准确回答某个 Wiki 版本到底依赖了哪一份 AST 结果。

## What Changes

- 新增 AST 版本化持久化能力，为每个 `RepositoryVersion` 保存可追溯的 AST 主版本与明细数据
- 持久化 AST 文件级结构化结果，覆盖语法树投影、符号、调用边、依赖边、声明级分块与模式提示
- 支持 AST 结果按分支、`commit_sha` 与解析配置长期共存，并对相同快照的重复解析提供复用规则
- 调整 Wiki 生成管线，要求 `WikiVersion` 绑定生成时实际依赖的 AST 版本，而不是仅间接依赖 `RepositoryVersion`
- 在任务摘要、版本化知识读取与后续动态渲染入口中暴露 AST 版本元信息

## Capabilities

### New Capabilities
- `ast-version-persistence`: AST 解析结果的版本化持久化、明细落库、多版本共存、复用与追溯规则

### Modified Capabilities
- `wiki-generation-pipeline`: Wiki 生成前必须解析或复用目标仓库快照的 AST 版本，并在 `WikiVersion` 上记录实际依赖的 AST 版本
- `code-analysis`: AST 分析输出不再只作为瞬时内存结果使用，还必须能够被投影为可持久化、可恢复的结构化数据

## Impact

- **数据模型**: 新增 `AstVersion` 单表（单行全量 JSON + 轻量索引字段）；扩展 `WikiVersion` 以追踪依赖的 AST 版本
- **后端服务**: 影响 `CodeIndexService`、`CodeUnderstandingService`、`WikiTaskService`、`VersionedKnowledgeService`
- **仓储层**: 新增 AST 版本读写仓储，单次事务写入
- **任务链路**: AST 持久化需要进入 Wiki 主链路的可观测阶段和结果摘要
- **后续能力**: 为完整语法树、调用图和其他动态渲染页面提供稳定数据底座

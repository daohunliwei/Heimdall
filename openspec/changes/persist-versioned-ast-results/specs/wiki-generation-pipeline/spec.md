## ADDED Requirements

### Requirement: Wiki 生成前必须解析或复用 AST 版本
Wiki 生成管线 SHALL 在持久化 `WikiVersion` 之前，先为目标 `RepositoryVersion` 解析或复用一个可引用的 AST 版本。若不存在可引用的 AST 版本，则 Wiki 主链路 MUST NOT 进入成功落库阶段。

#### Scenario: 命中可复用 AST 版本
- **WHEN** 目标 `RepositoryVersion` 已存在满足当前解析配置的成功 AST 版本
- **THEN** Wiki 生成管线复用该 AST 版本
- **AND** 不重复创建语义等价的 AST 结果

#### Scenario: 需要先生成 AST 版本
- **WHEN** 目标 `RepositoryVersion` 不存在可复用的 AST 版本
- **THEN** Wiki 生成管线先完成 AST 解析和持久化
- **AND** 只有 AST 版本可引用后才继续 `WikiVersion` 落库

#### Scenario: AST 持久化失败阻断 Wiki 成功态
- **WHEN** 本次 Wiki 生成所需的 AST 持久化失败
- **THEN** `WikiVersion` 不得进入成功态
- **AND** 系统不得写入指向不存在或失败 AST 版本的 Wiki 关联

### Requirement: WikiVersion 必须记录实际依赖的 AST 版本
`WikiVersion` SHALL 显式记录生成时实际依赖的 `AstVersionId`，而不是仅通过 `RepositoryVersion` 间接推导。

#### Scenario: Wiki 主数据落库
- **WHEN** 系统创建新的 `WikiVersion`
- **THEN** 该记录写入 `repository_version_id`
- **AND** 同时写入本次生成实际使用的 `ast_version_id`

#### Scenario: 查询 Wiki 依赖链路
- **WHEN** 调用版本化知识读取、任务结果摘要或后续动态渲染入口
- **THEN** 返回结果包含 `repository_version_id`、`wiki_version_id` 与 `ast_version_id`
- **AND** 可据此追溯该 Wiki 版本依赖的语法树版本

## MODIFIED Requirements

### Requirement: 新 Wiki 生成管线流程
系统 SHALL 按 10 阶段执行 Wiki 生成。**变更**：Stage 2（仓库准备）增加文档文件收集子步骤；Stage 3（深度代码理解）和 Stage 4（结构规划）注入收集到的仓库文档内容；Stage 4 输出结构和 Stage 5 页面生成使用分离后的 MaxContextTokens/MaxOutputTokens 动态预算。

#### Scenario: Stage 2 文档收集
- **WHEN** 管线执行至 Stage 2 仓库准备
- **THEN** 系统在克隆/拉取完成后扫描仓库根目录及 docs/、.github/ 子目录
- **AND** 收集 AGENTS.md、README.md、CLAUDE.md 等文档文件内容
- **AND** 将收集结果存入管线上下文 `RepositoryDocs` 字典

#### Scenario: 文档内容注入结构规划
- **WHEN** 管线执行至 Stage 4 结构规划
- **THEN** BuildWikiStructurePromptV7 方法接收 RepositoryDocs 字典，将文档内容注入提示词上下文段
- **AND** LLM 据此输出更符合仓库实际架构的层级结构

#### Scenario: 大窗口模型使用分离式预算
- **WHEN** 当前模型的 MaxContextTokens=1048576, MaxOutputTokens=384000
- **THEN** 结构规划输入预算计算为 1048576 * ContextFillRatio，页面生成时 max_tokens 设置为 384000
- **AND** 系统尽可能在输入预算内填充代码片段和文档内容

### Requirement: 结构规划输入变更
结构规划阶段 SHALL 使用目录树、模块列表、入口点文件列表、关键技术栈信息、CodeUnderstandingResult、**RepositoryDocs（仓库文档内容）** 作为输入。

**变更**：新增 RepositoryDocs 作为结构规划的核心输入源之一。文档内容按优先级排序后注入提示词上下文段，帮助 LLM 理解项目的架构约定和组织方式。

#### Scenario: 结构规划使用仓库文档
- **WHEN** 系统执行结构规划且 RepositoryDocs 包含 AGENTS.md 和 README.md
- **THEN** LLM 收到的输入除目录树和代码理解结果外，还包含："## 仓库文档参考\n以下为仓库文档内容，请据此理解架构：\n### AGENTS.md\n...\n### README.md\n..."
- **AND** 文档中描述的架构分层和模块职责直接影响 Wiki 层级结构设计

## ADDED Requirements

### Requirement: 文档收集子步骤管线集成
系统 SHALL 在 WikiTaskService 的管线执行流程中，于 Stage 2 新增 `CollectRepositoryDocuments` 方法。该方法 SHALL 在前端代码拉取完成后同步执行，不依赖 LLM 调用。

#### Scenario: 文档收集成功
- **WHEN** `CollectRepositoryDocuments` 执行且仓库包含 AGENTS.md（2000 字符）、README.md（3000 字符）
- **THEN** 返回 `RepositoryDocs` 字典包含两个条目，每个条目含 FileName、Content（完整或截断后）、Priority 字段
- **AND** 管线日志记录 "已收集 2 个仓库文档文件"

#### Scenario: 文档收集异常隔离
- **WHEN** 文档收集过程中文件读取抛出 IOException
- **THEN** 异常被捕获并记录 Error 日志，管线继续执行
- **AND** RepositoryDocs 字典仅包含成功读取的文档

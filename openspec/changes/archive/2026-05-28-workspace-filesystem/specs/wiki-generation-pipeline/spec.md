## ADDED Requirements

### Requirement: Wiki 页面内容存储到 Workspace 文件
Wiki 页面 SHALL 将 `ContentMarkdown` 写入 Workspace 的 `wiki/{wiki_version_id[:8]}/pages/{page_order:D4}_{slug}.md` 文件，DB 中 `WikiPage.content_file_path` 记录路径，不再将完整 Markdown 内容存入 DB。加粗、反斜杠等格式的特殊字符保留在 Markdown 中。

#### Scenario: Wiki 页面生成后写文件
- **WHEN** 系统完成一个 Wiki 页面的 LLM 生成
- **THEN** 页面 Markdown 内容写入 workspace 文件
- **AND** `WikiPage.content_file_path` 记录文件路径
- **AND** `WikiPage.ContentMarkdown` DB 列保留为空或存摘要

#### Scenario: 读取 Wiki 页面内容
- **WHEN** API 请求读取某个 Wiki 页面
- **THEN** 系统根据 `content_file_path` 从 workspace 读取文件
- **AND** 文件缺失时触发页面重新生成

### Requirement: Wiki 版本结构存储到 Workspace 文件
`WikiVersion.StructureJson` SHALL 写入 Workspace 的 `wiki/{wiki_version_id[:8]}/structure.json` 文件，DB 中 `structure_file_path` 记录路径。

#### Scenario: Wiki 结构规划完成后写文件
- **WHEN** 系统完成结构规划阶段
- **THEN** 结构 JSON 写入 workspace 文件
- **AND** `WikiVersion.structure_file_path` 记录路径

### Requirement: 仓库克隆路径迁移到 Workspace
仓库克隆目标路径 SHALL 从 `%TEMP%/heimdall_repos/` 改为 `{workspace}/repos/{owner}_{repo}/`，与 Workspace 统一管理。

#### Scenario: 克隆新仓库
- **WHEN** 系统需要克隆 `SilverHawk/wikispider`
- **THEN** 目标路径为 `{workspace}/repos/SilverHawk_wikispider/`
- **AND** 克隆逻辑不变（`git clone --depth=1`）

#### Scenario: 重用已克隆仓库
- **WHEN** 目标路径已存在非空目录
- **THEN** 系统跳过克隆直接复用
- **AND** 行为与当前一致

## MODIFIED Requirements

### Requirement: Wiki 生成前必须解析或复用 AST 版本
Wiki 生成管线 SHALL 在持久化 `WikiVersion` 之前，先为目标 `RepositoryVersion` 解析或复用一个可引用的 AST 版本，且 AST 版本数据 SHALL 存储在 Workspace `ast/` 目录下。若不存在可引用的 AST 版本，则 Wiki 主链路 MUST NOT 进入成功落库阶段。

#### Scenario: 命中可复用 AST 版本
- **WHEN** 目标 `RepositoryVersion` 已存在满足当前解析配置的成功 AST 版本
- **THEN** Wiki 生成管线复用该 AST 版本
- **AND** AST 数据存储在 `{workspace}/ast/{ast_version_id[:8]}/` 目录下
- **AND** 不重复创建语义等价的 AST 结果

#### Scenario: 需要先生成 AST 版本
- **WHEN** 目标 `RepositoryVersion` 不存在可复用的 AST 版本
- **THEN** Wiki 生成管线先完成 AST 解析并将数据写入 workspace
- **AND** 只有 AST 版本可引用后才继续 `WikiVersion` 落库

#### Scenario: AST 持久化失败阻断 Wiki 成功态
- **WHEN** 本次 Wiki 生成所需的 AST 持久化失败
- **THEN** `WikiVersion` 不得进入成功态
- **AND** 系统不得写入指向不存在或失败 AST 版本的 Wiki 关联

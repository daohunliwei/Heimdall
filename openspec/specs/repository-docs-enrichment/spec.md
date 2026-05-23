## ADDED Requirements

### Requirement: 仓库文档文件自动收集
系统 SHALL 在 Wiki 生成管线的 Stage 2（仓库准备）阶段自动扫描并收集仓库根目录及相关子目录下的 Markdown 文档文件。收集目标文件 SHALL 至少包括：`AGENTS.md`、`README.md`、`CLAUDE.md`、`CONTRIBUTING.md`、`CODE_OF_CONDUCT.md`、`CHANGELOG.md`、`SECURITY.md`、`GOVERNANCE.md`，以及 `docs/` 和 `.github/` 目录下的 `.md` 文件。

#### Scenario: 收集已存在的仓库文档
- **WHEN** 仓库根目录存在 `AGENTS.md`、`README.md` 和 `docs/architecture.md`
- **THEN** 系统收集这三个文件的内容，按文件名-内容键值对存储到管线上下文 `RepositoryDocs` 字段
- **AND** 记录每个文件的收集状态（已收集/未找到）

#### Scenario: 仓库文档不存在时继续执行
- **WHEN** 仓库根目录不存在任何目标文档文件
- **THEN** 管线正常继续执行，`RepositoryDocs` 为空集合
- **AND** 日志记录 "仓库中未找到可用的文档文件"

#### Scenario: 大文档截断处理
- **WHEN** 某文档文件内容超过 5000 字符
- **THEN** 系统保留文档前 3000 字符和文件名信息，尾部追加 "…（文档过长，已截断）" 标记
- **AND** 完整内容在调试日志中记录

### Requirement: 文档内容注入结构规划阶段
系统 SHALL 在 Stage 4（结构规划）的提示词上下文中注入已收集的仓库文档内容。注入内容 SHALL 包含：文档文件名、文档内容摘要（<3000 字符截断版本）、文档中识别的架构关键词。

#### Scenario: 结构规划提示词包含文档上下文
- **WHEN** 仓库收集到 `AGENTS.md`（包含架构分层描述）和 `README.md`（包含项目介绍）
- **THEN** 结构规划提示词的上下文段包含 "## 仓库文档参考\n以下为仓库根目录文档内容，请据此理解项目架构和模块组织：\n### AGENTS.md\n<内容>…\n### README.md\n<内容>…"
- **AND** LLM 据此输出更符合实际架构的层级结构

#### Scenario: 文档中识别的架构模式影响结构规划
- **WHEN** `AGENTS.md` 描述了"分层架构：Api → Core → Repository"，`README.md` 说明了模块职责
- **THEN** 结构规划输出的 Wiki 目录应当反映该分层结构（如 API 层、业务层、数据层作为顶层章节）

### Requirement: 文档内容注入页面生成阶段
系统 SHALL 在 Stage 5（页面生成）阶段根据页面主题相关性，有选择地注入仓库文档内容作为补充上下文。

#### Scenario: 页面关联到架构主题时注入相关文档
- **WHEN** 生成的页面标题包含"架构"、"模块"、"设计"等关键词
- **THEN** 页面生成提示词的上下文段追加 AGENTS.md 和 README.md 的内容摘要，帮助 LLM 理解整体架构背景

#### Scenario: 页面为代码详解类型时不注入无关文档
- **WHEN** 生成的页面为 article 级代码实现详解，标题为具体文件或类名
- **THEN** 文档内容不注入（避免噪声），仅使用代码检索片段作为上下文

### Requirement: 文档优先级排序
系统 SHALL 按以下优先级排序注入的文档内容：AGENTS.md > CLAUDE.md > README.md > CONTRIBUTING.md > 其他文档。当所有文档总长度超过输入预算的 15% 时，低优先级文档被裁剪。

#### Scenario: 文档总长度未超预算
- **WHEN** 收集到 AGENTS.md（2000 字符）和 README.md（1500 字符），总长度 3500 字符，输入预算 100000 tokens
- **THEN** 所有文档内容完整注入，不裁剪

#### Scenario: 文档总长度超预算时裁剪低优先级
- **WHEN** 收集到 6 个文档，总长度 30000 字符，输入预算 50000 tokens（约 12500 字符的 15% 为 7500 字符）
- **THEN** 按优先级注入 AGENTS.md、CLAUDE.md、README.md 完整内容，CONTRIBUTING.md 截断，更低优先级文档仅注入文件名标记

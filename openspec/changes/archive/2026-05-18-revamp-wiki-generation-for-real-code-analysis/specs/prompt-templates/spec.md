## ADDED Requirements

### Requirement: 页面生成提示词使用真实代码
Wiki 页面生成的提示词 SHALL 包含从混合检索获取的真实代码片段，而非代码摘要。提示词 SHALL 要求 LLM 基于提供的源代码撰写技术文档。

#### Scenario: 提示词注入代码片段
- **WHEN** 系统构建页面生成提示词
- **THEN** 提示词中包含格式化后的真实源代码块，附带文件路径和行号

#### Scenario: 提示词禁止虚构
- **WHEN** 页面生成提示词构建完成
- **THEN** 提示词中包含指令"严格基于上述源代码撰写文档。不得编造不存在的类、方法或 API。如某方面在代码中未体现，请注明'代码中未包含此部分'"

### Requirement: 删除旧摘要模板
系统 SHALL 直接从 `PromptSeedData.cs` 中删除 `code-summary-file`、`code-summary-module`、`code-summary-system` 三个提示词模板及其播种逻辑。

#### Scenario: 旧模板已删除
- **WHEN** 系统启动并播种提示词模板
- **THEN** code-summary-* 模板不在数据库中出现

### Requirement: 替换结构规划和页面生成模板变量
`wiki-structure-planning` 模板 SHALL 使用 `{{repo_structure}}`（目录树+入口点+技术栈）替代 `{{code_summary}}`。`wiki-page-generation` 模板 SHALL 使用 `{{retrieved_code_snippets}}`（真实代码片段）替代 `{{file_summaries}}`。

#### Scenario: 结构规划模板更新
- **WHEN** 使用新的结构规划模板
- **THEN** 模板变量使用 `{{repo_structure}}`，内容为仓库目录树和入口文件列表

#### Scenario: 页面生成模板更新
- **WHEN** 使用新的页面生成模板
- **THEN** 模板变量使用 `{{retrieved_code_snippets}}`，内容为混合检索返回的真实代码片段

### Requirement: 模型感知的提示词变体
系统 SHALL 根据使用的模型能力自动调整提示词。对于能力较弱的模型（如 7-14B 参数），提示词 SHALL 包含更严格的约束和更少的要求项。

#### Scenario: 小模型提示词调整
- **WHEN** 用户配置 7B 参数模型作为页面生成模型
- **THEN** 提示词中增加"每次只分析一个函数"、"不要输出超过 500 字"等约束，并减少同时要求的任务数量

#### Scenario: 强模型提示词
- **WHEN** 用户配置 Claude Sonnet 或 GPT-4o 级别模型
- **THEN** 提示词包含完整的代码分析要求（函数调用链、设计模式识别、性能考量）

## REMOVED Requirements

### Requirement: 旧模板标记为废弃
**Reason**: 不需要保留兼容性。code-summary-* 模板及其在数据库中的记录直接删除，不保留任何标记或回退逻辑。
**Migration**: 删除 PromptSeedData.cs 中的对应播种代码，删除数据库中已播种的模板记录。

## ADDED Requirements

### Requirement: 新 Wiki 生成管线流程
系统 SHALL 按以下阶段执行 Wiki 生成：仓库准备 → 代码索引（本地，无 LLM）→ 结构规划（LLM）→ 检索增强页面生成（LLM + 混合检索）→ 质量审查 → 渲染后处理 → 持久化 → 向量嵌入。

#### Scenario: 标准仓库 Wiki 生成
- **WHEN** 用户触发 Wiki 刷新
- **THEN** 系统按 8 阶段顺序执行，Stage 2 不再调用 LLM 摘要，改为本地代码索引

#### Scenario: 管线中断恢复
- **WHEN** 管线在页面生成阶段中断
- **THEN** 系统恢复时从上一个成功的阶段继续，复用已生成的页面，不重新执行代码索引

### Requirement: 结构规划输入变更
结构规划阶段 SHALL 使用目录树、模块列表、入口点文件列表和关键技术栈信息作为输入，不再使用代码摘要作为输入。

#### Scenario: 结构规划使用目录树
- **WHEN** 系统执行结构规划
- **THEN** LLM 收到仓库目录树（深度限制 3 层）、入口文件内容和项目构建文件内容，据以设计 Wiki 页面结构

#### Scenario: 结构规划输出页面-文件映射
- **WHEN** 结构规划完成
- **THEN** 每个规划的页面包含明确的关键文件路径列表和搜索关键词，供后续检索阶段使用

### Requirement: 检索增强页面生成
页面生成阶段 SHALL 使用混合检索（BM25 + 向量搜索）从代码索引中获取真实代码片段，注入提示词后由 LLM 生成页面。输出 SHALL 包含真实代码引用（类名、方法签名、关键实现片段），不得包含虚构的示例代码。

#### Scenario: 页面生成含真实代码
- **WHEN** 生成用户认证 Wiki 页面
- **THEN** 页面内容包含从源代码中检索到的真实类名和方法签名，以及核心实现片段

#### Scenario: 页面生成不得虚构 API
- **WHEN** LLM 生成页面内容
- **THEN** 提示词中明确要求"仅使用提供的源代码片段，如代码片段不足以解释某个概念，请注明'未在代码中找到对应实现'"

#### Scenario: 批量页面生成
- **WHEN** 结构规划确定 10 个页面
- **THEN** 系统以每批 5 页的方式并行生成，每页生成前独立执行代码检索

### Requirement: 无 LLM 代码索引替代旧摘要
系统 SHALL 废弃 Stage 2 的 LLM 代码摘要环节（文件摘要、模块摘要、系统摘要），改用本地代码结构索引。

#### Scenario: 不再调用 LLM 生成文件摘要
- **WHEN** 执行代码分析阶段
- **THEN** 系统不调用任何 LLM Provider，仅执行本地文件遍历和符号提取

### Requirement: 旧管线数据清空
系统 SHALL 在部署新管线时清空旧的 Wiki 生成数据和摘要表，不保留旧管线产生的数据库记录。旧管线代码（CodeSummaryService 的 LLM 摘要方法、code-summary-* 提示词模板）SHALL 直接删除。

#### Scenario: 旧摘要表删除
- **WHEN** 执行新数据库迁移
- **THEN** 旧的 code_summaries 相关表被 DROP，新的 code_index_entries 和 code_index_chunks 表被 CREATE

#### Scenario: 旧代码删除
- **WHEN** 编译新版本代码
- **THEN** CodeSummaryService.cs 中的 LLM 摘要方法不存在，PromptSeedData.cs 中无 code-summary-* 模板

## REMOVED Requirements

### Requirement: 向后兼容
**Reason**: 旧管线输出质量低（示例代码、虚构 API），没有保留价值。当前处于开发验证期，无需兼容历史数据。
**Migration**: 清空数据库中的旧 Wiki 版本和摘要数据，使用新管线重新生成。

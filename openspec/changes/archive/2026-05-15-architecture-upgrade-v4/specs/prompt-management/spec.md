## ADDED Requirements

### Requirement: 提示词模板 CRUD 管理
系统 SHALL 提供 `PromptTemplate` 实体与完整的 REST API（GET/POST/PUT/DELETE），支持按 `slug` 唯一标识、按 `category` 分类查询。系统内置模板（`is_system=true`）不可删除。

#### Scenario: 创建自定义提示词模板
- **WHEN** 管理员通过 `POST /api/admin/prompt-templates` 提交新模板
- **THEN** 系统 SHALL 创建模板记录，自动设置 version=1，返回完整模板对象

#### Scenario: 尝试删除系统模板
- **WHEN** 管理员尝试删除 `is_system=true` 的模板
- **THEN** 系统 SHALL 返回 403 并提示"系统模板不可删除，可通过覆写进行定制"

### Requirement: 仓库级提示词覆写
系统 SHALL 支持为特定仓库创建提示词覆写（`PromptOverride`），覆写策略包含：`override`（完全替换）、`merge`（模板变量级合并）、`append`（追加内容到模板末尾）。

#### Scenario: 仓库覆写 Wiki 结构规划提示词
- **WHEN** 为仓库 A 创建了 `wiki_structure` 类别的 override（strategy=append, content="额外关注安全相关模块"）
- **THEN** 该仓库生成 Wiki 时，结构规划提示词 SHALL 在全局模板末尾追加覆写内容

#### Scenario: 无覆写时使用全局默认
- **WHEN** 仓库 B 未配置任何覆写
- **THEN** 系统 SHALL 使用全局默认模板，无额外内容注入

### Requirement: 提示词版本化追踪
每次修改模板或覆写时，系统 SHALL 自增 `version` 字段。系统 SHALL 保留历史版本记录，支持按版本号查询历史内容。

#### Scenario: 回滚到历史版本
- **WHEN** 管理员请求回滚模板到指定历史版本
- **THEN** 系统 SHALL 创建新版本，内容等于目标历史版本的内容

### Requirement: 运行时提示词解析
`PromptManagementService.ResolveTemplate(slug, repositoryId?)` SHALL 按以下优先级组合最终提示词：
1. 查找 slug 对应的全局模板
2. 如果提供了 repositoryId，查找该仓库的活跃覆写
3. 按策略合并：override 直接替换、merge 合并变量、append 追加
4. 执行变量插值（`{{variable}}` 语法）
5. 返回最终文本

#### Scenario: 多覆写按优先级排序
- **WHEN** 同一模板存在多个活跃覆写（priority 不同）
- **THEN** 系统 SHALL 按 priority 降序依次应用，高优先级覆写先生效

### Requirement: 管理后台提示词界面
管理后台 SHALL 提供提示词管理页面，支持：模板列表浏览、模板内容编辑（代码编辑器）、覆写配置、变量预览。

#### Scenario: 在线编辑提示词并预览
- **WHEN** 管理员修改模板内容并点击"预览"
- **THEN** 系统 SHALL 展示变量插值后的最终提示词文本（使用示例变量值）

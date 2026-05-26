## MODIFIED Requirements

### Requirement: Wiki 生成管线流程
系统 SHALL 按当前实现描述 Wiki 生成的 8 阶段主流程：仓库准备 → 代码索引 → 代码理解 → 结构规划 → 页面生成 → 质量审查 → 渲染后处理 → 持久化。与向量嵌入、独立向量阶段相关的描述不属于当前已落地能力。

#### Scenario: 标准仓库 Wiki 生成
- **WHEN** 用户触发 Wiki 刷新
- **THEN** 系统按 8 阶段主流程执行
- **AND** Stage 2 基于 Tree-sitter 和索引构建代码检索底座
- **AND** Stage 5 基于当前可用的检索证据生成页面
- **AND** 不要求执行独立的向量嵌入阶段才能完成主流程

## ADDED Requirements

### Requirement: 页面生成使用当前已落地的证据检索能力
页面生成阶段 SHALL 按当前实现使用 `BM25` 检索、版本化页面与工件上下文注入提示词，输出基于真实代码与版本证据的内容。未实现的向量召回不得写成当前流程的默认步骤。

#### Scenario: 页面生成注入 BM25 与版本化证据
- **WHEN** 系统生成某个 Wiki 页面
- **THEN** 提示词证据来自当前可用的 `BM25` 检索结果、版本化页面内容和任务工件摘要
- **AND** 如果当前代码未提供向量召回，则文档与注释不得声称已执行 `pgvector` 搜索

### Requirement: Stage 3 与 Stage 5 Tool Call 描述保持现状
系统 SHALL 继续允许 Stage 3 / Stage 5 通过 `ChatOptions.Tools` 增强代码理解与页面生成，但相关说明必须明确其前提是配置开启，而非默认强制执行。

#### Scenario: Tool Call 关闭时主流程仍可运行
- **WHEN** `ToolCallConfigurationService` 返回 Stage 3 或 Stage 5 关闭
- **THEN** 系统仍按主流程继续执行
- **AND** 仅跳过对应阶段的工具增强
- **AND** 文档中应明确说明这是”可选增强”而不是固定阶段

## Why

当前 Wiki 生成系统存在四个关键短板：缺少 DeepSeek 大上下文模型的 Provider 支持；模型配置中上下文窗口与输出长度未在业务逻辑中正确区分使用；Wiki 目录结构虽有 spec 定义但实际实现仍为平铺展开，缺乏层级结构；仓库中的 AGENTS.md、README.md 等文档性文件未被充分利用，导致 Wiki 内容单薄、缺乏架构洞察。

## What Changes

- **新增 DeepSeek Provider**：基于 OpenAI 兼容协议实现 DeepSeek Chat Provider，支持 reasoning_content 流式/非流式响应、thinking 配置、1M 上下文窗口和 384K 输出长度
- **模型上下文与输出长度分离使用**：在 LLM 调用链中正确区分 MaxContextTokens（控制输入截断）和 MaxOutputTokens（控制 max_tokens 参数），大窗口模型尽量填满输入上下文，同时调大输出长度
- **修复 Wiki 目录层级结构**：完善 wiki-deep-structure spec 的实现，确保结构规划输出多层嵌套树，前端左侧树形组件正确渲染 parentId 父子层级关系
- **仓库文档增强 Wiki 生成**：在代码理解和结构规划阶段收集并注入 AGENTS.md、README.md、CLAUDE.md、CONTRIBUTING.md 等仓库文档内容，丰富提示词上下文，提升 Wiki 内容质量和架构洞察深度

## Capabilities

### New Capabilities

- `deepseek-provider`: 集成 DeepSeek 作为新的 LLM Provider，支持 deepseek-v4-pro 和 deepseek-v4-flash 模型，兼容 reasoning_content 和 thinking 配置
- `repository-docs-enrichment`: 在 Wiki 生成管线中收集并注入仓库根目录文档性文件（AGENTS.md、README.md、CLAUDE.md 等），丰富结构规划和页面生成阶段的提示词上下文

### Modified Capabilities

- `model-metadata-config`: MaxContextTokens 和 MaxOutputTokens 字段现已在 DB 实体中定义，但 LLM 调用链中仅使用 MaxContextTokens 做粗略控制。需要修改为：输入侧使用 MaxContextTokens + ContextFillRatio 控制 prompt 截断；输出侧使用 MaxOutputTokens 设置 Provider 的 max_tokens 参数
- `wiki-deep-structure`: 已有完整的 spec 定义（多层嵌套结构、拓扑序生成、内容深度分级），但当前实现中结构规划输出和前端渲染未正确遵守 parentId 层级关系，树形展示为平铺。需要在结构规划服务和前端组件中正确实现父子层级逻辑
- `wiki-generation-pipeline`: 管线需在 Stage 2（仓库准备）阶段新增文档文件收集步骤，在 Stage 3（深度代码理解）和 Stage 4（结构规划）注入文档内容

## Impact

- `backend/Heimdall.Infrastructure/Providers/ChatProviders/` — 新增 DeepSeekChatProvider.cs
- `backend/Heimdall.Infrastructure/Providers/ProviderRegistry.cs` — 注册 DeepSeek Provider
- `backend/Heimdall.Api/config/generator.json` — 新增 deepseek Provider 配置段
- `backend/Heimdall.Core/Services/Tasks/WikiTaskService.cs` — 管线各阶段修改
- `backend/Heimdall.Core/Services/Tasks/TaskPromptService.cs` — 提示词构建修改
- `backend/Heimdall.Core/Services/Tasks/` — 模型上下文/输出分离逻辑
- `backend/Heimdall.Core/Entities/` — 可能需要新增实体或修改
- `frontend/src/components/` — Wiki 树形组件修复
- `backend/Heimdall.Api/Controllers/` — 可能需要新增 API

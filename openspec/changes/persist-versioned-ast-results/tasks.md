## 1. AST 数据模型

- [ ] 1.1 新增 `AstVersion` 实体，绑定 `RepositoryVersion`，包含 `result_json`（全量结果）、`symbol_names_json`（轻量索引）、`file_list_json`（文件清单）及统计与元信息字段
- [ ] 1.2 为 `AstVersion` 补齐索引、唯一键（`repository_version_id` + `config_fingerprint`）与注释
- [ ] 1.3 扩展 `WikiVersion`，增加对实际依赖 `ast_version_id` 的追溯字段

## 2. AST 持久化链路

- [ ] 2.1 在代码分析阶段产出可直接序列化为 `AstVersion.result_json` 的 `AstFileResult[]` 集合
- [ ] 2.2 新增 AST 版本仓储和单次事务写入路径，同时写入全量 JSON、轻量索引 JSON 与统计字段
- [ ] 2.3 为 AST 持久化补齐成功、失败状态，失败版本不暴露为可引用

## 3. 版本解析与复用

- [ ] 3.1 建立按 `RepositoryVersion` + 解析配置指纹查询 AST 版本的读取路径
- [ ] 3.2 实现同一快照命中相同解析配置时的 AST 版本复用逻辑
- [ ] 3.3 验证同仓库不同分支、不同提交、不同解析配置的 AST 版本可长期共存

## 4. Wiki 版本绑定

- [ ] 4.1 调整 Wiki 主链路，确保在持久化 `WikiVersion` 前先解析或复用可引用的 AST 版本
- [ ] 4.2 在 `WikiVersion` 落库、任务结果摘要和版本化知识读取路径中写入并暴露 `ast_version_id`
- [ ] 4.3 阻断"AST 持久化失败但 Wiki 成功落库"的状态撕裂场景

## 5. 验证与回归

- [ ] 5.1 为 AST 版本主记录落库、`result_json` 完整性、版本复用和失败隔离补齐后端测试
- [ ] 5.2 为 Wiki 与 AST 的绑定关系补齐验证，确保可精确追溯 `repository_version_id → ast_version_id → wiki_version_id`
- [ ] 5.3 运行后端构建与相关测试，确认新增 AST 版本化持久化链路不破坏现有 Wiki 生成主流程

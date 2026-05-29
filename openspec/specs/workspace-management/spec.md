## Purpose

Workspace 文件系统管理——涵盖 Workspace 根目录配置、标准目录结构初始化、路径解析服务及文件级缓存失效与重新生成触发。

## Requirements

### Requirement: Workspace 根目录配置
系统 SHALL 通过环境变量 `HEIMDALL_WORKSPACE` 指定 Workspace 根目录。若环境变量未设置，SHALL 默认使用进程工作目录下的 `./workspace`。WorkspaceService SHALL 在启动时确保根目录和所有顶层子目录存在。

#### Scenario: 环境变量已设置
- **WHEN** `HEIMDALL_WORKSPACE` 指向 `/data/heimdall`
- **THEN** `WorkspaceService.RootPath` 返回 `/data/heimdall`
- **AND** 所有子路径基于此根目录解析

#### Scenario: 环境变量未设置
- **WHEN** `HEIMDALL_WORKSPACE` 环境变量不存在
- **THEN** 系统使用 `{AppContext.BaseDirectory}/workspace` 作为默认值
- **AND** 启动时自动创建默认 Workspace 目录

#### Scenario: 根目录不存在时自动创建
- **WHEN** 指定的 Workspace 根目录路径不存在
- **THEN** `WorkspaceService.EnsureDirectories()` 递归创建根目录和所有顶层子目录
- **AND** 创建失败时抛出明确异常

### Requirement: Workspace 标准目录结构
WorkspaceService SHALL 维护标准子目录结构，并为各数据类型提供路径解析方法。SHALL 提供 `GetRepoPath`、`GetAstDir`、`GetWikiDir`、`GetArtifactDir`、`GetLogDir`、`GetCacheDir` 六个路径解析方法。

#### Scenario: 路径解析
- **WHEN** 调用 `GetAstDir(astVersionId)`
- **THEN** 返回 `{workspace}/ast/{astVersionId[:8]}/`
- **AND** 使用 Guid 前 8 位十六进制字符作为目录名

#### Scenario: 路径解析幂等
- **WHEN** 对同一参数多次调用同一路径解析方法
- **THEN** 每次返回相同路径

### Requirement: 文件缺失即缓存失效
系统 SHALL 在读取 Workspace 文件前检查文件是否存在。若 DB 中 `*_file_path` 非空但文件不存在，SHALL 将相关记录标记为 `stale` 状态，触发对应服务的重新生成逻辑，并在生成完成后更新路径和状态。若 `*_file_path` 为空，SHALL 直接触发重新生成。

#### Scenario: 文件存在时直接读取
- **WHEN** DB 路径字段指向的文件在磁盘上存在
- **THEN** 系统直接读取文件内容返回
- **AND** 不触发重新生成

#### Scenario: 文件缺失时重新生成
- **WHEN** DB 路径字段非空但磁盘文件不存在
- **THEN** 系统标记记录状态为 `stale`
- **AND** 触发对应生成服务重新生成
- **AND** 生成完成后更新 DB 路径和状态

#### Scenario: 无路径记录时首次生成
- **WHEN** DB 路径字段为空
- **THEN** 系统直接触发生成服务
- **AND** 生成完成后写入文件并更新 DB 路径字段

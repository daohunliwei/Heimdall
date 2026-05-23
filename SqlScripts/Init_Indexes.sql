-- Heimdall 数据库表索引初始化
-- 列名使用 PascalCase（与现有 EF Core 数据库一致）

-- ===== 仓库 =====
CREATE INDEX IF NOT EXISTS ix_repositories_owner_repo_type ON repositories ("Owner", "RepoName", "RepoType");
CREATE UNIQUE INDEX IF NOT EXISTS uq_repositories_provider_key ON repositories ("provider_type", "provider_repository_key") WHERE "provider_repository_key" IS NOT NULL;

-- ===== 仓库版本 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_repository_versions_repo_branch_commit ON repository_versions ("RepositoryId", "BranchName", "CommitSha");
CREATE INDEX IF NOT EXISTS ix_repository_versions_repo_branch_latest ON repository_versions ("RepositoryId", "BranchName", "IsLatestOnBranch");

-- ===== 任务 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_one_running_task_per_repo_branch ON tasks ("RepositoryId", "SourceBranch") WHERE "Status" = 'running';
CREATE UNIQUE INDEX IF NOT EXISTS uq_one_pending_task_per_repo_branch_type ON tasks ("RepositoryId", "SourceBranch", "TaskType") WHERE "Status" = 'pending';
CREATE INDEX IF NOT EXISTS ix_tasks_status ON tasks ("Status");
CREATE INDEX IF NOT EXISTS ix_tasks_created_at ON tasks ("CreatedAt");

-- ===== 任务工件 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_task_artifacts_task_type_key ON task_artifacts ("TaskId", "ArtifactType", "ArtifactKey");
CREATE INDEX IF NOT EXISTS ix_task_artifacts_task_stage_sequence ON task_artifacts ("TaskId", "StageName", "Sequence");

-- ===== 任务 LLM 调用日志 =====
CREATE INDEX IF NOT EXISTS ix_task_llm_call_logs_task ON task_llm_call_logs ("TaskId", "StepOrder");

-- ===== LLM 调用指标 =====
CREATE INDEX IF NOT EXISTS idx_llm_call_metrics_task ON llm_call_metrics ("TaskId");
CREATE INDEX IF NOT EXISTS idx_llm_call_metrics_created ON llm_call_metrics ("CreatedAt");
CREATE INDEX IF NOT EXISTS idx_llm_call_metrics_provider_model ON llm_call_metrics ("Provider", "Model");

-- ===== 提示词模板 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_prompt_templates_slug ON prompt_templates ("Slug");
CREATE UNIQUE INDEX IF NOT EXISTS uq_prompt_templates_name_scope ON prompt_templates ("Name", "ScopeType", "ScopeValue");

-- ===== 提示词模板历史 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_prompt_template_history_template_version ON prompt_template_history ("PromptTemplateId", "Version");

-- ===== 提示词覆盖 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_repository_prompt_overrides_repo_template ON repository_prompt_overrides ("RepositoryId", "PromptTemplateId");

-- ===== Provider 元数据 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_provider_model_metadata_provider_model ON provider_model_metadata ("ProviderKey", "ModelName");

-- ===== 系统设置 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_system_settings_key ON system_settings ("Key");

-- ===== 用户 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_users_username ON users ("Username");

-- ===== Wiki 空间 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_wiki_spaces_repo_lang_view ON wiki_spaces ("RepositoryId", "Language", "ViewType");

-- ===== Wiki 版本 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_wiki_versions_space_version ON wiki_versions ("WikiSpaceId", "VersionNo");
CREATE INDEX IF NOT EXISTS ix_wiki_versions_repo_version ON wiki_versions ("RepositoryVersionId");

-- ===== Wiki 页面 =====
CREATE INDEX IF NOT EXISTS ix_wiki_pages_version ON wiki_pages ("WikiVersionId");
CREATE INDEX IF NOT EXISTS ix_wiki_pages_parent ON wiki_pages ("ParentPageId");
CREATE INDEX IF NOT EXISTS ix_wiki_pages_task ON wiki_pages ("TaskId");

-- ===== Wiki 页面关系 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_wiki_page_relations_version_src_tgt_type ON wiki_page_relations ("WikiVersionId", "SourcePageId", "TargetPageId", "RelationType");

-- ===== 代码索引 =====
CREATE INDEX IF NOT EXISTS ix_code_index_entries_repo_version ON code_index_entries ("RepositoryVersionId");
CREATE UNIQUE INDEX IF NOT EXISTS uq_code_index_entries_version_path ON code_index_entries ("RepositoryVersionId", "FilePath");
CREATE INDEX IF NOT EXISTS ix_code_index_entries_module ON code_index_entries ("ModuleName");
CREATE INDEX IF NOT EXISTS ix_code_index_chunks_entry ON code_index_chunks ("CodeIndexEntryId");

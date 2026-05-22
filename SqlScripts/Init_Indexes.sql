-- Heimdall 数据库表索引初始化
-- 用途：创建常用查询列和外键索引

-- ===== 仓库相关 =====
CREATE INDEX IF NOT EXISTS ix_repositories_owner_repo_type ON repositories (owner, repo_name, repo_type);
CREATE UNIQUE INDEX IF NOT EXISTS uq_repositories_provider_key ON repositories (provider_type, provider_repository_key) WHERE provider_repository_key IS NOT NULL;

-- ===== 仓库版本 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_repository_versions_repo_branch_commit ON repository_versions (repository_id, branch_name, commit_sha);
CREATE INDEX IF NOT EXISTS ix_repository_versions_repo_branch_latest ON repository_versions (repository_id, branch_name, is_latest_on_branch);

-- ===== 任务 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_one_running_task_per_repo_branch ON tasks (repository_id, source_branch) WHERE status = 'running';
CREATE UNIQUE INDEX IF NOT EXISTS uq_one_pending_task_per_repo_branch_type ON tasks (repository_id, source_branch, task_type) WHERE status = 'pending';
CREATE INDEX IF NOT EXISTS ix_tasks_status ON tasks (status);
CREATE INDEX IF NOT EXISTS ix_tasks_created_at ON tasks (created_at);

-- ===== 任务工件 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_task_artifacts_task_type_key ON task_artifacts (task_id, artifact_type, artifact_key);
CREATE INDEX IF NOT EXISTS ix_task_artifacts_task_stage_sequence ON task_artifacts (task_id, stage_name, sequence);

-- ===== 任务 LLM 调用日志 =====
CREATE INDEX IF NOT EXISTS ix_task_llm_call_logs_task ON task_llm_call_logs (task_id, step_order);

-- ===== LLM 调用指标 =====
CREATE INDEX IF NOT EXISTS idx_llm_call_metrics_task ON llm_call_metrics (task_id);
CREATE INDEX IF NOT EXISTS idx_llm_call_metrics_created ON llm_call_metrics (created_at);
CREATE INDEX IF NOT EXISTS idx_llm_call_metrics_provider_model ON llm_call_metrics (provider, model);

-- ===== 提示词模板 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_prompt_templates_slug ON prompt_templates (slug);
CREATE UNIQUE INDEX IF NOT EXISTS uq_prompt_templates_name_scope ON prompt_templates (name, scope_type, scope_value);

-- ===== 提示词模板历史 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_prompt_template_history_template_version ON prompt_template_history (prompt_template_id, version);

-- ===== 提示词覆盖 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_repository_prompt_overrides_repo_template ON repository_prompt_overrides (repository_id, prompt_template_id);

-- ===== Provider 元数据 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_provider_model_metadata_provider_model ON provider_model_metadata (provider_key, model_name);

-- ===== 系统设置 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_system_settings_key ON system_settings (key);

-- ===== 用户 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_users_username ON users (username);

-- ===== Wiki 空间 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_wiki_spaces_repo_lang_view ON wiki_spaces (repository_id, language, view_type);

-- ===== Wiki 版本 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_wiki_versions_space_version ON wiki_versions (wiki_space_id, version_no);
CREATE INDEX IF NOT EXISTS ix_wiki_versions_repo_version ON wiki_versions (repository_version_id);

-- ===== Wiki 页面 =====
CREATE INDEX IF NOT EXISTS ix_wiki_pages_version ON wiki_pages (wiki_version_id);
CREATE INDEX IF NOT EXISTS ix_wiki_pages_parent ON wiki_pages (parent_page_id);
CREATE INDEX IF NOT EXISTS ix_wiki_pages_task ON wiki_pages (task_id);

-- ===== Wiki 页面关系 =====
CREATE UNIQUE INDEX IF NOT EXISTS uq_wiki_page_relations_version_src_tgt_type ON wiki_page_relations (wiki_version_id, source_page_id, target_page_id, relation_type);

-- ===== 代码索引 =====
CREATE INDEX IF NOT EXISTS ix_code_index_entries_repo_version ON code_index_entries (repository_version_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_code_index_entries_version_path ON code_index_entries (repository_version_id, file_path);
CREATE INDEX IF NOT EXISTS ix_code_index_entries_module ON code_index_entries (module_name);
CREATE INDEX IF NOT EXISTS ix_code_index_chunks_entry ON code_index_chunks (code_index_entry_id);

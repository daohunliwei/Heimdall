-- Heimdall 数据库表结构初始化
-- 用途：创建所有业务表（与 SqlSugar CodeFirst 生成的结构一致）
-- 注意：执行前请先执行 Init_Extensions.sql 启用扩展

-- ===== 用户 =====
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY,
    username VARCHAR(64) NOT NULL,
    email VARCHAR(256),
    password_hash VARCHAR(256),
    source INTEGER NOT NULL DEFAULT 0,
    role VARCHAR(16) NOT NULL DEFAULT 'Viewer',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== 仓库 =====
CREATE TABLE IF NOT EXISTS repositories (
    id UUID PRIMARY KEY,
    provider_type VARCHAR(32) NOT NULL DEFAULT 'github',
    provider_repository_key VARCHAR(256),
    display_name VARCHAR(512) NOT NULL DEFAULT '',
    owner VARCHAR(128) NOT NULL DEFAULT '',
    repo_name VARCHAR(128) NOT NULL DEFAULT '',
    repo_type VARCHAR(16) NOT NULL DEFAULT 'github',
    repo_url TEXT,
    clone_url TEXT,
    default_branch VARCHAR(128) NOT NULL DEFAULT 'main',
    default_language VARCHAR(8) NOT NULL DEFAULT 'zh',
    description TEXT,
    is_archived BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== 仓库快照版本 =====
CREATE TABLE IF NOT EXISTS repository_versions (
    id UUID PRIMARY KEY,
    repository_id UUID NOT NULL REFERENCES repositories(id) ON DELETE CASCADE,
    branch_name VARCHAR(256) NOT NULL DEFAULT 'main',
    commit_sha VARCHAR(64) NOT NULL DEFAULT '',
    tree_fingerprint VARCHAR(128),
    commit_time TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    commit_author VARCHAR(256),
    commit_message TEXT,
    source_status VARCHAR(32) NOT NULL DEFAULT 'active',
    is_latest_on_branch BOOLEAN NOT NULL DEFAULT FALSE,
    version_source_confidence VARCHAR(16) NOT NULL DEFAULT 'exact',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== Wiki 逻辑空间 =====
CREATE TABLE IF NOT EXISTS wiki_spaces (
    id UUID PRIMARY KEY,
    repository_id UUID NOT NULL REFERENCES repositories(id) ON DELETE CASCADE,
    language VARCHAR(8) NOT NULL DEFAULT 'zh',
    view_type VARCHAR(32) NOT NULL DEFAULT 'default',
    title VARCHAR(200) NOT NULL DEFAULT '',
    description TEXT,
    published_wiki_version_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== Wiki 生成版本 =====
CREATE TABLE IF NOT EXISTS wiki_versions (
    id UUID PRIMARY KEY,
    wiki_space_id UUID NOT NULL REFERENCES wiki_spaces(id) ON DELETE CASCADE,
    repository_version_id UUID NOT NULL REFERENCES repository_versions(id) ON DELETE CASCADE,
    version_no INTEGER NOT NULL DEFAULT 1,
    generation_mode VARCHAR(16) NOT NULL DEFAULT 'latest',
    generation_profile VARCHAR(32) NOT NULL DEFAULT 'comprehensive',
    prompt_profile_hash VARCHAR(64),
    model_profile_hash VARCHAR(64),
    status VARCHAR(16) NOT NULL DEFAULT 'draft',
    is_force_refresh BOOLEAN NOT NULL DEFAULT FALSE,
    page_count INTEGER,
    toc_depth INTEGER,
    summary_markdown TEXT,
    structure_json TEXT,
    created_by_task_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ
);

-- ===== 任务记录 =====
CREATE TABLE IF NOT EXISTS tasks (
    id UUID PRIMARY KEY,
    task_type VARCHAR(16) NOT NULL DEFAULT 'wiki',
    status VARCHAR(16) NOT NULL DEFAULT 'pending',
    repository_id UUID REFERENCES repositories(id) ON DELETE SET NULL,
    source_branch VARCHAR(128) NOT NULL DEFAULT 'main',
    user_id UUID REFERENCES users(id) ON DELETE SET NULL,
    request_hash VARCHAR(64) NOT NULL DEFAULT '',
    provider VARCHAR(32),
    model VARCHAR(64),
    language VARCHAR(8),
    progress_percent INTEGER NOT NULL DEFAULT 0,
    progress_message TEXT,
    total_prompt_tokens INTEGER NOT NULL DEFAULT 0,
    total_completion_tokens INTEGER NOT NULL DEFAULT 0,
    result_json JSONB,
    error_message TEXT,
    current_stage VARCHAR(64) NOT NULL DEFAULT 'queued',
    current_stage_status VARCHAR(16) NOT NULL DEFAULT 'pending',
    last_successful_stage VARCHAR(64),
    last_artifact_id UUID,
    attempt_count INTEGER NOT NULL DEFAULT 0,
    resume_count INTEGER NOT NULL DEFAULT 0,
    auto_resume_fail_count INTEGER NOT NULL DEFAULT 0,
    target_branch VARCHAR(128),
    resolved_repository_version_id UUID REFERENCES repository_versions(id) ON DELETE SET NULL,
    result_wiki_version_id UUID REFERENCES wiki_versions(id) ON DELETE SET NULL,
    refresh_strategy VARCHAR(16),
    force_refresh BOOLEAN NOT NULL DEFAULT FALSE,
    config_hash VARCHAR(64),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ
);

-- ===== 任务工件 =====
CREATE TABLE IF NOT EXISTS task_artifacts (
    id UUID PRIMARY KEY,
    task_id UUID NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    artifact_type VARCHAR(64) NOT NULL DEFAULT '',
    artifact_key VARCHAR(128) NOT NULL DEFAULT '',
    stage_name VARCHAR(64) NOT NULL DEFAULT '',
    status VARCHAR(16) NOT NULL DEFAULT 'completed',
    sequence INTEGER NOT NULL DEFAULT 0,
    content_hash VARCHAR(64),
    summary TEXT,
    payload_json JSONB NOT NULL DEFAULT '{}',
    error_message TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== 任务 LLM 调用日志 =====
CREATE TABLE IF NOT EXISTS task_llm_call_logs (
    id UUID PRIMARY KEY,
    task_id UUID NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    step_order INTEGER NOT NULL DEFAULT 0,
    call_type VARCHAR(32) NOT NULL DEFAULT '',
    provider VARCHAR(32),
    model VARCHAR(64),
    prompt_tokens INTEGER NOT NULL DEFAULT 0,
    completion_tokens INTEGER NOT NULL DEFAULT 0,
    total_tokens INTEGER NOT NULL DEFAULT 0,
    request_preview TEXT,
    response_preview TEXT,
    latency_ms INTEGER NOT NULL DEFAULT 0,
    is_error BOOLEAN NOT NULL DEFAULT FALSE,
    error_message TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== LLM 调用指标 =====
CREATE TABLE IF NOT EXISTS llm_call_metrics (
    id UUID PRIMARY KEY,
    task_id UUID NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    stage VARCHAR(64) NOT NULL DEFAULT '',
    provider VARCHAR(32) NOT NULL DEFAULT '',
    model VARCHAR(64) NOT NULL DEFAULT '',
    input_tokens INTEGER NOT NULL DEFAULT 0,
    output_tokens INTEGER NOT NULL DEFAULT 0,
    cache_hit_tokens INTEGER NOT NULL DEFAULT 0,
    latency_ms INTEGER NOT NULL DEFAULT 0,
    success BOOLEAN NOT NULL DEFAULT TRUE,
    error_type VARCHAR(64),
    is_estimated BOOLEAN NOT NULL DEFAULT FALSE,
    is_streaming BOOLEAN NOT NULL DEFAULT FALSE,
    first_token_latency_ms INTEGER,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== Provider 模型元数据 =====
CREATE TABLE IF NOT EXISTS provider_model_metadata (
    id UUID PRIMARY KEY,
    provider_key VARCHAR(64) NOT NULL DEFAULT '',
    model_name VARCHAR(128) NOT NULL DEFAULT '',
    billing_type VARCHAR(32) NOT NULL DEFAULT 'TokenPlan',
    max_context_tokens INTEGER NOT NULL DEFAULT 128000,
    max_output_tokens INTEGER NOT NULL DEFAULT 8192,
    rate_limit_per_minute INTEGER,
    input_token_price DECIMAL(10,6),
    output_token_price DECIMAL(10,6),
    call_price DECIMAL(10,6),
    supports_caching BOOLEAN NOT NULL DEFAULT FALSE,
    context_fill_ratio DOUBLE PRECISION NOT NULL DEFAULT 0.65,
    context_warning_threshold DOUBLE PRECISION NOT NULL DEFAULT 0.90,
    supports_streaming BOOLEAN NOT NULL DEFAULT TRUE,
    raw_endpoint TEXT,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== 提示词模板 =====
CREATE TABLE IF NOT EXISTS prompt_templates (
    id UUID PRIMARY KEY,
    slug VARCHAR(128) NOT NULL DEFAULT '',
    name VARCHAR(128) NOT NULL DEFAULT '',
    layer VARCHAR(16) NOT NULL DEFAULT 'system',
    scope_type VARCHAR(16) NOT NULL DEFAULT 'global',
    scope_value VARCHAR(128),
    template_content TEXT NOT NULL DEFAULT '',
    category VARCHAR(64) NOT NULL DEFAULT 'general',
    sub_category VARCHAR(64),
    priority INTEGER NOT NULL DEFAULT 0,
    applicable_providers TEXT[],
    variables TEXT[],
    is_system BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    version INTEGER NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== 提示词模板历史 =====
CREATE TABLE IF NOT EXISTS prompt_template_history (
    id UUID PRIMARY KEY,
    prompt_template_id UUID NOT NULL REFERENCES prompt_templates(id) ON DELETE CASCADE,
    version INTEGER NOT NULL DEFAULT 0,
    template_content TEXT NOT NULL DEFAULT '',
    changed_by UUID,
    changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== 仓库级提示词覆盖 =====
CREATE TABLE IF NOT EXISTS repository_prompt_overrides (
    id UUID PRIMARY KEY,
    repository_id UUID NOT NULL REFERENCES repositories(id) ON DELETE CASCADE,
    prompt_template_id UUID NOT NULL REFERENCES prompt_templates(id) ON DELETE CASCADE,
    override_content TEXT,
    strategy VARCHAR(16) NOT NULL DEFAULT 'override',
    priority INTEGER NOT NULL DEFAULT 0,
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== 系统设置 =====
CREATE TABLE IF NOT EXISTS system_settings (
    id UUID PRIMARY KEY,
    key VARCHAR(128) NOT NULL DEFAULT '',
    value TEXT NOT NULL DEFAULT '',
    description TEXT,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== Wiki 页面 =====
CREATE TABLE IF NOT EXISTS wiki_pages (
    id UUID PRIMARY KEY,
    wiki_version_id UUID NOT NULL REFERENCES wiki_versions(id) ON DELETE CASCADE,
    task_id UUID REFERENCES tasks(id) ON DELETE SET NULL,
    page_order INTEGER NOT NULL DEFAULT 0,
    title TEXT NOT NULL DEFAULT '',
    nav_title VARCHAR(256),
    content_markdown TEXT,
    parent_page_id UUID REFERENCES wiki_pages(id) ON DELETE SET NULL,
    page_type VARCHAR(16) NOT NULL DEFAULT 'article',
    importance VARCHAR(8) NOT NULL DEFAULT 'medium',
    depth INTEGER NOT NULL DEFAULT 0,
    outline_json JSONB,
    summary TEXT,
    source_coverage_json JSONB,
    file_paths TEXT[],
    token_count INTEGER,
    status VARCHAR(16) NOT NULL DEFAULT 'ready',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== Wiki 页面关系 =====
CREATE TABLE IF NOT EXISTS wiki_page_relations (
    id UUID PRIMARY KEY,
    wiki_version_id UUID NOT NULL REFERENCES wiki_versions(id) ON DELETE CASCADE,
    source_page_id UUID NOT NULL REFERENCES wiki_pages(id) ON DELETE CASCADE,
    target_page_id UUID NOT NULL REFERENCES wiki_pages(id) ON DELETE CASCADE,
    relation_type VARCHAR(32) NOT NULL DEFAULT 'related_to',
    metadata_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ===== 代码索引条目 =====
CREATE TABLE IF NOT EXISTS code_index_entries (
    id UUID PRIMARY KEY,
    file_path VARCHAR(1024) NOT NULL DEFAULT '',
    module_name VARCHAR(256) NOT NULL DEFAULT '',
    file_type VARCHAR(64) NOT NULL DEFAULT 'source',
    language VARCHAR(64) NOT NULL DEFAULT '',
    size_bytes BIGINT NOT NULL DEFAULT 0,
    importance_score INTEGER NOT NULL DEFAULT 0,
    exported_symbols TEXT NOT NULL DEFAULT '[]',
    dependency_hints TEXT NOT NULL DEFAULT '[]',
    call_graph_json TEXT,
    dependency_edges_json TEXT,
    design_pattern_hints TEXT,
    repository_version_id UUID NOT NULL REFERENCES repository_versions(id) ON DELETE CASCADE
);

-- ===== 代码索引分块 =====
CREATE TABLE IF NOT EXISTS code_index_chunks (
    id UUID PRIMARY KEY,
    content TEXT NOT NULL DEFAULT '',
    start_line INTEGER NOT NULL DEFAULT 0,
    end_line INTEGER NOT NULL DEFAULT 0,
    language VARCHAR(64) NOT NULL DEFAULT '',
    code_index_entry_id UUID NOT NULL REFERENCES code_index_entries(id) ON DELETE CASCADE
);

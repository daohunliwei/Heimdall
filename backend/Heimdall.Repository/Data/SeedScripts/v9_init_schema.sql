-- ========================================================================
-- Heimdall 数据库初始化脚本 (Idempotent)
-- 可在任何 PostgreSQL 数据库上运行，自动补齐缺失的表、列、索引
-- 版本：v9 — 与 AppDbContext 模型对齐
-- ========================================================================

-- 1. __EFMigrationsHistory
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- 2. users
CREATE TABLE IF NOT EXISTS users (
    "Id" uuid NOT NULL,
    "Username" character varying(128) NOT NULL,
    "PasswordHash" character varying(256) NOT NULL,
    "Role" character varying(32) NOT NULL DEFAULT 'user',
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_users" PRIMARY KEY ("Id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "ix_users_username" ON users ("Username");

-- 3. repositories
CREATE TABLE IF NOT EXISTS repositories (
    "Id" uuid NOT NULL,
    "Owner" character varying(256) NOT NULL,
    "RepoName" character varying(256) NOT NULL,
    "RepoType" character varying(32) NOT NULL DEFAULT 'github',
    "RepoUrl" character varying(2048),
    "DefaultBranch" character varying(128) DEFAULT 'main',
    "DefaultLanguage" character varying(16) DEFAULT 'zh',
    "IsArchived" boolean NOT NULL DEFAULT false,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_repositories" PRIMARY KEY ("Id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "ix_repositories_owner_repo_type" ON repositories ("Owner", "RepoName", "RepoType");

-- 4. tasks
CREATE TABLE IF NOT EXISTS tasks (
    "Id" uuid NOT NULL,
    "TaskType" character varying(32) NOT NULL DEFAULT 'wiki',
    "RepoUrl" character varying(2048),
    "RepoType" character varying(32),
    "Token" text,
    "Provider" character varying(32),
    "Model" character varying(64),
    "CustomModel" character varying(128),
    "Language" character varying(16) DEFAULT 'zh',
    "Comprehensive" boolean NOT NULL DEFAULT true,
    "ForceRefresh" boolean NOT NULL DEFAULT false,
    "RefreshStrategy" character varying(32) DEFAULT 'latest',
    "GenerationProfile" character varying(32) DEFAULT 'comprehensive',
    "Branch" character varying(128) DEFAULT 'main',
    "Status" character varying(16) NOT NULL DEFAULT 'pending',
    "CurrentStage" character varying(64),
    "CurrentStageStatus" character varying(16),
    "LastSuccessfulStage" character varying(64),
    "LastArtifactId" uuid,
    "AttemptCount" integer NOT NULL DEFAULT 0,
    "ProgressPercent" integer NOT NULL DEFAULT 0,
    "ProgressMessage" text,
    "ErrorMessage" text,
    "TotalPromptTokens" integer NOT NULL DEFAULT 0,
    "TotalCompletionTokens" integer NOT NULL DEFAULT 0,
    "UserId" uuid,
    "RepositoryId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "StartedAt" timestamp with time zone,
    "CompletedAt" timestamp with time zone,
    CONSTRAINT "PK_tasks" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_tasks_users_UserId" FOREIGN KEY ("UserId") REFERENCES users("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_tasks_repositories_RepositoryId" FOREIGN KEY ("RepositoryId") REFERENCES repositories("Id") ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS "ix_tasks_status" ON tasks ("Status");
CREATE INDEX IF NOT EXISTS "ix_tasks_created" ON tasks ("CreatedAt");
CREATE INDEX IF NOT EXISTS "ix_tasks_UserId" ON tasks ("UserId");
CREATE INDEX IF NOT EXISTS "ix_tasks_RepositoryId" ON tasks ("RepositoryId");

-- 5. repository_versions
CREATE TABLE IF NOT EXISTS repository_versions (
    "Id" uuid NOT NULL,
    "RepositoryId" uuid NOT NULL,
    "BranchName" character varying(256) NOT NULL DEFAULT 'main',
    "CommitSha" character varying(64),
    "CommitTime" timestamp with time zone,
    "CommitAuthor" character varying(256),
    "CommitMessage" text,
    "IsLatestOnBranch" boolean NOT NULL DEFAULT false,
    "SourceStatus" character varying(32) DEFAULT 'ready',
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_repository_versions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_repository_versions_repositories_RepositoryId" FOREIGN KEY ("RepositoryId") REFERENCES repositories("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS "ix_repository_versions_repo_branch_sha" ON repository_versions ("RepositoryId", "BranchName", "CommitSha");
CREATE INDEX IF NOT EXISTS "ix_repository_versions_repo" ON repository_versions ("RepositoryId");

-- 6. wiki_spaces
CREATE TABLE IF NOT EXISTS wiki_spaces (
    "Id" uuid NOT NULL,
    "RepositoryId" uuid NOT NULL,
    "Language" character varying(16) NOT NULL DEFAULT 'zh',
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_wiki_spaces" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_wiki_spaces_repositories_RepositoryId" FOREIGN KEY ("RepositoryId") REFERENCES repositories("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS "ix_wiki_spaces_repo_lang" ON wiki_spaces ("RepositoryId", "Language");

-- 7. wiki_versions
CREATE TABLE IF NOT EXISTS wiki_versions (
    "Id" uuid NOT NULL,
    "WikiSpaceId" uuid NOT NULL,
    "RepositoryVersionId" uuid NOT NULL,
    "VersionNo" integer NOT NULL DEFAULT 1,
    "GenerationMode" character varying(32) NOT NULL DEFAULT 'rebuild',
    "GenerationProfile" character varying(32),
    "Status" character varying(16) NOT NULL DEFAULT 'draft',
    "PageCount" integer NOT NULL DEFAULT 0,
    "TocDepth" integer DEFAULT 2,
    "SummaryMarkdown" text,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "CompletedAt" timestamp with time zone,
    CONSTRAINT "PK_wiki_versions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_wiki_versions_wiki_spaces_WikiSpaceId" FOREIGN KEY ("WikiSpaceId") REFERENCES wiki_spaces("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_wiki_versions_repository_versions_RepositoryVersionId" FOREIGN KEY ("RepositoryVersionId") REFERENCES repository_versions("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "ix_wiki_versions_space" ON wiki_versions ("WikiSpaceId");
CREATE INDEX IF NOT EXISTS "ix_wiki_versions_repo_version" ON wiki_versions ("RepositoryVersionId");

-- 8. wiki_pages
CREATE TABLE IF NOT EXISTS wiki_pages (
    "Id" uuid NOT NULL,
    "WikiVersionId" uuid NOT NULL,
    "Title" character varying(512) NOT NULL,
    "Content" text DEFAULT '',
    "PageType" character varying(32) DEFAULT 'article',
    "Importance" character varying(16) DEFAULT 'medium',
    "PageOrder" integer NOT NULL DEFAULT 0,
    "ParentPageId" character varying(128),
    "Depth" integer DEFAULT 0,
    "ContentDepthLevel" character varying(32),
    "FilePathsJson" text DEFAULT '[]',
    "NavTitle" character varying(256),
    "TokenCount" integer DEFAULT 0,
    "Status" character varying(16) DEFAULT 'ready',
    "Summary" text,
    "RelatedPagesJson" text DEFAULT '[]',
    "PrerequisitePagesJson" text DEFAULT '[]',
    "SearchKeywordsJson" text DEFAULT '[]',
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_wiki_pages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_wiki_pages_wiki_versions_WikiVersionId" FOREIGN KEY ("WikiVersionId") REFERENCES wiki_versions("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "ix_wiki_pages_version" ON wiki_pages ("WikiVersionId");
CREATE INDEX IF NOT EXISTS "ix_wiki_pages_parent" ON wiki_pages ("ParentPageId");
CREATE INDEX IF NOT EXISTS "ix_wiki_pages_order" ON wiki_pages ("PageOrder");

-- 9. wiki_page_relations
CREATE TABLE IF NOT EXISTS wiki_page_relations (
    "Id" uuid NOT NULL,
    "WikiVersionId" uuid NOT NULL,
    "SourcePageId" character varying(128) NOT NULL,
    "TargetPageId" character varying(128) NOT NULL,
    "RelationType" character varying(32) NOT NULL DEFAULT 'related',
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_wiki_page_relations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_wiki_page_relations_wiki_versions_WikiVersionId" FOREIGN KEY ("WikiVersionId") REFERENCES wiki_versions("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS "ix_wiki_page_relations_unique" ON wiki_page_relations ("WikiVersionId", "SourcePageId", "TargetPageId", "RelationType");

-- 10. task_artifacts
CREATE TABLE IF NOT EXISTS task_artifacts (
    "Id" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "ArtifactType" character varying(64) NOT NULL,
    "ArtifactKey" character varying(128) NOT NULL,
    "StageName" character varying(64) NOT NULL,
    "Status" character varying(16) NOT NULL DEFAULT 'completed',
    "Sequence" integer NOT NULL DEFAULT 0,
    "ContentHash" character varying(64),
    "Summary" text,
    "PayloadJson" jsonb NOT NULL DEFAULT '{}',
    "ErrorMessage" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_task_artifacts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_task_artifacts_tasks_TaskId" FOREIGN KEY ("TaskId") REFERENCES tasks("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS "ix_task_artifacts_task_type_key" ON task_artifacts ("TaskId", "ArtifactType", "ArtifactKey");
CREATE INDEX IF NOT EXISTS "ix_task_artifacts_task_stage_sequence" ON task_artifacts ("TaskId", "StageName", "Sequence");

-- 11. task_llm_call_logs
CREATE TABLE IF NOT EXISTS task_llm_call_logs (
    "Id" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "StepOrder" integer NOT NULL DEFAULT 0,
    "Stage" character varying(64) NOT NULL,
    "Provider" character varying(32) NOT NULL,
    "Model" character varying(64) NOT NULL,
    "Prompt" text,
    "Response" text,
    "LatencyMs" integer NOT NULL DEFAULT 0,
    "IsError" boolean NOT NULL DEFAULT false,
    "ErrorMessage" text,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_task_llm_call_logs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_task_llm_call_logs_tasks_TaskId" FOREIGN KEY ("TaskId") REFERENCES tasks("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "ix_task_llm_call_logs_task" ON task_llm_call_logs ("TaskId");

-- 12. code_embedding_chunks
CREATE TABLE IF NOT EXISTS code_embedding_chunks (
    "Id" uuid NOT NULL,
    "RepositoryVersionId" uuid NOT NULL,
    "FilePath" character varying(1024) NOT NULL,
    "Content" text NOT NULL,
    "StartLine" integer NOT NULL DEFAULT 0,
    "EndLine" integer NOT NULL DEFAULT 0,
    "Language" character varying(64) NOT NULL DEFAULT 'text',
    "ChunkIndex" integer NOT NULL DEFAULT 0,
    "ChunkType" character varying(32) DEFAULT 'code',
    "Embedding" bytea,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_code_embedding_chunks" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_code_embedding_chunks_repository_versions_RepositoryVersionId" FOREIGN KEY ("RepositoryVersionId") REFERENCES repository_versions("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "ix_code_embedding_chunks_version" ON code_embedding_chunks ("RepositoryVersionId");
CREATE INDEX IF NOT EXISTS "ix_code_embedding_chunks_file" ON code_embedding_chunks ("FilePath");

-- 13. wiki_embedding_chunks
CREATE TABLE IF NOT EXISTS wiki_embedding_chunks (
    "Id" uuid NOT NULL,
    "WikiVersionId" uuid NOT NULL,
    "PageId" character varying(128) NOT NULL,
    "Content" text NOT NULL,
    "ChunkIndex" integer NOT NULL DEFAULT 0,
    "Embedding" bytea,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_wiki_embedding_chunks" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_wiki_embedding_chunks_wiki_versions_WikiVersionId" FOREIGN KEY ("WikiVersionId") REFERENCES wiki_versions("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "ix_wiki_embedding_chunks_version" ON wiki_embedding_chunks ("WikiVersionId");

-- 14. prompt_templates
CREATE TABLE IF NOT EXISTS prompt_templates (
    "Id" uuid NOT NULL,
    "Slug" character varying(128) NOT NULL,
    "Category" character varying(64) NOT NULL,
    "SubCategory" character varying(64) DEFAULT 'base',
    "TemplateContent" text NOT NULL,
    "TemplateEngine" character varying(32) DEFAULT 'replace',
    "Priority" integer NOT NULL DEFAULT 0,
    "ApplicableProviders" text,
    "IsSystem" boolean NOT NULL DEFAULT false,
    "Version" integer NOT NULL DEFAULT 1,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_prompt_templates" PRIMARY KEY ("Id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "ix_prompt_templates_slug" ON prompt_templates ("Slug");

-- 15. repository_prompt_overrides
CREATE TABLE IF NOT EXISTS repository_prompt_overrides (
    "Id" uuid NOT NULL,
    "PromptTemplateId" uuid NOT NULL,
    "RepositoryId" uuid NOT NULL,
    "OverrideContent" text,
    "Strategy" character varying(32) DEFAULT 'override',
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_repository_prompt_overrides" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_repository_prompt_overrides_prompt_templates_PromptTemplateId" FOREIGN KEY ("PromptTemplateId") REFERENCES prompt_templates("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_repository_prompt_overrides_repositories_RepositoryId" FOREIGN KEY ("RepositoryId") REFERENCES repositories("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS "ix_repo_prompt_overrides_template_repo" ON repository_prompt_overrides ("PromptTemplateId", "RepositoryId");

-- 16. prompt_template_history
CREATE TABLE IF NOT EXISTS prompt_template_history (
    "Id" uuid NOT NULL,
    "PromptTemplateId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "TemplateContent" text NOT NULL,
    "ChangeNote" text,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_prompt_template_history" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_prompt_template_history_prompt_templates_PromptTemplateId" FOREIGN KEY ("PromptTemplateId") REFERENCES prompt_templates("Id") ON DELETE CASCADE
);

-- 17. system_settings
CREATE TABLE IF NOT EXISTS system_settings (
    "Id" uuid NOT NULL,
    "SettingKey" character varying(128) NOT NULL,
    "SettingValue" text NOT NULL,
    "Description" text,
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_system_settings" PRIMARY KEY ("Id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "ix_system_settings_key" ON system_settings ("SettingKey");

-- 18. code_index_entries
CREATE TABLE IF NOT EXISTS code_index_entries (
    "Id" uuid NOT NULL,
    "FilePath" character varying(1024) NOT NULL,
    "ModuleName" character varying(256) NOT NULL,
    "FileType" character varying(64) NOT NULL,
    "Language" character varying(64) NOT NULL,
    "SizeBytes" bigint NOT NULL DEFAULT 0,
    "ImportanceScore" integer NOT NULL DEFAULT 0,
    "exported_symbols" text NOT NULL DEFAULT '',
    "dependency_hints" text NOT NULL DEFAULT '',
    "CallGraphJson" text,
    "DependencyEdgesJson" text,
    "DesignPatternHints" text,
    "RepositoryVersionId" uuid NOT NULL,
    CONSTRAINT "PK_code_index_entries" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_code_index_entries_repository_versions_RepositoryVersionId" FOREIGN KEY ("RepositoryVersionId") REFERENCES repository_versions("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "ix_code_index_entries_module" ON code_index_entries ("ModuleName");
CREATE INDEX IF NOT EXISTS "ix_code_index_entries_version" ON code_index_entries ("RepositoryVersionId");
CREATE UNIQUE INDEX IF NOT EXISTS "ix_code_index_entries_version_file" ON code_index_entries ("RepositoryVersionId", "FilePath");

-- 19. code_index_chunks
CREATE TABLE IF NOT EXISTS code_index_chunks (
    "Id" uuid NOT NULL,
    "Content" text NOT NULL,
    "StartLine" integer NOT NULL DEFAULT 0,
    "EndLine" integer NOT NULL DEFAULT 0,
    "Language" character varying(64) NOT NULL,
    "Embedding" bytea,
    "CodeIndexEntryId" uuid NOT NULL,
    CONSTRAINT "PK_code_index_chunks" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_code_index_chunks_code_index_entries_CodeIndexEntryId" FOREIGN KEY ("CodeIndexEntryId") REFERENCES code_index_entries("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "ix_code_index_chunks_entry" ON code_index_chunks ("CodeIndexEntryId");

-- 20. llm_call_metrics
CREATE TABLE IF NOT EXISTS llm_call_metrics (
    "Id" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "Stage" character varying(64) NOT NULL,
    "Provider" character varying(32) NOT NULL,
    "Model" character varying(64) NOT NULL,
    "InputTokens" integer NOT NULL DEFAULT 0,
    "OutputTokens" integer NOT NULL DEFAULT 0,
    "CacheHitTokens" integer NOT NULL DEFAULT 0,
    "LatencyMs" integer NOT NULL DEFAULT 0,
    "Success" boolean NOT NULL DEFAULT true,
    "ErrorType" character varying(64),
    "IsEstimated" boolean NOT NULL DEFAULT false,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_llm_call_metrics" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_llm_call_metrics_tasks_TaskId" FOREIGN KEY ("TaskId") REFERENCES tasks("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "idx_llm_call_metrics_created" ON llm_call_metrics ("CreatedAt");
CREATE INDEX IF NOT EXISTS "idx_llm_call_metrics_provider_model" ON llm_call_metrics ("Provider", "Model");
CREATE INDEX IF NOT EXISTS "idx_llm_call_metrics_task" ON llm_call_metrics ("TaskId");

-- 21. provider_model_metadata
CREATE TABLE IF NOT EXISTS provider_model_metadata (
    "Id" uuid NOT NULL,
    "ProviderKey" character varying(64) NOT NULL,
    "ModelName" character varying(128) NOT NULL,
    "BillingType" character varying(32) NOT NULL DEFAULT 'TokenPlan',
    "MaxContextTokens" integer NOT NULL DEFAULT 128000,
    "MaxOutputTokens" integer NOT NULL DEFAULT 8192,
    "RateLimitPerMinute" integer,
    "InputTokenPrice" numeric(10,6),
    "OutputTokenPrice" numeric(10,6),
    "CallPrice" numeric(10,6),
    "SupportsCaching" boolean NOT NULL DEFAULT false,
    "ContextFillRatio" double precision NOT NULL DEFAULT 0.65,
    "ContextWarningThreshold" double precision NOT NULL DEFAULT 0.90,
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_provider_model_metadata" PRIMARY KEY ("Id")
);
CREATE UNIQUE INDEX IF NOT EXISTS "ix_provider_model_metadata_key_model" ON provider_model_metadata ("ProviderKey", "ModelName");

-- 22. Mark all known migrations as applied
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
    ('20260514081700_InitialV2', '10.0.0'),
    ('20260514102128_FixStructureJsonToText', '10.0.0'),
    ('20260514155446_V3Phase1TaskArtifacts', '10.0.0'),
    ('20260515030804_V4PromptManagement', '10.0.0'),
    ('20260515061300_V4RemoveLegacyWiki', '10.0.0'),
    ('20260515082608_V5PromptManagement', '10.0.0'),
    ('20260520061302_V7_AddLlmCallMetricsAndCodeIndexExpansion', '10.0.0'),
    ('20260521135451_V8_AddProviderModelMetadata', '10.0.0')
ON CONFLICT DO NOTHING;

-- Done
SELECT 'v9_init_schema: All tables verified and ready' AS result;

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prompt_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Layer = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ScopeType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "global"),
                    ScopeValue = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TemplateContent = table.Column<string>(type: "text", nullable: false),
                    Variables = table.Column<string[]>(type: "text[]", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "repositories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "github"),
                    provider_repository_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    display_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Owner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RepoName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RepoType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RepoUrl = table.Column<string>(type: "text", nullable: true),
                    CloneUrl = table.Column<string>(type: "text", nullable: true),
                    DefaultBranch = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "main"),
                    DefaultLanguage = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "zh"),
                    Description = table.Column<string>(type: "text", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repositories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Viewer"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "repository_prompt_overrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    OverrideContent = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repository_prompt_overrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repository_prompt_overrides_prompt_templates_PromptTemplate~",
                        column: x => x.PromptTemplateId,
                        principalTable: "prompt_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_repository_prompt_overrides_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repository_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tree_fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    commit_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    commit_author = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    commit_message = table.Column<string>(type: "text", nullable: true),
                    source_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "active"),
                    is_latest_on_branch = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    version_source_confidence = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "exact"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_repository_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_repository_versions_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wiki_spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "zh"),
                    view_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "default"),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    published_wiki_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wiki_spaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wiki_spaces_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wikis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SourceRepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBranch = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "main"),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "zh"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wikis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wikis_repositories_SourceRepositoryId",
                        column: x => x.SourceRepositoryId,
                        principalTable: "repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "code_embedding_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: false),
                    symbol_path = table.Column<string>(type: "text", nullable: true),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    chunk_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "code_block"),
                    language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    start_line = table.Column<int>(type: "integer", nullable: false),
                    end_line = table.Column<int>(type: "integer", nullable: false),
                    content_raw = table.Column<string>(type: "text", nullable: false),
                    content_normalized = table.Column<string>(type: "text", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    token_count = table.Column<int>(type: "integer", nullable: true),
                    embedding_model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    embedding_vector = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_embedding_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_code_embedding_chunks_repository_versions_RepositoryVersion~",
                        column: x => x.RepositoryVersionId,
                        principalTable: "repository_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wiki_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WikiSpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    generation_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "latest"),
                    generation_profile = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "comprehensive"),
                    prompt_profile_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    model_profile_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "draft"),
                    is_force_refresh = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    page_count = table.Column<int>(type: "integer", nullable: true),
                    toc_depth = table.Column<int>(type: "integer", nullable: true),
                    summary_markdown = table.Column<string>(type: "text", nullable: true),
                    structure_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_by_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wiki_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wiki_versions_repository_versions_RepositoryVersionId",
                        column: x => x.RepositoryVersionId,
                        principalTable: "repository_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wiki_versions_wiki_spaces_WikiSpaceId",
                        column: x => x.WikiSpaceId,
                        principalTable: "wiki_spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    source_branch = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "main"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ProgressMessage = table.Column<string>(type: "text", nullable: true),
                    TotalPromptTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalCompletionTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    target_branch = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    resolved_repository_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    result_wiki_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    refresh_strategy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    force_refresh = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    config_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tasks_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "repositories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tasks_repository_versions_resolved_repository_version_id",
                        column: x => x.resolved_repository_version_id,
                        principalTable: "repository_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tasks_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_tasks_wiki_versions_result_wiki_version_id",
                        column: x => x.result_wiki_version_id,
                        principalTable: "wiki_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "task_llm_call_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    CallType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PromptTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CompletionTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RequestPreview = table.Column<string>(type: "text", nullable: true),
                    ResponsePreview = table.Column<string>(type: "text", nullable: true),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    IsError = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_llm_call_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_llm_call_logs_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wiki_pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WikiId = table.Column<Guid>(type: "uuid", nullable: false),
                    WikiVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    PageOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Title = table.Column<string>(type: "text", nullable: false),
                    nav_title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContentMarkdown = table.Column<string>(type: "text", nullable: true),
                    ParentPageId = table.Column<Guid>(type: "uuid", nullable: true),
                    page_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "article"),
                    Importance = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "medium"),
                    depth = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    outline_json = table.Column<string>(type: "jsonb", nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    source_coverage_json = table.Column<string>(type: "jsonb", nullable: true),
                    FilePaths = table.Column<string[]>(type: "text[]", nullable: true),
                    token_count = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "ready"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wiki_pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wiki_pages_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_wiki_pages_wiki_pages_ParentPageId",
                        column: x => x.ParentPageId,
                        principalTable: "wiki_pages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_wiki_pages_wiki_versions_WikiVersionId",
                        column: x => x.WikiVersionId,
                        principalTable: "wiki_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_wiki_pages_wikis_WikiId",
                        column: x => x.WikiId,
                        principalTable: "wikis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wiki_embedding_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WikiVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WikiPageId = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    chunk_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "section"),
                    content_raw = table.Column<string>(type: "text", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    token_count = table.Column<int>(type: "integer", nullable: true),
                    embedding_model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    embedding_vector = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wiki_embedding_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wiki_embedding_chunks_wiki_pages_WikiPageId",
                        column: x => x.WikiPageId,
                        principalTable: "wiki_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wiki_embedding_chunks_wiki_versions_WikiVersionId",
                        column: x => x.WikiVersionId,
                        principalTable: "wiki_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wiki_page_relations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WikiVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePageId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetPageId = table.Column<Guid>(type: "uuid", nullable: false),
                    relation_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "related_to"),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wiki_page_relations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wiki_page_relations_wiki_pages_SourcePageId",
                        column: x => x.SourcePageId,
                        principalTable: "wiki_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wiki_page_relations_wiki_pages_TargetPageId",
                        column: x => x.TargetPageId,
                        principalTable: "wiki_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wiki_page_relations_wiki_versions_WikiVersionId",
                        column: x => x.WikiVersionId,
                        principalTable: "wiki_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_code_embedding_chunks_version_file_chunk",
                table: "code_embedding_chunks",
                columns: new[] { "RepositoryVersionId", "file_path", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompt_templates_Name_ScopeType_ScopeValue",
                table: "prompt_templates",
                columns: new[] { "Name", "ScopeType", "ScopeValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repositories_Owner_RepoName_RepoType",
                table: "repositories",
                columns: new[] { "Owner", "RepoName", "RepoType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_repositories_provider_type_provider_repository_key",
                table: "repositories",
                columns: new[] { "provider_type", "provider_repository_key" },
                unique: true,
                filter: "provider_repository_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_repository_prompt_overrides_PromptTemplateId",
                table: "repository_prompt_overrides",
                column: "PromptTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_repository_prompt_overrides_RepositoryId_PromptTemplateId",
                table: "repository_prompt_overrides",
                columns: new[] { "RepositoryId", "PromptTemplateId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_repository_versions_repo_branch_commit",
                table: "repository_versions",
                columns: new[] { "RepositoryId", "branch_name", "commit_sha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_repository_versions_repo_branch_latest",
                table: "repository_versions",
                columns: new[] { "RepositoryId", "branch_name", "is_latest_on_branch" });

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_Key",
                table: "system_settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_task_llm_call_logs_task",
                table: "task_llm_call_logs",
                columns: new[] { "TaskId", "StepOrder" });

            migrationBuilder.CreateIndex(
                name: "idx_one_pending_task_per_repo_branch_type",
                table: "tasks",
                columns: new[] { "RepositoryId", "source_branch", "TaskType" },
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "idx_one_running_task_per_repo_branch",
                table: "tasks",
                columns: new[] { "RepositoryId", "source_branch" },
                unique: true,
                filter: "status = 'running'");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_resolved_repository_version_id",
                table: "tasks",
                column: "resolved_repository_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_result_wiki_version_id",
                table: "tasks",
                column: "result_wiki_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_UserId",
                table: "tasks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Username",
                table: "users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wiki_embedding_chunks_version_page_chunk",
                table: "wiki_embedding_chunks",
                columns: new[] { "WikiVersionId", "WikiPageId", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wiki_embedding_chunks_WikiPageId",
                table: "wiki_embedding_chunks",
                column: "WikiPageId");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_page_relations_SourcePageId",
                table: "wiki_page_relations",
                column: "SourcePageId");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_page_relations_TargetPageId",
                table: "wiki_page_relations",
                column: "TargetPageId");

            migrationBuilder.CreateIndex(
                name: "ix_wiki_page_relations_version_src_tgt_type",
                table: "wiki_page_relations",
                columns: new[] { "WikiVersionId", "SourcePageId", "TargetPageId", "relation_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_ParentPageId",
                table: "wiki_pages",
                column: "ParentPageId");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_TaskId",
                table: "wiki_pages",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_WikiId",
                table: "wiki_pages",
                column: "WikiId");

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_WikiVersionId",
                table: "wiki_pages",
                column: "WikiVersionId");

            migrationBuilder.CreateIndex(
                name: "ix_wiki_spaces_repo_lang_view",
                table: "wiki_spaces",
                columns: new[] { "RepositoryId", "language", "view_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wiki_versions_repo_version",
                table: "wiki_versions",
                column: "RepositoryVersionId");

            migrationBuilder.CreateIndex(
                name: "ix_wiki_versions_space_version",
                table: "wiki_versions",
                columns: new[] { "WikiSpaceId", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wikis_SourceRepositoryId_SourceBranch_Language",
                table: "wikis",
                columns: new[] { "SourceRepositoryId", "SourceBranch", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "code_embedding_chunks");

            migrationBuilder.DropTable(
                name: "repository_prompt_overrides");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "task_llm_call_logs");

            migrationBuilder.DropTable(
                name: "wiki_embedding_chunks");

            migrationBuilder.DropTable(
                name: "wiki_page_relations");

            migrationBuilder.DropTable(
                name: "prompt_templates");

            migrationBuilder.DropTable(
                name: "wiki_pages");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "wikis");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "wiki_versions");

            migrationBuilder.DropTable(
                name: "repository_versions");

            migrationBuilder.DropTable(
                name: "wiki_spaces");

            migrationBuilder.DropTable(
                name: "repositories");
        }
    }
}

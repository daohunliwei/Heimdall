using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                    Owner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RepoName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RepoType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RepoUrl = table.Column<string>(type: "text", nullable: true),
                    CloneUrl = table.Column<string>(type: "text", nullable: true),
                    DefaultBranch = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "main"),
                    DefaultLanguage = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "zh"),
                    Description = table.Column<string>(type: "text", nullable: true),
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
                name: "embedding_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    TextContent = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<byte[]>(type: "bytea", nullable: true),
                    TokenCount = table.Column<int>(type: "integer", nullable: true),
                    IsCode = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_embedding_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_embedding_documents_repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "pending"),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceBranch = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "main"),
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                        name: "FK_tasks_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
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
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    PageOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ContentMarkdown = table.Column<string>(type: "text", nullable: true),
                    ParentPageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Importance = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "medium"),
                    FilePaths = table.Column<string[]>(type: "text[]", nullable: true),
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
                        name: "FK_wiki_pages_wikis_WikiId",
                        column: x => x.WikiId,
                        principalTable: "wikis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_embedding_documents_RepositoryId",
                table: "embedding_documents",
                column: "RepositoryId");

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
                name: "IX_repository_prompt_overrides_PromptTemplateId",
                table: "repository_prompt_overrides",
                column: "PromptTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_repository_prompt_overrides_RepositoryId_PromptTemplateId",
                table: "repository_prompt_overrides",
                columns: new[] { "RepositoryId", "PromptTemplateId" },
                unique: true);

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
                columns: new[] { "RepositoryId", "SourceBranch", "TaskType" },
                unique: true,
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "idx_one_running_task_per_repo_branch",
                table: "tasks",
                columns: new[] { "RepositoryId", "SourceBranch" },
                unique: true,
                filter: "status = 'running'");

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
                name: "IX_wikis_SourceRepositoryId_SourceBranch_Language",
                table: "wikis",
                columns: new[] { "SourceRepositoryId", "SourceBranch", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "embedding_documents");

            migrationBuilder.DropTable(
                name: "repository_prompt_overrides");

            migrationBuilder.DropTable(
                name: "system_settings");

            migrationBuilder.DropTable(
                name: "task_llm_call_logs");

            migrationBuilder.DropTable(
                name: "wiki_pages");

            migrationBuilder.DropTable(
                name: "prompt_templates");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "wikis");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "repositories");
        }
    }
}

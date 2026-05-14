using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddVersioningEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 注意：repositories 表的 provider_type、provider_repository_key、display_name、is_archived
            // 列已在上一迁移中使用 raw SQL 以 snake_case 名称创建，因此跳过重命名，只新增以下内容。

            migrationBuilder.AddColumn<Guid>(
                name: "WikiVersionId",
                table: "wiki_pages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "depth",
                table: "wiki_pages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "nav_title",
                table: "wiki_pages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outline_json",
                table: "wiki_pages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "page_type",
                table: "wiki_pages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "article");

            migrationBuilder.AddColumn<string>(
                name: "source_coverage_json",
                table: "wiki_pages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "wiki_pages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "ready");

            migrationBuilder.AddColumn<string>(
                name: "summary",
                table: "wiki_pages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "token_count",
                table: "wiki_pages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "config_hash",
                table: "tasks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "force_refresh",
                table: "tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "refresh_strategy",
                table: "tasks",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "resolved_repository_version_id",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "result_wiki_version_id",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_branch",
                table: "tasks",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

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
                name: "IX_wiki_pages_WikiVersionId",
                table: "wiki_pages",
                column: "WikiVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_resolved_repository_version_id",
                table: "tasks",
                column: "resolved_repository_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_result_wiki_version_id",
                table: "tasks",
                column: "result_wiki_version_id");

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

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_repository_versions_resolved_repository_version_id",
                table: "tasks",
                column: "resolved_repository_version_id",
                principalTable: "repository_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_wiki_versions_result_wiki_version_id",
                table: "tasks",
                column: "result_wiki_version_id",
                principalTable: "wiki_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_wiki_pages_wiki_versions_WikiVersionId",
                table: "wiki_pages",
                column: "WikiVersionId",
                principalTable: "wiki_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tasks_repository_versions_resolved_repository_version_id",
                table: "tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_tasks_wiki_versions_result_wiki_version_id",
                table: "tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_wiki_pages_wiki_versions_WikiVersionId",
                table: "wiki_pages");

            migrationBuilder.DropTable(
                name: "wiki_page_relations");

            migrationBuilder.DropTable(
                name: "wiki_versions");

            migrationBuilder.DropTable(
                name: "repository_versions");

            migrationBuilder.DropTable(
                name: "wiki_spaces");

            migrationBuilder.DropIndex(
                name: "IX_wiki_pages_WikiVersionId",
                table: "wiki_pages");

            migrationBuilder.DropIndex(
                name: "IX_tasks_resolved_repository_version_id",
                table: "tasks");

            migrationBuilder.DropIndex(
                name: "IX_tasks_result_wiki_version_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "WikiVersionId",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "depth",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "nav_title",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "outline_json",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "page_type",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "source_coverage_json",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "status",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "summary",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "token_count",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "config_hash",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "force_refresh",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "refresh_strategy",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "resolved_repository_version_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "result_wiki_version_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "target_branch",
                table: "tasks");
        }
    }
}

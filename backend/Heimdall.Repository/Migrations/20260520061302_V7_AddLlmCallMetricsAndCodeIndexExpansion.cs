using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class V7_AddLlmCallMetricsAndCodeIndexExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "code_index_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ModuleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FileType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Language = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ImportanceScore = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    exported_symbols = table.Column<string>(type: "text", nullable: false),
                    dependency_hints = table.Column<string>(type: "text", nullable: false),
                    CallGraphJson = table.Column<string>(type: "text", nullable: true),
                    DependencyEdgesJson = table.Column<string>(type: "text", nullable: true),
                    DesignPatternHints = table.Column<string>(type: "text", nullable: true),
                    RepositoryVersionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_index_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_code_index_entries_repository_versions_RepositoryVersionId",
                        column: x => x.RepositoryVersionId,
                        principalTable: "repository_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "llm_call_metrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CacheHitTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Success = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ErrorType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsEstimated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_call_metrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_llm_call_metrics_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "code_index_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    StartLine = table.Column<int>(type: "integer", nullable: false),
                    EndLine = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Embedding = table.Column<byte[]>(type: "bytea", nullable: true),
                    CodeIndexEntryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_index_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_code_index_chunks_code_index_entries_CodeIndexEntryId",
                        column: x => x.CodeIndexEntryId,
                        principalTable: "code_index_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_code_index_chunks_CodeIndexEntryId",
                table: "code_index_chunks",
                column: "CodeIndexEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_code_index_entries_ModuleName",
                table: "code_index_entries",
                column: "ModuleName");

            migrationBuilder.CreateIndex(
                name: "IX_code_index_entries_RepositoryVersionId",
                table: "code_index_entries",
                column: "RepositoryVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_code_index_entries_RepositoryVersionId_FilePath",
                table: "code_index_entries",
                columns: new[] { "RepositoryVersionId", "FilePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_llm_call_metrics_created",
                table: "llm_call_metrics",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "idx_llm_call_metrics_provider_model",
                table: "llm_call_metrics",
                columns: new[] { "Provider", "Model" });

            migrationBuilder.CreateIndex(
                name: "idx_llm_call_metrics_task",
                table: "llm_call_metrics",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "code_index_chunks");

            migrationBuilder.DropTable(
                name: "llm_call_metrics");

            migrationBuilder.DropTable(
                name: "code_index_entries");
        }
    }
}

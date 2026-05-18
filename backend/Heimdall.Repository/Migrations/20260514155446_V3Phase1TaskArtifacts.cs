using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class V3Phase1TaskArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                table: "tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "current_stage",
                table: "tasks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "queued");

            migrationBuilder.AddColumn<string>(
                name: "current_stage_status",
                table: "tasks",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.AddColumn<Guid>(
                name: "last_artifact_id",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_successful_stage",
                table: "tasks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "task_artifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    artifact_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    stage_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "completed"),
                    sequence = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    summary = table.Column<string>(type: "text", nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_artifacts_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_last_artifact_id",
                table: "tasks",
                column: "last_artifact_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_artifacts_task_stage_sequence",
                table: "task_artifacts",
                columns: new[] { "TaskId", "stage_name", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_task_artifacts_task_type_key",
                table: "task_artifacts",
                columns: new[] { "TaskId", "artifact_type", "artifact_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_task_artifacts_last_artifact_id",
                table: "tasks",
                column: "last_artifact_id",
                principalTable: "task_artifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tasks_task_artifacts_last_artifact_id",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "task_artifacts");

            migrationBuilder.DropIndex(
                name: "IX_tasks_last_artifact_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "current_stage",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "current_stage_status",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "last_artifact_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "last_successful_stage",
                table: "tasks");
        }
    }
}

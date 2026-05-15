using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class V4PromptManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "repository_prompt_overrides",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "repository_prompt_overrides",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Strategy",
                table: "repository_prompt_overrides",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "override");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "prompt_templates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "prompt_templates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "prompt_templates",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "prompt_template_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    TemplateContent = table.Column<string>(type: "text", nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_template_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prompt_template_history_prompt_templates_PromptTemplateId",
                        column: x => x.PromptTemplateId,
                        principalTable: "prompt_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prompt_templates_Slug",
                table: "prompt_templates",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompt_template_history_PromptTemplateId_Version",
                table: "prompt_template_history",
                columns: new[] { "PromptTemplateId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prompt_template_history");

            migrationBuilder.DropIndex(
                name: "IX_prompt_templates_Slug",
                table: "prompt_templates");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "repository_prompt_overrides");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "repository_prompt_overrides");

            migrationBuilder.DropColumn(
                name: "Strategy",
                table: "repository_prompt_overrides");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "prompt_templates");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "prompt_templates");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "prompt_templates");
        }
    }
}

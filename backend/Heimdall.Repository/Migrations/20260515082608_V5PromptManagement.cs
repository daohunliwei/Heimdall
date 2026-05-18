using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class V5PromptManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "ApplicableProviders",
                table: "prompt_templates",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "prompt_templates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "general");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "prompt_templates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SubCategory",
                table: "prompt_templates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicableProviders",
                table: "prompt_templates");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "prompt_templates");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "prompt_templates");

            migrationBuilder.DropColumn(
                name: "SubCategory",
                table: "prompt_templates");
        }
    }
}

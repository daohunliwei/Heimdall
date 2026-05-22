using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class V8_AddTaskResumeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "auto_resume_fail_count",
                table: "tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "resume_count",
                table: "tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "provider_model_metadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BillingType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MaxContextTokens = table.Column<int>(type: "integer", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    RateLimitPerMinute = table.Column<int>(type: "integer", nullable: true),
                    InputTokenPrice = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    OutputTokenPrice = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    CallPrice = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    SupportsCaching = table.Column<bool>(type: "boolean", nullable: false),
                    ContextFillRatio = table.Column<double>(type: "double precision", nullable: false),
                    ContextWarningThreshold = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_model_metadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_provider_model_metadata_ProviderKey_ModelName",
                table: "provider_model_metadata",
                columns: new[] { "ProviderKey", "ModelName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_model_metadata");

            migrationBuilder.DropColumn(
                name: "auto_resume_fail_count",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "resume_count",
                table: "tasks");
        }
    }
}

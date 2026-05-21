using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class V8_AddProviderModelMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provider_model_metadata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ModelName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BillingType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "TokenPlan"),
                    MaxContextTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 128000),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 8192),
                    RateLimitPerMinute = table.Column<int>(type: "integer", nullable: true),
                    InputTokenPrice = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    OutputTokenPrice = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    CallPrice = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    SupportsCaching = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ContextFillRatio = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.65),
                    ContextWarningThreshold = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.90),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_model_metadata", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provider_model_metadata_key_model",
                table: "provider_model_metadata",
                columns: new[] { "ProviderKey", "ModelName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_model_metadata");
        }
    }
}

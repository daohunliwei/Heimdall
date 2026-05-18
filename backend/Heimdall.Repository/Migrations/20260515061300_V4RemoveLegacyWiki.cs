using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class V4RemoveLegacyWiki : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_wiki_pages_wiki_versions_WikiVersionId",
                table: "wiki_pages");

            migrationBuilder.DropForeignKey(
                name: "FK_wiki_pages_wikis_WikiId",
                table: "wiki_pages");

            migrationBuilder.DropTable(
                name: "wikis");

            migrationBuilder.DropIndex(
                name: "IX_wiki_pages_WikiId",
                table: "wiki_pages");

            migrationBuilder.DropColumn(
                name: "WikiId",
                table: "wiki_pages");

            migrationBuilder.AlterColumn<Guid>(
                name: "WikiVersionId",
                table: "wiki_pages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_wiki_pages_wiki_versions_WikiVersionId",
                table: "wiki_pages",
                column: "WikiVersionId",
                principalTable: "wiki_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_wiki_pages_wiki_versions_WikiVersionId",
                table: "wiki_pages");

            migrationBuilder.AlterColumn<Guid>(
                name: "WikiVersionId",
                table: "wiki_pages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "WikiId",
                table: "wiki_pages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "wikis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "zh"),
                    SourceBranch = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, defaultValue: "main"),
                    Title = table.Column<string>(type: "text", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_wiki_pages_WikiId",
                table: "wiki_pages",
                column: "WikiId");

            migrationBuilder.CreateIndex(
                name: "IX_wikis_SourceRepositoryId_SourceBranch_Language",
                table: "wikis",
                columns: new[] { "SourceRepositoryId", "SourceBranch", "Language" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_wiki_pages_wiki_versions_WikiVersionId",
                table: "wiki_pages",
                column: "WikiVersionId",
                principalTable: "wiki_versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_wiki_pages_wikis_WikiId",
                table: "wiki_pages",
                column: "WikiId",
                principalTable: "wikis",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

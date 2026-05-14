using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddDualVectorTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "ix_code_embedding_chunks_version_file_chunk",
                table: "code_embedding_chunks",
                columns: new[] { "RepositoryVersionId", "file_path", "chunk_index" },
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "code_embedding_chunks");

            migrationBuilder.DropTable(
                name: "wiki_embedding_chunks");
        }
    }
}

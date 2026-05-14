using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heimdall.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryV2Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 使用 raw SQL 确保列名与现有命名规则一致（小写无引号）
            migrationBuilder.Sql(@"
                ALTER TABLE repositories
                ADD COLUMN IF NOT EXISTS provider_type character varying(32) NOT NULL DEFAULT 'github';

                ALTER TABLE repositories
                ADD COLUMN IF NOT EXISTS provider_repository_key character varying(256) NULL;

                ALTER TABLE repositories
                ADD COLUMN IF NOT EXISTS display_name character varying(512) NOT NULL DEFAULT '';

                ALTER TABLE repositories
                ADD COLUMN IF NOT EXISTS is_archived boolean NOT NULL DEFAULT false;
            ");

            // 唯一索引用于 (provider_type, provider_repository_key)
            // 使用部分索引使得 NULL provider_repository_key 可共存
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_repositories_ProviderType_ProviderRepositoryKey""
                ON repositories (provider_type, provider_repository_key)
                WHERE provider_repository_key IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_repositories_ProviderType_ProviderRepositoryKey"";
                ALTER TABLE repositories DROP COLUMN IF EXISTS provider_type;
                ALTER TABLE repositories DROP COLUMN IF EXISTS provider_repository_key;
                ALTER TABLE repositories DROP COLUMN IF EXISTS display_name;
                ALTER TABLE repositories DROP COLUMN IF EXISTS is_archived;
            ");
        }
    }
}

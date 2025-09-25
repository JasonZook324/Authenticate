using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authenticate.Migrations
{
    public partial class AddCredentialCookies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiCredentialCookies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApiCredentialId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EncryptedValue = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCredentialCookies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiCredentialCookies_ApiCredentials_ApiCredentialId",
                        column: x => x.ApiCredentialId,
                        principalTable: "ApiCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentialCookies_ApiCredentialId",
                table: "ApiCredentialCookies",
                column: "ApiCredentialId");

            // Backfill from legacy single-cookie columns if present
            migrationBuilder.Sql(@"
                INSERT INTO ""ApiCredentialCookies"" (""ApiCredentialId"", ""Name"", ""EncryptedValue"")
                SELECT ""Id"", ""CookieName"", ""EncryptedCookieValue""
                FROM ""ApiCredentials""
                WHERE ""CookieName"" IS NOT NULL AND ""EncryptedCookieValue"" IS NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiCredentialCookies");
        }
    }
}
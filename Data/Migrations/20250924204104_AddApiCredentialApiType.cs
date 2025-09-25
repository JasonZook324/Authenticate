using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authenticate.Migrations
{
    public partial class AddApiCredentialApiType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add as NULLABLE, no default
            migrationBuilder.AddColumn<int>(
                name: "ApiTypeId",
                table: "ApiCredentials",
                type: "integer",
                nullable: true);

            // 2) Seed a default API type if not present (e.g., 'General')
            migrationBuilder.Sql(@"
INSERT INTO ""APITypes"" (""ApiName"", ""Description"")
SELECT 'General', 'Default API type'
WHERE NOT EXISTS (SELECT 1 FROM ""APITypes"" WHERE ""ApiName"" = 'General');
");

            // 3) Backfill existing credentials to point to the seeded type
            migrationBuilder.Sql(@"
UPDATE ""ApiCredentials"" ac
SET ""ApiTypeId"" = at.""Id""
FROM (SELECT ""Id"" FROM ""APITypes"" WHERE ""ApiName"" = 'General' LIMIT 1) at
WHERE ac.""ApiTypeId"" IS NULL;
");

            // 4) Make non-nullable
            migrationBuilder.AlterColumn<int>(
                name: "ApiTypeId",
                table: "ApiCredentials",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // 5) Index + FK
            migrationBuilder.CreateIndex(
                name: "IX_ApiCredentials_ApiTypeId",
                table: "ApiCredentials",
                column: "ApiTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiCredentials_APITypes_ApiTypeId",
                table: "ApiCredentials",
                column: "ApiTypeId",
                principalTable: "APITypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiCredentials_APITypes_ApiTypeId",
                table: "ApiCredentials");

            migrationBuilder.DropIndex(
                name: "IX_ApiCredentials_ApiTypeId",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "ApiTypeId",
                table: "ApiCredentials");
        }
    }
}

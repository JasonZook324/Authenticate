using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authenticate.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthTypesToApiCredential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EncryptedApiKey",
                table: "ApiCredentials",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "AuthType",
                table: "ApiCredentials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CookieName",
                table: "ApiCredentials",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedCookieValue",
                table: "ApiCredentials",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedPassword",
                table: "ApiCredentials",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedUsername",
                table: "ApiCredentials",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenHeaderName",
                table: "ApiCredentials",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenScheme",
                table: "ApiCredentials",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthType",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "CookieName",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "EncryptedCookieValue",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "EncryptedPassword",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "EncryptedUsername",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "TokenHeaderName",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "TokenScheme",
                table: "ApiCredentials");

            migrationBuilder.AlterColumn<string>(
                name: "EncryptedApiKey",
                table: "ApiCredentials",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}

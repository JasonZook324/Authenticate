using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authenticate.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EspnLeagueId",
                table: "ApiCredentials",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EspnSeasonId",
                table: "ApiCredentials",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EspnLeagueId",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "EspnSeasonId",
                table: "ApiCredentials");
        }
    }
}

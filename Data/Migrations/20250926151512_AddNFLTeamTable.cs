using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Authenticate.Migrations
{
    /// <inheritdoc />
    public partial class AddNFLTeamTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NFLTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeamID = table.Column<string>(type: "text", nullable: false),
                    TeamAbv = table.Column<string>(type: "text", nullable: false),
                    TeamCity = table.Column<string>(type: "text", nullable: false),
                    TeamName = table.Column<string>(type: "text", nullable: false),
                    Division = table.Column<string>(type: "text", nullable: false),
                    ConferenceAbv = table.Column<string>(type: "text", nullable: false),
                    Conference = table.Column<string>(type: "text", nullable: false),
                    NflComLogo1 = table.Column<string>(type: "text", nullable: false),
                    EspnLogo1 = table.Column<string>(type: "text", nullable: false),
                    Loss = table.Column<string>(type: "text", nullable: false),
                    Tie = table.Column<string>(type: "text", nullable: false),
                    Wins = table.Column<string>(type: "text", nullable: false),
                    Pa = table.Column<string>(type: "text", nullable: false),
                    Pf = table.Column<string>(type: "text", nullable: false),
                    CurrentStreakJson = table.Column<string>(type: "text", nullable: false),
                    ByeWeeksJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NFLTeams", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NFLTeams");
        }
    }
}

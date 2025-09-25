using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authenticate.Migrations
{
    /// <inheritdoc />
    public partial class AddApiCredentialVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "ApiCredentials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAtUtc",
                table: "ApiCredentials",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "VerifiedAtUtc",
                table: "ApiCredentials");
        }
    }
}

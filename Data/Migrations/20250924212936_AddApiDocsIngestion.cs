using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Authenticate.Migrations
{
    /// <inheritdoc />
    public partial class AddApiDocsIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentationUrl",
                table: "APITypes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedUtc",
                table: "APITypes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApiTypeEndpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApiTypeId = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OperationId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Deprecated = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalDocsUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiTypeEndpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiTypeEndpoints_APITypes_ApiTypeId",
                        column: x => x.ApiTypeId,
                        principalTable: "APITypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiTypeEndpointParameters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EndpointId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    In = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Format = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SchemaJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiTypeEndpointParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiTypeEndpointParameters_ApiTypeEndpoints_EndpointId",
                        column: x => x.EndpointId,
                        principalTable: "ApiTypeEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiTypeEndpointResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EndpointId = table.Column<int>(type: "integer", nullable: false),
                    StatusCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Format = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SchemaJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiTypeEndpointResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiTypeEndpointResponses_ApiTypeEndpoints_EndpointId",
                        column: x => x.EndpointId,
                        principalTable: "ApiTypeEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiTypeEndpointParameters_EndpointId",
                table: "ApiTypeEndpointParameters",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiTypeEndpointResponses_EndpointId",
                table: "ApiTypeEndpointResponses",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiTypeEndpoints_ApiTypeId",
                table: "ApiTypeEndpoints",
                column: "ApiTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiTypeEndpointParameters");

            migrationBuilder.DropTable(
                name: "ApiTypeEndpointResponses");

            migrationBuilder.DropTable(
                name: "ApiTypeEndpoints");

            migrationBuilder.DropColumn(
                name: "DocumentationUrl",
                table: "APITypes");

            migrationBuilder.DropColumn(
                name: "LastSyncedUtc",
                table: "APITypes");
        }
    }
}

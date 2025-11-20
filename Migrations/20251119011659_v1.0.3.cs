using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByProxy.Migrations
{
    /// <inheritdoc />
    public partial class v103 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfigRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    RouteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RouteConfigRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Values = table.Column<string>(type: "TEXT", nullable: true),
                    CaseSensitiveValues = table.Column<bool>(type: "INTEGER", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 21, nullable: false),
                    HeaderMatchMode = table.Column<string>(type: "TEXT", nullable: true),
                    QueryMatchMode = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchCriteria", x => new { x.Id, x.ConfigRevision });
                    table.ForeignKey(
                        name: "FK_MatchCriteria_Configurations_ConfigRevision",
                        column: x => x.ConfigRevision,
                        principalTable: "Configurations",
                        principalColumn: "Revision",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchCriteria_Routes_RouteId_RouteConfigRevision",
                        columns: x => new { x.RouteId, x.RouteConfigRevision },
                        principalTable: "Routes",
                        principalColumns: new[] { "Id", "ConfigRevision" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchCriteria_ConfigRevision",
                table: "MatchCriteria",
                column: "ConfigRevision");

            migrationBuilder.CreateIndex(
                name: "IX_MatchCriteria_RouteId_RouteConfigRevision",
                table: "MatchCriteria",
                columns: new[] { "RouteId", "RouteConfigRevision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchCriteria");
        }
    }
}

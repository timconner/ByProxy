using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByProxy.Migrations {
    /// <inheritdoc />
    public partial class v1240 : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.RenameTable(
                name: "RouteTransforms",
                newName: "RequestTransforms");

            migrationBuilder.RenameColumn(
                name: "AllowedHeaders",
                table: "Routes",
                newName: "AllowedRequestHeaders");
            
            migrationBuilder.AddColumn<string>(
                name: "StaticResponseHeaders",
                table: "Routes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedResponseHeaders",
                table: "Routes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreserveResponseHeaders",
                table: "Routes",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "StaticResponseBody",
                table: "Routes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResponseTransforms",
                columns: table => new {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfigRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    RouteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RouteConfigRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 34, nullable: false),
                    HeaderOperation = table.Column<string>(type: "TEXT", nullable: true),
                    HeaderName = table.Column<string>(type: "TEXT", nullable: true),
                    HeaderValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ResponseTransforms", x => new { x.Id, x.ConfigRevision });
                    table.ForeignKey(
                        name: "FK_ResponseTransforms_Configurations_ConfigRevision",
                        column: x => x.ConfigRevision,
                        principalTable: "Configurations",
                        principalColumn: "Revision",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResponseTransforms_Routes_RouteId_RouteConfigRevision",
                        columns: x => new { x.RouteId, x.RouteConfigRevision },
                        principalTable: "Routes",
                        principalColumns: new[] { "Id", "ConfigRevision" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.DropIndex(
                name: "IX_RouteTransforms_ConfigRevision",
                table: "RequestTransforms");

            migrationBuilder.DropIndex(
                name: "IX_RouteTransforms_RouteId_RouteConfigRevision",
                table: "RequestTransforms");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTransforms_ConfigRevision",
                table: "RequestTransforms",
                column: "ConfigRevision");

            migrationBuilder.CreateIndex(
                name: "IX_RequestTransforms_RouteId_RouteConfigRevision",
                table: "RequestTransforms",
                columns: new[] { "RouteId", "RouteConfigRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_ResponseTransforms_ConfigRevision",
                table: "ResponseTransforms",
                column: "ConfigRevision");

            migrationBuilder.CreateIndex(
                name: "IX_ResponseTransforms_RouteId_RouteConfigRevision",
                table: "ResponseTransforms",
                columns: new[] { "RouteId", "RouteConfigRevision" });

            migrationBuilder.Sql("UPDATE \"Routes\" SET \"ResponseType\" = 'Static' WHERE \"ResponseType\" = 'Status';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("UPDATE \"Routes\" SET \"ResponseType\" = 'Status' WHERE \"ResponseType\" = 'Static';");

            migrationBuilder.RenameTable(
                name: "RequestTransforms",
                newName: "RouteTransforms");

            migrationBuilder.DropIndex(
                name: "IX_RequestTransforms_ConfigRevision",
                table: "RouteTransforms");

            migrationBuilder.DropIndex(
                name: "IX_RequestTransforms_RouteId_RouteConfigRevision",
                table: "RouteTransforms");

            migrationBuilder.DropTable(
                name: "ResponseTransforms");

            migrationBuilder.DropColumn(
                name: "StaticResponseHeaders",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "AllowedResponseHeaders",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "PreserveResponseHeaders",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "StaticResponseBody",
                table: "Routes");

            migrationBuilder.RenameColumn(
                name: "AllowedRequestHeaders",
                table: "Routes",
                newName: "AllowedHeaders");

            migrationBuilder.CreateIndex(
                name: "IX_RouteTransforms_ConfigRevision",
                table: "RouteTransforms",
                column: "ConfigRevision");

            migrationBuilder.CreateIndex(
                name: "IX_RouteTransforms_RouteId_RouteConfigRevision",
                table: "RouteTransforms",
                columns: new[] { "RouteId", "RouteConfigRevision" });
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByProxy.Migrations {
    /// <inheritdoc />
    public partial class v1250 : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.RenameColumn(
                name: "HttpStatusCode",
                table: "Routes",
                newName: "StaticResponse_HttpStatusCode");

            migrationBuilder.RenameColumn(
                name: "StaticResponseHeaders",
                table: "Routes",
                newName: "StaticResponse_Headers");

            migrationBuilder.RenameColumn(
                name: "StaticResponseBody",
                table: "Routes",
                newName: "StaticResponse_Body");

            migrationBuilder.RenameColumn(
                name: "UnmatchedStatus",
                table: "Configurations",
                newName: "UnmatchedResponse_HttpStatusCode");

            migrationBuilder.RenameColumn(
                name: "Tarpit",
                table: "Configurations",
                newName: "UnmatchedResponse_UseTarpit");

            migrationBuilder.AddColumn<string>(
                name: "IpRestrictionMode",
                table: "Routes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpDenyResponse_Body",
                table: "Routes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpDenyResponse_Headers",
                table: "Routes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IpDenyResponse_HttpStatusCode",
                table: "Routes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IpDenyResponse_UseTarpit",
                table: "Routes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpRestrictionEntries",
                table: "Routes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StaticResponse_UseTarpit",
                table: "Routes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrustedProxies",
                table: "Configurations",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "TrustedProxyChainLimit",
                table: "Configurations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UnmatchedResponse_Body",
                table: "Configurations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnmatchedResponse_Headers",
                table: "Configurations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Routes"
                SET "StaticResponse_Headers" =
                    '{"Location":"' ||
                    REPLACE(REPLACE("RedirectLocation", '\', '\\'), '"', '\"') ||
                    '"}'
                WHERE "ResponseType" = 'Redirect';

                UPDATE "Routes"
                SET "StaticResponse_UseTarpit" = CASE
                    WHEN "ResponseType" = 'Tarpit' THEN 1
                    ELSE 0
                END
                WHERE "ResponseType" IN ('Redirect', 'Static', 'Tarpit');
                """);

            migrationBuilder.DropColumn(
                name: "RedirectLocation",
                table: "Routes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "RedirectLocation",
                table: "Routes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Routes"
                SET "RedirectLocation" =
                    REPLACE(REPLACE(
                        substr("StaticResponse_Headers", 14, length("StaticResponse_Headers") - 15),
                        '\"', '"'),
                        '\\', '\')
                WHERE "ResponseType" = 'Redirect';
                """);

            migrationBuilder.DropColumn(
                name: "IpRestrictionMode",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "IpDenyResponse_Body",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "IpDenyResponse_Headers",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "IpDenyResponse_HttpStatusCode",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "IpDenyResponse_UseTarpit",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "IpRestrictionEntries",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "StaticResponse_UseTarpit",
                table: "Routes");

            migrationBuilder.RenameColumn(
                name: "StaticResponse_HttpStatusCode",
                table: "Routes",
                newName: "HttpStatusCode");

            migrationBuilder.RenameColumn(
                name: "StaticResponse_Headers",
                table: "Routes",
                newName: "StaticResponseHeaders");

            migrationBuilder.RenameColumn(
                name: "StaticResponse_Body",
                table: "Routes",
                newName: "StaticResponseBody");

            migrationBuilder.DropColumn(
                name: "TrustedProxies",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "TrustedProxyChainLimit",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "UnmatchedResponse_Body",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "UnmatchedResponse_Headers",
                table: "Configurations");

            migrationBuilder.RenameColumn(
                name: "UnmatchedResponse_HttpStatusCode",
                table: "Configurations",
                newName: "UnmatchedStatus");

            migrationBuilder.RenameColumn(
                name: "UnmatchedResponse_UseTarpit",
                table: "Configurations",
                newName: "Tarpit");
        }
    }
}
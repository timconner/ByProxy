using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ByProxy.Migrations
{
    /// <inheritdoc />
    public partial class v1101 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MaxRequestBodySize",
                table: "Routes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaxRequestBodySize",
                table: "Configurations",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxRequestBodySize",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "MaxRequestBodySize",
                table: "Configurations");
        }
    }
}

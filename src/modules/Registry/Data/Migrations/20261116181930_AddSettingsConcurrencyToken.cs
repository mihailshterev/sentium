using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Registry.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "registry",
                table: "SystemSettings",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "registry",
                table: "SystemSettings");
        }
    }
}

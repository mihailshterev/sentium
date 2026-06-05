using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Registry.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "SystemSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_UserId",
                table: "SystemSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SystemSettings_UserId",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SystemSettings");
        }
    }
}

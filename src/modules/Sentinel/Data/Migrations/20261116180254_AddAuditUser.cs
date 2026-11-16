using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Sentinel.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "sentinel",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                schema: "sentinel",
                table: "AuditLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_UserId",
                schema: "sentinel",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "sentinel",
                table: "AuditLogs");
        }
    }
}

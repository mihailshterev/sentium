using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionLogOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "praxis",
                table: "ExecutionLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionLogs_UserId_ExecutedAt",
                schema: "praxis",
                table: "ExecutionLogs",
                columns: new[] { "UserId", "ExecutedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExecutionLogs_UserId_ExecutedAt",
                schema: "praxis",
                table: "ExecutionLogs");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "praxis",
                table: "ExecutionLogs");
        }
    }
}

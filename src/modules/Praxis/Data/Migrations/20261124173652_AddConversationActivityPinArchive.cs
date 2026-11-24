using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationActivityPinArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "praxis",
                table: "Conversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                schema: "praxis",
                table: "Conversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                schema: "praxis",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UserId_IsArchived_LastActivityAt",
                schema: "praxis",
                table: "Conversations",
                columns: new[] { "UserId", "IsArchived", "LastActivityAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_UserId_IsArchived_LastActivityAt",
                schema: "praxis",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                schema: "praxis",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                schema: "praxis",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                schema: "praxis",
                table: "Conversations");
        }
    }
}

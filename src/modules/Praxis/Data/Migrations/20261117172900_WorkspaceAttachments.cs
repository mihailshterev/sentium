using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessingError",
                schema: "praxis",
                table: "ProjectFiles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Attachments",
                schema: "praxis",
                table: "Messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceId",
                schema: "praxis",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_WorkspaceId",
                schema: "praxis",
                table: "Conversations",
                column: "WorkspaceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Workspaces_WorkspaceId",
                schema: "praxis",
                table: "Conversations",
                column: "WorkspaceId",
                principalSchema: "praxis",
                principalTable: "Workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Workspaces_WorkspaceId",
                schema: "praxis",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_WorkspaceId",
                schema: "praxis",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ProcessingError",
                schema: "praxis",
                table: "ProjectFiles");

            migrationBuilder.DropColumn(
                name: "Attachments",
                schema: "praxis",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "WorkspaceId",
                schema: "praxis",
                table: "Conversations");
        }
    }
}

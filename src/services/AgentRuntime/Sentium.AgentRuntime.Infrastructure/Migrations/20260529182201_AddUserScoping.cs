using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.AgentRuntime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Workspaces_Name",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_Name",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_Title",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_AgentSkills_Name",
                table: "AgentSkills");

            migrationBuilder.DropIndex(
                name: "IX_Agents_Name",
                table: "Agents");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Workspaces",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Workflows",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "WorkflowRuns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ProjectFiles",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "AgentSkills",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Agents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_UserId_Name",
                table: "Workspaces",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_UserId_Name",
                table: "Workflows",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_UserId",
                table: "WorkflowRuns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_UserId",
                table: "ProjectFiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UserId_Title",
                table: "Conversations",
                columns: new[] { "UserId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentSkills_UserId_Name",
                table: "AgentSkills",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agents_UserId_Name",
                table: "Agents",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Workspaces_UserId_Name",
                table: "Workspaces");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_UserId_Name",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowRuns_UserId",
                table: "WorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_ProjectFiles_UserId",
                table: "ProjectFiles");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_UserId_Title",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_AgentSkills_UserId_Name",
                table: "AgentSkills");

            migrationBuilder.DropIndex(
                name: "IX_Agents_UserId_Name",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Workspaces");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ProjectFiles");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AgentSkills");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Agents");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_Name",
                table: "Workspaces",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_Name",
                table: "Workflows",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Title",
                table: "Conversations",
                column: "Title",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentSkills_Name",
                table: "AgentSkills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agents_Name",
                table: "Agents",
                column: "Name",
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.AgentRuntime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserScopingToLearningsAndWorkflowRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "WorkflowRuns",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            // System-triggered runs (NATS) had Guid.Empty as a placeholder; convert to NULL so
            // they are correctly treated as unowned (Sovereign-only visibility).
            migrationBuilder.Sql(
                "UPDATE [WorkflowRuns] SET [UserId] = NULL WHERE [UserId] = '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowId",
                table: "WorkflowRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "AgentLearnings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_WorkflowId",
                table: "WorkflowRuns",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_UserId",
                table: "AgentLearnings",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowRuns_Workflows_WorkflowId",
                table: "WorkflowRuns",
                column: "WorkflowId",
                principalTable: "Workflows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowRuns_Workflows_WorkflowId",
                table: "WorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowRuns_WorkflowId",
                table: "WorkflowRuns");

            migrationBuilder.DropIndex(
                name: "IX_AgentLearnings_UserId",
                table: "AgentLearnings");

            migrationBuilder.DropColumn(
                name: "WorkflowId",
                table: "WorkflowRuns");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AgentLearnings");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "WorkflowRuns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}

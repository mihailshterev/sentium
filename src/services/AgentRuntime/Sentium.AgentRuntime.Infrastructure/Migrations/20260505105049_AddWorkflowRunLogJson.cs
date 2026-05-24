using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.AgentRuntime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowRunLogJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogJson",
                table: "WorkflowRuns",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogJson",
                table: "WorkflowRuns");
        }
    }
}

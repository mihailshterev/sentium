using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.AgentRuntime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsGlobalToAgentLearnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGlobal",
                table: "AgentLearnings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Preserve visibility of pre-existing global learnings. Under the previous model a NULL
            // UserId meant "global" (visible to everyone). The new query filter keys visibility off
            // IsGlobal, so mark those legacy rows as global to keep them shared.
            migrationBuilder.Sql("UPDATE [AgentLearnings] SET [IsGlobal] = 1 WHERE [UserId] IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_IsGlobal",
                table: "AgentLearnings",
                column: "IsGlobal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentLearnings_IsGlobal",
                table: "AgentLearnings");

            migrationBuilder.DropColumn(
                name: "IsGlobal",
                table: "AgentLearnings");
        }
    }
}

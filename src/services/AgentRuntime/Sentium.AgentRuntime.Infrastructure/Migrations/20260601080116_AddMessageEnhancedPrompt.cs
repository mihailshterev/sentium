using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.AgentRuntime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageEnhancedPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnhancedPrompt",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnhancedPrompt",
                table: "Messages");
        }
    }
}

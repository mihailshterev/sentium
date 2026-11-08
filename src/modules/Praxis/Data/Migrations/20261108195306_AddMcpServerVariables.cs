using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpServerVariables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Variables",
                schema: "praxis",
                table: "McpServers",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Variables",
                schema: "praxis",
                table: "McpServers");
        }
    }
}

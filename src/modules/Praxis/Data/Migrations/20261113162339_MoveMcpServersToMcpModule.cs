using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <summary>
    /// MCP servers moved out of Praxis into the <c>Sentium.Mcp</c> module (schema <c>mcp</c>). This migration only records
    /// that <c>McpServer</c> left the Praxis model; it deliberately leaves the data alone. The table is moved - not
    /// dropped - by the Mcp module's first migration, which runs after Praxis. Roll the Mcp context back to <c>0</c>
    /// (which moves the table back into <c>praxis</c>) before rolling Praxis back past <c>AddMcpServers</c>.
    /// </summary>
    public partial class MoveMcpServersToMcpModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentToolsAndMessageSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Steps",
                schema: "praxis",
                table: "Messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "Tools",
                schema: "praxis",
                table: "Agents",
                type: "text[]",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgentToolOverrides",
                schema: "praxis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Tools = table.Column<List<string>>(type: "text[]", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentToolOverrides", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentToolOverrides_UserId_AgentName",
                schema: "praxis",
                table: "AgentToolOverrides",
                columns: new[] { "UserId", "AgentName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentToolOverrides",
                schema: "praxis");

            migrationBuilder.DropColumn(
                name: "Steps",
                schema: "praxis",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Tools",
                schema: "praxis",
                table: "Agents");
        }
    }
}

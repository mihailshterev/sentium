using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.AgentRuntime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettingsAndAgentLearnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentLearnings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsIngested = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentLearnings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserHarnessPrompt = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    IsBuiltInHarnessEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_AgentName",
                table: "AgentLearnings",
                column: "AgentName");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_CapturedAt",
                table: "AgentLearnings",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_IsIngested",
                table: "AgentLearnings",
                column: "IsIngested");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentLearnings");

            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}

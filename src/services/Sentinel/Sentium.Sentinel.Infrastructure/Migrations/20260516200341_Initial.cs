using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Sentinel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AgentId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SkillName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UserPromptHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Allowed = table.Column<bool>(type: "bit", nullable: false),
                    Effect = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Risk = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TriggeredPoliciesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvaluationDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    AlignmentVerdict = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_AgentId",
                table: "AuditLogs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}

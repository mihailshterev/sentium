using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Sandbox.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExecutionLogs",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AgentId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalUserPrompt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ExitCode = table.Column<long>(type: "bigint", nullable: false),
                    Output = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimedOut = table.Column<bool>(type: "bit", nullable: false),
                    PolicyDenied = table.Column<bool>(type: "bit", nullable: false),
                    PolicyDenialReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentinelAuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Artifacts = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileContext = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionLogs", x => x.JobId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionLogs");
        }
    }
}

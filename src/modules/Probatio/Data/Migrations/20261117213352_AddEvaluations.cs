using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Probatio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Evaluations",
                schema: "probatio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuiteName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SuiteVersion = table.Column<int>(type: "integer", nullable: false),
                    AgentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Repetitions = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CaseCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedCaseCount = table.Column<int>(type: "integer", nullable: false),
                    PassedCaseCount = table.Column<int>(type: "integer", nullable: false),
                    Cases = table.Column<string>(type: "jsonb", nullable: false),
                    Agent = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationCaseResults",
                schema: "probatio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    PassedRepetitions = table.Column<int>(type: "integer", nullable: false),
                    Repetitions = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationCaseResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationCaseResults_Evaluations_EvaluationId",
                        column: x => x.EvaluationId,
                        principalSchema: "probatio",
                        principalTable: "Evaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCaseResults_EvaluationId_CaseId",
                schema: "probatio",
                table: "EvaluationCaseResults",
                columns: new[] { "EvaluationId", "CaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_Status",
                schema: "probatio",
                table: "Evaluations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_UserId_CreatedAt",
                schema: "probatio",
                table: "Evaluations",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_UserId_SuiteId",
                schema: "probatio",
                table: "Evaluations",
                columns: new[] { "UserId", "SuiteId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluationCaseResults",
                schema: "probatio");

            migrationBuilder.DropTable(
                name: "Evaluations",
                schema: "probatio");
        }
    }
}

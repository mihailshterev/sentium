using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Probatio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBenchmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BenchmarkId",
                schema: "probatio",
                table: "Evaluations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Benchmarks",
                schema: "probatio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuiteName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SuiteVersion = table.Column<int>(type: "integer", nullable: false),
                    AgentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Repetitions = table.Column<int>(type: "integer", nullable: false),
                    JudgeModel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Benchmarks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Evaluations_BenchmarkId",
                schema: "probatio",
                table: "Evaluations",
                column: "BenchmarkId");

            migrationBuilder.CreateIndex(
                name: "IX_Benchmarks_UserId_CreatedAt",
                schema: "probatio",
                table: "Benchmarks",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Evaluations_Benchmarks_BenchmarkId",
                schema: "probatio",
                table: "Evaluations",
                column: "BenchmarkId",
                principalSchema: "probatio",
                principalTable: "Benchmarks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Evaluations_Benchmarks_BenchmarkId",
                schema: "probatio",
                table: "Evaluations");

            migrationBuilder.DropTable(
                name: "Benchmarks",
                schema: "probatio");

            migrationBuilder.DropIndex(
                name: "IX_Evaluations_BenchmarkId",
                schema: "probatio",
                table: "Evaluations");

            migrationBuilder.DropColumn(
                name: "BenchmarkId",
                schema: "probatio",
                table: "Evaluations");
        }
    }
}

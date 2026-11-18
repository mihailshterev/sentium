using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Probatio.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationJudge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JudgeModel",
                schema: "probatio",
                table: "Evaluations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JudgeModel",
                schema: "probatio",
                table: "Evaluations");
        }
    }
}

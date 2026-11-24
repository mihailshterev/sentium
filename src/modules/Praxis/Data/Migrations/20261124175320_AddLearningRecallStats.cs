using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningRecallStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastRecalledAt",
                schema: "praxis",
                table: "AgentLearnings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecallCount",
                schema: "praxis",
                table: "AgentLearnings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRecalledAt",
                schema: "praxis",
                table: "AgentLearnings");

            migrationBuilder.DropColumn(
                name: "RecallCount",
                schema: "praxis",
                table: "AgentLearnings");
        }
    }
}

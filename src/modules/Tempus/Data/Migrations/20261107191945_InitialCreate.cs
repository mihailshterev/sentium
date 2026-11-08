using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Tempus.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tempus");

            migrationBuilder.CreateTable(
                name: "TimelineTasks",
                schema: "tempus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    AssignedAgent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Origin = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedByAgent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineTasks", x => x.Id);
                    table.CheckConstraint("CK_TimelineTasks_DateInMonth", "\"Date\" IS NULL OR (EXTRACT(YEAR FROM \"Date\") = \"Year\" AND EXTRACT(MONTH FROM \"Date\") = \"Month\")");
                    table.CheckConstraint("CK_TimelineTasks_Month", "\"Month\" BETWEEN 1 AND 12");
                });

            migrationBuilder.CreateTable(
                name: "TimelineNotes",
                schema: "tempus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Author = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineNotes", x => x.Id);
                    table.CheckConstraint("CK_TimelineNotes_DateInMonth", "\"Date\" IS NULL OR (EXTRACT(YEAR FROM \"Date\") = \"Year\" AND EXTRACT(MONTH FROM \"Date\") = \"Month\")");
                    table.CheckConstraint("CK_TimelineNotes_Month", "\"Month\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_TimelineNotes_TimelineTasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "tempus",
                        principalTable: "TimelineTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimelineProposals",
                schema: "tempus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    AssignedAgent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    FromYear = table.Column<int>(type: "integer", nullable: true),
                    FromMonth = table.Column<int>(type: "integer", nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Rationale = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProposedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineProposals", x => x.Id);
                    table.CheckConstraint("CK_TimelineProposals_DateInMonth", "\"Date\" IS NULL OR (EXTRACT(YEAR FROM \"Date\") = \"Year\" AND EXTRACT(MONTH FROM \"Date\") = \"Month\")");
                    table.CheckConstraint("CK_TimelineProposals_Month", "\"Month\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_TimelineProposals_TimelineTasks_TaskId",
                        column: x => x.TaskId,
                        principalSchema: "tempus",
                        principalTable: "TimelineTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimelineNotes_TaskId",
                schema: "tempus",
                table: "TimelineNotes",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TimelineNotes_UserId_Year_Month",
                schema: "tempus",
                table: "TimelineNotes",
                columns: new[] { "UserId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_TimelineProposals_TaskId",
                schema: "tempus",
                table: "TimelineProposals",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TimelineProposals_UserId_Status",
                schema: "tempus",
                table: "TimelineProposals",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TimelineProposals_UserId_Year_Month",
                schema: "tempus",
                table: "TimelineProposals",
                columns: new[] { "UserId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_TimelineTasks_Tags",
                schema: "tempus",
                table: "TimelineTasks",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_TimelineTasks_UserId_Date",
                schema: "tempus",
                table: "TimelineTasks",
                columns: new[] { "UserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_TimelineTasks_UserId_Year_Month",
                schema: "tempus",
                table: "TimelineTasks",
                columns: new[] { "UserId", "Year", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimelineNotes",
                schema: "tempus");

            migrationBuilder.DropTable(
                name: "TimelineProposals",
                schema: "tempus");

            migrationBuilder.DropTable(
                name: "TimelineTasks",
                schema: "tempus");
        }
    }
}

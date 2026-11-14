using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Tempus.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEntriesSeriesAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                schema: "tempus",
                table: "TimelineTasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDetached",
                schema: "tempus",
                table: "TimelineTasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "tempus",
                table: "TimelineTasks",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Task");

            migrationBuilder.AddColumn<DateOnly>(
                name: "OccurrenceDate",
                schema: "tempus",
                table: "TimelineTasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderDueAt",
                schema: "tempus",
                table: "TimelineTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderOffsetMinutes",
                schema: "tempus",
                table: "TimelineTasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReminderSentAt",
                schema: "tempus",
                table: "TimelineTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                schema: "tempus",
                table: "TimelineTasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                schema: "tempus",
                table: "TimelineTasks",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                schema: "tempus",
                table: "TimelineProposals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                schema: "tempus",
                table: "TimelineProposals",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TimelineSeries",
                schema: "tempus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AssignedAgent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DueOffsetDays = table.Column<int>(type: "integer", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    ReminderOffsetMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    Weekdays = table.Column<int[]>(type: "integer[]", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Until = table.Column<DateOnly>(type: "date", nullable: true),
                    ExcludedDates = table.Column<List<DateOnly>>(type: "date[]", nullable: false),
                    MaterializedThrough = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineSeries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimelineTasks_ReminderDueAt",
                schema: "tempus",
                table: "TimelineTasks",
                column: "ReminderDueAt",
                filter: "\"ReminderSentAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimelineTasks_SeriesId_OccurrenceDate",
                schema: "tempus",
                table: "TimelineTasks",
                columns: new[] { "SeriesId", "OccurrenceDate" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimelineTasks_EndDate",
                schema: "tempus",
                table: "TimelineTasks",
                sql: "\"EndDate\" IS NULL OR (\"Date\" IS NOT NULL AND \"EndDate\" >= \"Date\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimelineTasks_StartTime",
                schema: "tempus",
                table: "TimelineTasks",
                sql: "\"StartTime\" IS NULL OR \"Date\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimelineSeries_UserId",
                schema: "tempus",
                table: "TimelineSeries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TimelineTasks_TimelineSeries_SeriesId",
                schema: "tempus",
                table: "TimelineTasks",
                column: "SeriesId",
                principalSchema: "tempus",
                principalTable: "TimelineSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimelineTasks_TimelineSeries_SeriesId",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropTable(
                name: "TimelineSeries",
                schema: "tempus");

            migrationBuilder.DropIndex(
                name: "IX_TimelineTasks_ReminderDueAt",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropIndex(
                name: "IX_TimelineTasks_SeriesId_OccurrenceDate",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TimelineTasks_EndDate",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TimelineTasks_StartTime",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropColumn(
                name: "EndDate",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropColumn(
                name: "IsDetached",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropColumn(
                name: "OccurrenceDate",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropColumn(
                name: "ReminderDueAt",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropColumn(
                name: "ReminderOffsetMinutes",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropColumn(
                name: "StartTime",
                schema: "tempus",
                table: "TimelineTasks");

            migrationBuilder.DropColumn(
                name: "EndDate",
                schema: "tempus",
                table: "TimelineProposals");

            migrationBuilder.DropColumn(
                name: "StartTime",
                schema: "tempus",
                table: "TimelineProposals");
        }
    }
}

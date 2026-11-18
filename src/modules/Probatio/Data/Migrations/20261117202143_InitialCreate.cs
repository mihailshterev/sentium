using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Probatio.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "probatio");

            migrationBuilder.CreateTable(
                name: "EvalSuites",
                schema: "probatio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalSuites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvalSuiteVersions",
                schema: "probatio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CaseCount = table.Column<int>(type: "integer", nullable: false),
                    Cases = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvalSuiteVersions", x => x.Id);
                    table.CheckConstraint("CK_EvalSuiteVersions_Version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_EvalSuiteVersions_EvalSuites_SuiteId",
                        column: x => x.SuiteId,
                        principalSchema: "probatio",
                        principalTable: "EvalSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvalSuites_UserId_UpdatedAt",
                schema: "probatio",
                table: "EvalSuites",
                columns: new[] { "UserId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EvalSuiteVersions_SuiteId_Version",
                schema: "probatio",
                table: "EvalSuiteVersions",
                columns: new[] { "SuiteId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvalSuiteVersions_UserId",
                schema: "probatio",
                table: "EvalSuiteVersions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvalSuiteVersions",
                schema: "probatio");

            migrationBuilder.DropTable(
                name: "EvalSuites",
                schema: "probatio");
        }
    }
}

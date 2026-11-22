using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingApprovals",
                schema: "praxis",
                columns: table => new
                {
                    RequestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingApprovals", x => x.RequestId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingApprovals_UserId",
                schema: "praxis",
                table: "PendingApprovals",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingApprovals",
                schema: "praxis");
        }
    }
}

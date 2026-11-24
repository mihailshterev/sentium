using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMemories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMemories",
                schema: "praxis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMemories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMemories_UserId_UpdatedAt",
                schema: "praxis",
                table: "UserMemories",
                columns: new[] { "UserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMemories",
                schema: "praxis");
        }
    }
}

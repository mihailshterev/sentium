using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.AgentRuntime.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    BlobName = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessingStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectFiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_CreatedAt",
                table: "ProjectFiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_WorkspaceId",
                table: "ProjectFiles",
                column: "WorkspaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectFiles");
        }
    }
}

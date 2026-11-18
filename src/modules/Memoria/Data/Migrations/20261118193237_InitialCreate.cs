using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Memoria.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "memoria");

            migrationBuilder.CreateTable(
                name: "IndexEntries",
                schema: "memoria",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexEntries", x => x.NoteId);
                });

            migrationBuilder.CreateTable(
                name: "Vaults",
                schema: "memoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSyncAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vaults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                schema: "memoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VaultId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PathKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    Aliases = table.Column<List<string>>(type: "text[]", nullable: false),
                    AliasKeys = table.Column<List<string>>(type: "text[]", nullable: false),
                    FrontmatterJson = table.Column<string>(type: "jsonb", nullable: true),
                    WordCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IndexedHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IndexedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notes_Vaults_VaultId",
                        column: x => x.VaultId,
                        principalSchema: "memoria",
                        principalTable: "Vaults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Links",
                schema: "memoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Target = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    TargetKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Heading = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsEmbed = table.Column<bool>(type: "boolean", nullable: false),
                    TargetNoteId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Links_Notes_SourceNoteId",
                        column: x => x.SourceNoteId,
                        principalSchema: "memoria",
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Links_Notes_TargetNoteId",
                        column: x => x.TargetNoteId,
                        principalSchema: "memoria",
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndexEntries_Status_NextAttemptAt",
                schema: "memoria",
                table: "IndexEntries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IndexEntries_UserId",
                schema: "memoria",
                table: "IndexEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Links_SourceNoteId",
                schema: "memoria",
                table: "Links",
                column: "SourceNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Links_TargetNoteId",
                schema: "memoria",
                table: "Links",
                column: "TargetNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Links_UserId_TargetKey",
                schema: "memoria",
                table: "Links",
                columns: new[] { "UserId", "TargetKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_AliasKeys",
                schema: "memoria",
                table: "Notes",
                column: "AliasKeys")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_Tags",
                schema: "memoria",
                table: "Notes",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_UserId_PathKey",
                schema: "memoria",
                table: "Notes",
                columns: new[] { "UserId", "PathKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_VaultId_Path",
                schema: "memoria",
                table: "Notes",
                columns: new[] { "VaultId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vaults_UserId",
                schema: "memoria",
                table: "Vaults",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndexEntries",
                schema: "memoria");

            migrationBuilder.DropTable(
                name: "Links",
                schema: "memoria");

            migrationBuilder.DropTable(
                name: "Notes",
                schema: "memoria");

            migrationBuilder.DropTable(
                name: "Vaults",
                schema: "memoria");
        }
    }
}

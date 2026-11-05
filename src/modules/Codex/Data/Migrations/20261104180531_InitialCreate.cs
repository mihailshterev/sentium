using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Sentium.Codex.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "codex");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                schema: "codex",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Collection = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(768)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_chunks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_Collection_CreatedAt",
                schema: "codex",
                table: "knowledge_chunks",
                columns: new[] { "Collection", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_Collection_Source",
                schema: "codex",
                table: "knowledge_chunks",
                columns: new[] { "Collection", "Source" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_Collection_UserId",
                schema: "codex",
                table: "knowledge_chunks",
                columns: new[] { "Collection", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_Embedding",
                schema: "codex",
                table: "knowledge_chunks",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_chunks",
                schema: "codex");
        }
    }
}

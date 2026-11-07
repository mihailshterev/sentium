using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Codex.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingModelToKnowledgeChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                schema: "codex",
                table: "knowledge_chunks",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbeddingModel",
                schema: "codex",
                table: "knowledge_chunks");
        }
    }
}

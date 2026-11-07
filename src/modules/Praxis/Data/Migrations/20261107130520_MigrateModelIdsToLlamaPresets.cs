using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <summary>
    /// Data-only migration for the switch from Ollama to llama-server: model ids are now the router preset
    /// names in <c>infra/llm/models.ini</c> instead of Ollama tags.
    /// <list type="bullet">
    /// <item>Agents: a known Ollama tag maps to its preset; any other tag becomes <c>''</c> (use the default model),
    /// because the router cannot serve it.</item>
    /// <item>Conversations: same mapping, but unknown tags become the default preset, since the portal resumes a
    /// conversation with its stored model.</item>
    /// </list>
    /// Not reversible (the original tags are not kept); Down is a no-op.
    /// </summary>
    public partial class MigrateModelIdsToLlamaPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE praxis."Agents"
                SET "Model" = CASE WHEN "Model" IN ('gemma4:e4b', 'gemma4:e4b-it-qat') THEN 'gemma-4-e4b' ELSE '' END
                WHERE "Model" <> '' AND "Model" <> 'gemma-4-e4b';
                """);

            migrationBuilder.Sql("""
                UPDATE praxis."Conversations"
                SET "Model" = 'gemma-4-e4b'
                WHERE "Model" <> 'gemma-4-e4b';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

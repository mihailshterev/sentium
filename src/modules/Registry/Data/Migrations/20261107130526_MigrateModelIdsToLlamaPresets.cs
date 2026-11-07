using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Registry.Data.Migrations
{
    /// <summary>
    /// Data-only migration for the switch from Ollama to llama-server: model ids are now the router preset
    /// names in <c>infra/llm/models.ini</c>. The global default model becomes the default preset (it must name
    /// a served model); the intent-check model becomes <c>''</c> (use the default model) unless it was the
    /// default's Ollama tag. Not reversible (the original tags are not kept); Down is a no-op.
    /// </summary>
    public partial class MigrateModelIdsToLlamaPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE registry."SystemSettings"
                SET "Settings" = jsonb_set("Settings", '{inference,defaultModel}', '"gemma-4-e4b"')
                WHERE jsonb_typeof("Settings" -> 'inference') = 'object'
                  AND COALESCE("Settings" #>> '{inference,defaultModel}', '') <> 'gemma-4-e4b';
                """);

            migrationBuilder.Sql("""
                UPDATE registry."SystemSettings"
                SET "Settings" = jsonb_set(
                    "Settings",
                    '{pdp,intentCheckModel}',
                    CASE WHEN "Settings" #>> '{pdp,intentCheckModel}' IN ('gemma4:e4b', 'gemma4:e4b-it-qat')
                         THEN '"gemma-4-e4b"'::jsonb ELSE '""'::jsonb END)
                WHERE jsonb_typeof("Settings" -> 'pdp') = 'object'
                  AND COALESCE("Settings" #>> '{pdp,intentCheckModel}', '') NOT IN ('', 'gemma-4-e4b');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

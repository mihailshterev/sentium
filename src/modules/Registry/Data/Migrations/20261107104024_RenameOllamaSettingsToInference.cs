using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Registry.Data.Migrations
{
    /// <summary>
    /// Data-only migration: the global <c>ollama</c> settings group became <c>inference</c> and lost
    /// <c>agentContextWindow</c> (context size is now fixed per model by the inference server).
    /// Without it the stored default model and temperature would silently fall back to defaults,
    /// because the JSON deserializer ignores the unknown <c>ollama</c> property.
    /// </summary>
    public partial class RenameOllamaSettingsToInference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE registry."SystemSettings"
                SET "Settings" = ("Settings" - 'ollama')
                    || jsonb_build_object('inference', ("Settings" -> 'ollama') - 'agentContextWindow')
                WHERE jsonb_typeof("Settings" -> 'ollama') = 'object'
                  AND ("Settings" -> 'inference') IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE registry."SystemSettings"
                SET "Settings" = "Settings" - 'ollama'
                WHERE ("Settings" -> 'ollama') IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // agentContextWindow is not restored; the old settings class defaults it when missing.
            migrationBuilder.Sql("""
                UPDATE registry."SystemSettings"
                SET "Settings" = ("Settings" - 'inference')
                    || jsonb_build_object('ollama', "Settings" -> 'inference')
                WHERE jsonb_typeof("Settings" -> 'inference') = 'object';
                """);
        }
    }
}

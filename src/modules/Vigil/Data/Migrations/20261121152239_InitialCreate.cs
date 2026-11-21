using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sentium.Vigil.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vigil");

            migrationBuilder.CreateTable(
                name: "InferenceServerMinutes",
                schema: "vigil",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EpochMinute = table.Column<long>(type: "bigint", nullable: false),
                    Server = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PromptTokens = table.Column<long>(type: "bigint", nullable: false),
                    PromptSeconds = table.Column<double>(type: "double precision", nullable: false),
                    GeneratedTokens = table.Column<long>(type: "bigint", nullable: false),
                    GenerationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    PeakRequestsProcessing = table.Column<int>(type: "integer", nullable: false),
                    PeakRequestsDeferred = table.Column<int>(type: "integer", nullable: false),
                    Samples = table.Column<int>(type: "integer", nullable: false),
                    BusySamples = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InferenceServerMinutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InferenceUsageMinutes",
                schema: "vigil",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EpochMinute = table.Column<long>(type: "bigint", nullable: false),
                    Caller = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Requests = table.Column<long>(type: "bigint", nullable: false),
                    Errors = table.Column<long>(type: "bigint", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: false),
                    FirstChunkSeconds = table.Column<double>(type: "double precision", nullable: false),
                    FirstChunkCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InferenceUsageMinutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModelLoadEvents",
                schema: "vigil",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Server = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelLoadEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InferenceServerMinutes_EpochMinute",
                schema: "vigil",
                table: "InferenceServerMinutes",
                column: "EpochMinute");

            migrationBuilder.CreateIndex(
                name: "IX_InferenceUsageMinutes_EpochMinute",
                schema: "vigil",
                table: "InferenceUsageMinutes",
                column: "EpochMinute");

            migrationBuilder.CreateIndex(
                name: "IX_ModelLoadEvents_CompletedAt",
                schema: "vigil",
                table: "ModelLoadEvents",
                column: "CompletedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InferenceServerMinutes",
                schema: "vigil");

            migrationBuilder.DropTable(
                name: "InferenceUsageMinutes",
                schema: "vigil");

            migrationBuilder.DropTable(
                name: "ModelLoadEvents",
                schema: "vigil");
        }
    }
}

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Mcp.Data.Migrations
{
    /// <summary>
    /// MCP servers used to live in the Praxis module (<c>praxis."McpServers"</c>, created by Praxis' <c>AddMcpServers</c>
    /// and <c>AddMcpServerVariables</c> migrations). Existing data is MOVED into the <c>mcp</c> schema, never recreated;
    /// the <c>IF</c> guards make a database that never had the Praxis table (or already moved it) create the table
    /// instead, so this is safe in every order. Praxis migrates first (see <c>SentiumModules</c>), so on a fresh database
    /// the table is created by Praxis and then moved here.
    /// </summary>
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "mcp");

            migrationBuilder.Sql("""ALTER TABLE IF EXISTS praxis."McpServers" SET SCHEMA mcp;""");

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS mcp."McpServers"
                (
                    "Id" uuid NOT NULL,
                    "Name" character varying(24) NOT NULL,
                    "DisplayName" character varying(100) NOT NULL,
                    "Description" character varying(1000) NULL,
                    "Transport" character varying(32) NOT NULL,
                    "Endpoint" character varying(2048) NULL,
                    "Image" character varying(512) NULL,
                    "Args" text[] NOT NULL,
                    "Network" character varying(128) NULL,
                    "MemoryMb" integer NULL,
                    "CpuLimit" double precision NULL,
                    "ProtectedSecrets" text NULL,
                    "Variables" jsonb NOT NULL,
                    "Enabled" boolean NOT NULL,
                    "LastSyncedAt" timestamp with time zone NULL,
                    "LastError" text NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "Tools" jsonb NULL,
                    CONSTRAINT "PK_McpServers" PRIMARY KEY ("Id")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_McpServers_Name" ON mcp."McpServers" ("Name");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hand the table back to Praxis rather than dropping it: Praxis' own migration history still owns its
            // creation, and rolling Praxis back past AddMcpServers expects it in the praxis schema.
            migrationBuilder.Sql("""ALTER TABLE IF EXISTS mcp."McpServers" SET SCHEMA praxis;""");
        }
    }
}

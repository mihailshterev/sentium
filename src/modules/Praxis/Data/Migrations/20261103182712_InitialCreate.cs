using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "praxis");

            migrationBuilder.CreateTable(
                name: "AgentLearnings",
                schema: "praxis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGlobal = table.Column<bool>(type: "boolean", nullable: false),
                    AgentName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsIngested = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentLearnings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Agents",
                schema: "praxis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Model = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentSkills",
                schema: "praxis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: false),
                    SkillType = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSkills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                schema: "praxis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Model = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionLogs",
                schema: "praxis",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AgentId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    OriginalUserPrompt = table.Column<string>(type: "text", nullable: true),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ExitCode = table.Column<long>(type: "bigint", nullable: false),
                    Output = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    TimedOut = table.Column<bool>(type: "boolean", nullable: false),
                    PolicyDenied = table.Column<bool>(type: "boolean", nullable: false),
                    PolicyDenialReason = table.Column<string>(type: "text", nullable: true),
                    SentinelAuditId = table.Column<Guid>(type: "uuid", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Artifacts = table.Column<string>(type: "jsonb", nullable: true),
                    FileContext = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionLogs", x => x.JobId);
                });

            migrationBuilder.CreateTable(
                name: "Workflows",
                schema: "praxis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspaces",
                schema: "praxis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "praxis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    EnhancedPrompt = table.Column<string>(type: "text", nullable: true),
                    Thought = table.Column<string>(type: "text", nullable: true),
                    ToolCalls = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "praxis",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowAgents",
                schema: "praxis",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowAgents", x => new { x.WorkflowId, x.AgentId });
                    table.ForeignKey(
                        name: "FK_WorkflowAgents_Agents_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "praxis",
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowAgents_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "praxis",
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowRuns",
                schema: "praxis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggerType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TriggerPayload = table.Column<string>(type: "text", nullable: false),
                    Explanation = table.Column<string>(type: "text", nullable: false),
                    Risk = table.Column<string>(type: "text", nullable: false),
                    Recommendation = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LogJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowRuns_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "praxis",
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProjectFiles",
                schema: "praxis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    BlobName = table.Column<Guid>(type: "uuid", nullable: false),
                    Extension = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ProcessingStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectFiles_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalSchema: "praxis",
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_AgentName",
                schema: "praxis",
                table: "AgentLearnings",
                column: "AgentName");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_CapturedAt",
                schema: "praxis",
                table: "AgentLearnings",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_IsGlobal",
                schema: "praxis",
                table: "AgentLearnings",
                column: "IsGlobal");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_IsIngested",
                schema: "praxis",
                table: "AgentLearnings",
                column: "IsIngested");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLearnings_UserId",
                schema: "praxis",
                table: "AgentLearnings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_UserId_Name",
                schema: "praxis",
                table: "Agents",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentSkills_CreatedAt",
                schema: "praxis",
                table: "AgentSkills",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSkills_UserId_Name",
                schema: "praxis",
                table: "AgentSkills",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UserId_Title",
                schema: "praxis",
                table: "Conversations",
                columns: new[] { "UserId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionLogs_ExecutedAt",
                schema: "praxis",
                table: "ExecutionLogs",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                schema: "praxis",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_CreatedAt",
                schema: "praxis",
                table: "ProjectFiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_UserId",
                schema: "praxis",
                table: "ProjectFiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFiles_WorkspaceId",
                schema: "praxis",
                table: "ProjectFiles",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowAgents_AgentId",
                schema: "praxis",
                table: "WorkflowAgents",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_StartedAt",
                schema: "praxis",
                table: "WorkflowRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_UserId",
                schema: "praxis",
                table: "WorkflowRuns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowRuns_WorkflowId",
                schema: "praxis",
                table: "WorkflowRuns",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_UserId_Name",
                schema: "praxis",
                table: "Workflows",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_UserId_Name",
                schema: "praxis",
                table: "Workspaces",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentLearnings",
                schema: "praxis");

            migrationBuilder.DropTable(
                name: "AgentSkills",
                schema: "praxis");

            migrationBuilder.DropTable(
                name: "ExecutionLogs",
                schema: "praxis");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "praxis");

            migrationBuilder.DropTable(
                name: "ProjectFiles",
                schema: "praxis");

            migrationBuilder.DropTable(
                name: "WorkflowAgents",
                schema: "praxis");

            migrationBuilder.DropTable(
                name: "WorkflowRuns",
                schema: "praxis");

            migrationBuilder.DropTable(
                name: "Conversations",
                schema: "praxis");

            migrationBuilder.DropTable(
                name: "Workspaces",
                schema: "praxis");

            migrationBuilder.DropTable(
                name: "Agents",
                schema: "praxis");

            migrationBuilder.DropTable(
                name: "Workflows",
                schema: "praxis");
        }
    }
}

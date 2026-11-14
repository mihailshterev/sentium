using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sentium.Praxis.Data.Migrations
{
    /// <summary>
    /// Scheduling moved out of Praxis into the <c>Sentium.Scheduling</c> assembly. Quartz stores each job's CLR type as
    /// <c>Namespace.Type, Assembly</c>, so persisted jobs would no longer resolve their job class without this rewrite.
    /// The EF model is unchanged; the Quartz tables stay in the <c>praxis</c> schema.
    /// </summary>
    public partial class RenameScheduledJobType : Migration
    {
        private const string OldJobType = "Sentium.Praxis.Features.Scheduling.ExecuteJob, Sentium.Praxis";
        private const string NewJobType = "Sentium.Scheduling.Features.Jobs.ExecuteJob, Sentium.Scheduling";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE praxis.qrtz_job_details SET job_class_name = '{NewJobType}' WHERE job_class_name = '{OldJobType}';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE praxis.qrtz_job_details SET job_class_name = '{OldJobType}' WHERE job_class_name = '{NewJobType}';");
        }
    }
}

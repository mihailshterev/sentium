using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sentium.Infrastructure.Extensions;

namespace Sentium.Codex.Data;

public sealed class CodexDbContextFactory : IDesignTimeDbContextFactory<CodexDbContext>
{
    public CodexDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CodexDbContext>();

        optionsBuilder.UseModuleNpgsql(ModuleDbContextExtensions.DesignTimeConnectionString, CodexDbContext.Schema, npgsql => npgsql.UseVector());

        return new CodexDbContext(optionsBuilder.Options);
    }
}

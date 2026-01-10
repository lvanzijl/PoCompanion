using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PoTool.Api.Persistence;

namespace PoTool.Api;

/// <summary>
/// Design-time factory for creating DbContext instances during EF Core migrations.
/// </summary>
public class PoToolDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PoToolDbContext>
{
    public PoToolDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PoToolDbContext>();
        optionsBuilder.UseSqlite("Data Source=potool.db");
        return new PoToolDbContext(optionsBuilder.Options);
    }
}

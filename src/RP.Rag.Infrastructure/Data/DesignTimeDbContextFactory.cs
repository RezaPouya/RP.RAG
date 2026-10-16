using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RP.Rag.Infrastructure.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RpRagDbContext>
{
    public RpRagDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RpRagDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=rp_rag_db;Username=rp_user;Password=rp_pass",
                npgsql => npgsql.UseVector())
            .Options;

        return new RpRagDbContext(options);
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SCADASMSSystem.Web.Data
{
    // Used only by EF Core CLI tools (migrations) at design time.
    // Not used at runtime.
    public class SCADADbContextFactory : IDesignTimeDbContextFactory<SCADADbContext>
    {
        public SCADADbContext CreateDbContext(string[] args)
        {
            var projectDir = Directory.GetCurrentDirectory();
            var config = new ConfigurationBuilder()
                .SetBasePath(projectDir)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<SCADADbContext>();
            var connectionString = config.GetConnectionString("DefaultConnection");
            optionsBuilder.UseSqlServer(connectionString);

            return new SCADADbContext(optionsBuilder.Options);
        }
    }
}

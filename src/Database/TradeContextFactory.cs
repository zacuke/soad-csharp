using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace soad_csharp.Database;
public class TradeContextFactory : IDesignTimeDbContextFactory<TradeDbContext>
{
    public TradeDbContext CreateDbContext(string[] args)
    {
        // Build the IConfiguration object to read from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory()) // Ensure the path is correct
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<TradeContextFactory>(optional: true, reloadOnChange: true)
            .Build();

        // Retrieve the connection string from the "ConnectionStrings" section
        var connectionString = configuration.GetConnectionString("TradeDb");

        var optionsBuilder = new DbContextOptionsBuilder<TradeDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new TradeDbContext(optionsBuilder.Options);
    }
}
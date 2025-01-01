using Alpaca.Markets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using soad_csharp.brokers;
using soad_csharp.database;
using soad_csharp.strategies;

namespace soad_csharp;

public class Worker(ILogger<Worker> logger, IConfiguration configuration, TradeDbContext appDbContext) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting Worker");

        var AlpacaApiKey = configuration["Alpaca:ApiKey"];
        var AlpacaApiSecret = configuration["Alpaca:ApiSecret"];
        IBroker broker = new AlpacaBroker(AlpacaApiKey, AlpacaApiSecret);
        var allocations = new List<AssetAllocation>
        {            
            new() { Name = "AAPL", Allocation = 0.2M, Type = AssetType.Stock },
            new() { Name = "GOOGL", Allocation = 0.3M, Type = AssetType.Stock },
            new() { Name = "MSFT", Allocation = 0.2M, Type = AssetType.Stock },
            new() { Name = "BTC/USD", Allocation = 0.15M, Type = AssetType.Crypto },
            new() { Name = "ETH/USD", Allocation = 0.15M, Type = AssetType.Crypto },

        };
        var strategy = new ConstantPercentageStrategy(broker, appDbContext, "TestStrategy", allocations, 0.2M, 5, 10000M, 0.1M, logger);
        await strategy.InitializeAsync();
        await strategy.RebalanceAsync();

        logger.LogInformation("Done");

    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
 
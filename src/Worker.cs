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
        var broker = new AlpacaBroker(AlpacaApiKey, AlpacaApiSecret);
        var startingCapital = 10000M;
        var allocations = new List<AssetAllocation>
        {            
            new() { Symbol = "AAPL", Allocation = 0.2M, AssetType = AssetType.Stock, StartingCapital=startingCapital },
            new() { Symbol = "GOOGL", Allocation = 0.3M, AssetType = AssetType.Stock,  StartingCapital=startingCapital },
            new() { Symbol = "MSFT", Allocation = 0.2M, AssetType = AssetType.Stock, StartingCapital=startingCapital },
            new() { Symbol = "BTC/USD", Allocation = 0.15M, AssetType = AssetType.Crypto, StartingCapital=startingCapital },
            new() { Symbol = "ETH/USD", Allocation = 0.15M, AssetType = AssetType.Crypto, StartingCapital=startingCapital },
          //  new() { Name = "CASH", Allocation = .9M, Type = AssetType.Cash },

        };
      //  var strategy = new ConstantPercentageStrategy(broker, appDbContext, "TestStrategy", allocations, 0.2M, 5, 10000M, 0.1M, logger);
        //strategy.Execute();
        //await strategy.InitializeAsync();
        //await strategy.RebalanceAsync();
        var strat = new ConstantPercentageStrategy(broker, appDbContext, allocations, logger, startingCapital, "TestStrategy");
        await strat.Execute();
        logger.LogInformation("Done");




    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
 
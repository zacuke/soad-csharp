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
    private Timer  _timer;

    public async Task StartAsync(CancellationToken cancellationToken)
    {

        logger.LogInformation("Starting Worker");

        //   await TestStrategy();
        //await OtherStrategy();

        _timer = new Timer(async state => await OtherStrategy(), null, TimeSpan.Zero, TimeSpan.FromMinutes(5));


    }

    private  async Task TestStrategy()
    {
        var strategyName = "TestStrategy";

        var AlpacaApiKey = configuration["TestStrategy:ApiKey"];
        var AlpacaApiSecret = configuration["TestStrategy:ApiSecret"];
        var broker = new AlpacaBroker(AlpacaApiKey, AlpacaApiSecret);
        var startingCapital = 10000M;
        var allocations = new List<AssetAllocation>
        {
            new() { Symbol = "AAPL", Allocation = 0.2M, AssetType = AssetType.Stock, StartingCapital=startingCapital },
            new() { Symbol = "GOOGL", Allocation = 0.3M, AssetType = AssetType.Stock,  StartingCapital=startingCapital },
            new() { Symbol = "MSFT", Allocation = 0.2M, AssetType = AssetType.Stock, StartingCapital=startingCapital },
            new() { Symbol = "BTC/USD", Allocation = 0.15M, AssetType = AssetType.Crypto, StartingCapital=startingCapital },
            new() { Symbol = "ETH/USD", Allocation = 0.15M, AssetType = AssetType.Crypto, StartingCapital=startingCapital },

        };

        var strat = new ConstantPercentageStrategy(broker, appDbContext, allocations, logger, startingCapital, strategyName);
        await strat.Execute();
        logger.LogInformation("{strategy} Done", strategyName);

    }

    private async Task OtherStrategy()
    {
        var strategyName = "OtherStrategy";

        var AlpacaApiKey = configuration["OtherStrategy:ApiKey"];
        var AlpacaApiSecret = configuration["OtherStrategy:ApiSecret"];
        var broker = new AlpacaBroker(AlpacaApiKey, AlpacaApiSecret);
        var startingCapital = 10000M;
        var allocations = new List<AssetAllocation>
        {
 
            new() { Symbol = "BTC/USD", Allocation = 0.33M, AssetType = AssetType.Crypto, StartingCapital=startingCapital },
            new() { Symbol = "ETH/USD", Allocation = 0.33M, AssetType = AssetType.Crypto, StartingCapital=startingCapital },
            new() { Symbol = "LTC/USD", Allocation = 0.34M, AssetType = AssetType.Crypto, StartingCapital=startingCapital },

        };

        var strat = new ConstantPercentageStrategy(broker, appDbContext, allocations, logger, startingCapital, strategyName);
        await strat.Execute();
        logger.LogInformation("{strategy} Done", strategyName);

    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
 
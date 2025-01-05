using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using soad_csharp;
using soad_csharp.Brokers;
using soad_csharp.Database;
using soad_csharp.Extensions;
using soad_csharp.Strategies.Abstract;
using System.Diagnostics;

public class BuyAndHoldStrategy : SimpleStrategy
{
    private readonly IBroker _broker;
    private readonly TradeDbContext _dbContext;
    private readonly ILogger _logger;
    private readonly List<AssetAllocation> _allocations;
    private readonly decimal _rebalanceThreshold = 0.05m; // 5% drift threshold for rebalancing
    private readonly string _strategyName;
    private readonly decimal _startingCapital;

    private List<BrokerPosition> _brokerPositions;
    private List<TradeRequest> _tradeRequests;
    public BuyAndHoldStrategy(
        IBroker broker,
        TradeDbContext dbContext,
        List<AssetAllocation> allocations,
        ILogger logger,
        decimal startingCapital,
        string strategyName
    ) : base(logger,broker,dbContext)
    {
        _broker = broker;
        _dbContext = dbContext;
        _logger = logger;
        _allocations = allocations;
        _startingCapital = startingCapital;
        PreValidateAllocations(_allocations);
        _strategyName = strategyName;
    }

    public override string StrategyName => _strategyName;
    public override decimal StartingCapital => _startingCapital;
    public override decimal ThresholdCapital => _startingCapital - (_startingCapital * _rebalanceThreshold);
    public override decimal BrokerTotalValue => _brokerPositions.Sum(p => p.MarketValue);

    public override List<BrokerPosition> BrokerPositions => _brokerPositions;
    public override List<TradeRequest> TradeRequests => _tradeRequests;
    public override async Task Execute()
    {
        // Sync broker to local
        _brokerPositions = await SyncPositionsToLocalAsync();

        await CheckUnfilledTrades();

        if (!await IsStrategyInitializedAsync())
        {
            _logger.LogDebug("Initial purchase allocation for strategy {StrategyName}", StrategyName);
            await PurchaseAllocation(_allocations);
            return;
        }
        await RunStrategy();

        await ExecuteTrades();
    }

    public override Task RunStrategy()
    {
        //initialization does the purchase so we don't do anything here
        return Task.CompletedTask;   
    }
}
 
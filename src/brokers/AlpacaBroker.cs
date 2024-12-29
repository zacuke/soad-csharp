using Alpaca.Markets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using soad_csharp.database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace soad_csharp.brokers
{
    public class AlpacaBroker : IBroker
    {
        // Replace these with your Alpaca API credentials
  
        private readonly IAlpacaTradingClient _tradingClient;
        private readonly IAlpacaDataClient _dataClient;
        private readonly IAlpacaCryptoDataClient _cryptoDataClient;
        public AlpacaBroker(string apiKey, string apiSecret)
        {
            //var AlpacaApiKey = _configuration["Alpaca:ApiKey"];
            //var AlpacaApiSecret = _configuration["Alpaca:ApiSecret"];
            var secretKey = new SecretKey(apiKey, apiSecret);

            _tradingClient = Environments.Paper.GetAlpacaTradingClient(secretKey);
            _dataClient = Environments.Paper.GetAlpacaDataClient(secretKey);
            _cryptoDataClient = Environments.Paper.GetAlpacaCryptoDataClient(secretKey);
        }

        public void Connect()
        {
            // Connection is implicit using the Alpaca clients with valid credentials
        }

        public async Task<decimal?> GetCurrentPriceAsync(string symbol, AssetType assetType)
        {
            IBar bar;
            switch(assetType)
            {
                case AssetType.Stock:
                    bar = await _dataClient.GetLatestBarAsync(new LatestMarketDataRequest(symbol));
                    break;
                case AssetType.Crypto:
                    var bars = await _cryptoDataClient.ListLatestBarsAsync(new LatestDataListRequest([symbol]));
                    bar = bars.FirstOrDefault().Value;
                    break;
                default:
                    throw new ArgumentException("Invalid asset type");
            }
            return bar?.Close;
        }

        public async Task<BidAsk> GetBidAskAsync(string symbol, AssetType assetType)
        {
            IQuote quote;
            switch (assetType)
            {
                case AssetType.Stock:
                    quote = await _dataClient.GetLatestQuoteAsync(new LatestMarketDataRequest(symbol));
                    break;
                case AssetType.Crypto:
                    var quotes = await _cryptoDataClient.ListLatestQuotesAsync(new LatestDataListRequest([symbol]));
                    quote = quotes.FirstOrDefault().Value;
                    break;
                default:
                    throw new ArgumentException("Invalid asset type");
            }

            return new BidAsk
            {
                BidPrice = quote?.BidPrice,
                AskPrice = quote?.AskPrice
            };

            //var quote = await _dataClient.GetLatestQuoteAsync(new LatestMarketDataRequest(symbol)) ;
            //return new BidAsk
            //{
            //    BidPrice = quote?.BidPrice,
            //    AskPrice = quote?.AskPrice
            //};
        }

        public async Task<AccountInfo> GetAccountInfoAsync()
        {
            var account = await _tradingClient.GetAccountAsync();
            return new AccountInfo
            {
                AccountId = account.AccountId.ToString(),
                AccountStatus = account.Status.ToString(),
                PortfolioValue = account.Equity ?? 0,
                BuyingPower = (int)(account.BuyingPower ?? 0)
            };
        }

        public async Task<List<Position>> GetPositionsAsync()
        {
            var positions = await _tradingClient.ListPositionsAsync();
            return [.. positions.Select(position => new Position
            {
                Symbol = position.Symbol,
                Quantity = position.Quantity,
                MarketValue = position.MarketValue ?? throw new Exception("null marketvalue unexpected"),
                AssetType = position.AssetClass switch
                {
                    AssetClass.Crypto => AssetType.Crypto,
                    AssetClass.UsEquity => AssetType.Stock,
                    AssetClass.UsOption => AssetType.Option,
                    _ => throw new ArgumentException("Invalid asset type") 

                }
            })];
        }
        public async Task<OrderResponse> PlaceOrderAsync(
            string symbol,
            int quantity,
            string side,
            decimal? price = null,
            string orderType = "limit",
            string timeInForce = "day")
        {
            Enum.TryParse(side, true, out OrderSide orderSide);
            Enum.TryParse(timeInForce, true, out TimeInForce tif);
            Enum.TryParse(orderType, true, out OrderType parsedOrderType);

            var orderRequest = new NewOrderRequest(
                symbol, quantity, orderSide,
                parsedOrderType,
                tif)
            {
                LimitPrice = price
            };

            var order = await _tradingClient.PostOrderAsync(orderRequest);
            return new OrderResponse
            {
                OrderId = order.OrderId.ToString(),
                Status = order.OrderStatus.ToString(),//todo create and convert to interface enum
                Symbol = order.Symbol,
                Quantity = order.Quantity,
                Side = orderSide.ToString(),
                OrderType = order.OrderType.ToString(),//todo create and convert to interface enum
                LimitPrice = order.LimitPrice,
                TimeInForce = order.TimeInForce.ToString()
            };
        }

        public Task<OrderResponse> PlaceOptionOrderAsync(
            string symbol,
            int quantity,
            string side,
            string optionType,
            decimal strikePrice,
            string expirationDate,
            decimal? price = null,
            string orderType = "limit",
            string timeInForce = "day")
        {
            // Alpaca does not support options directly.
            throw new NotImplementedException("Options trading is not supported by the Alpaca API.");
        }

        public async Task<OrderStatus> GetOrderStatusAsync(string orderId)
        {
            var order = await _tradingClient.GetOrderAsync(orderId);
            return new OrderStatus
            {
                OrderId = order.OrderId.ToString(),
                Status = order.OrderStatus.ToString(),//todo create and convert to interface enum
                Symbol = order.Symbol,
                FilledQuantity = order.FilledQuantity ,
                Quantity = order.Quantity, 
                RemainingQuantity = order.Quantity - order.FilledQuantity
            };
        }

        public async Task<CancelOrderResponse> CancelOrderAsync(string orderId)
        {
            try
            {
                var orderGuid = Guid.Parse(orderId); 
                await _tradingClient.CancelOrderAsync(orderGuid);
                return new CancelOrderResponse
                {
                    OrderId = orderId,
                    Status = "Canceled"
                };
            }
            catch (Exception ex)
            {
                return new CancelOrderResponse
                {
                    OrderId = orderId,
                    Status = $"Failed: {ex.Message}"
                };
            }
        }
      

    }
}
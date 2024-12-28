using Alpaca.Markets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace soad_csharp.brokers
{
    public class alpaca_broker : IBroker
    {
        // Replace these with your Alpaca API credentials
        private const string ApiKey = "YOUR_API_KEY";
        private const string ApiSecret = "YOUR_API_SECRET";

        private readonly IAlpacaTradingClient _tradingClient;
        private readonly IAlpacaDataClient _dataClient;

        public alpaca_broker()
        {
            // Initialize Alpaca trading client
            _tradingClient = Environments.Paper.GetAlpacaTradingClient(new SecretKey(ApiKey, ApiSecret));

            // Initialize Alpaca data client
            _dataClient = Environments.Paper.GetAlpacaDataClient(new SecretKey(ApiKey, ApiSecret));
        }

        public void Connect()
        {
            // Connection is implicit using the Alpaca clients with valid credentials
        }

        public async Task<decimal?> GetCurrentPriceAsync(string symbol)
        {
            var bar = await _dataClient.GetLatestBarAsync(new LatestMarketDataRequest(symbol));
            return bar?.Close;
        }

        public async Task<BidAsk> GetBidAskAsync(string symbol)
        {
            // Wrap async call into a sync context for easier use
            var quote = await _dataClient.GetLatestQuoteAsync(new LatestMarketDataRequest(symbol)) ;
            return new BidAsk
            {
                BidPrice = quote?.BidPrice,
                AskPrice = quote?.AskPrice
            };
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
            return positions.Select(position => new Position
            {
                Symbol = position.Symbol,
                Quantity = position.Quantity,
                MarketValue = position.MarketValue ?? 0
            }).ToList();
        }

        public async Task<OrderResponse> PlaceOrderAsync(
            string symbol,
            int quantity,
            string side,
            decimal? price = null,
            string orderType = "limit",
            string timeInForce = "day")
        {
            var orderSide = side.ToLower() == "buy" ? OrderSide.Buy : OrderSide.Sell;
            var tif = timeInForce.ToLower() == "day" ? TimeInForce.Day : TimeInForce.Gtc;

            var orderRequest = new NewOrderRequest(
                symbol, quantity, orderSide,
                orderType.ToLower() == "market" ? OrderType.Market : OrderType.Limit,
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
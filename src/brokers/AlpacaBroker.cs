using Alpaca.Markets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using soad_csharp.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace soad_csharp.Brokers
{
    public class AlpacaBroker : IBroker
    {
        private readonly IAlpacaTradingClient _tradingClient;
        private readonly IAlpacaDataClient _dataClient;
        private readonly IAlpacaCryptoDataClient _cryptoDataClient;
        public AlpacaBroker(string apiKey, string apiSecret)
        {
            var secretKey = new SecretKey(apiKey, apiSecret);
            _tradingClient = Environments.Paper.GetAlpacaTradingClient(secretKey);
            _dataClient = Environments.Paper.GetAlpacaDataClient(secretKey);
            _cryptoDataClient = Environments.Paper.GetAlpacaCryptoDataClient(secretKey);
        }

        public void Connect()
        {
            // Connection is implicit using the Alpaca clients with valid credentials
        }

        public async Task<decimal> GetCurrentPriceAsync(string symbol, AssetType assetType)
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
            return bar.Close;
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

        public async Task<List<BrokerPosition>> GetPositionsAsync()
        {
            var positions = await _tradingClient.ListPositionsAsync();
            return [.. positions.Select(position => new BrokerPosition
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

                },
                CostBasis = position.CostBasis,
                CurrentPrice = position.AssetCurrentPrice ?? throw new Exception("null AssetCurrentPrice unexpected"),
                AverageEntryPrice = position.AverageEntryPrice
            })];
        }
        public async Task<OrderResponse> PlaceOrderAsync(
            string symbol,
            decimal quantity,
            string side,
            decimal price ,
            string orderType ,
            string timeInForce ,
            string clientOrderId)
        {
            Enum.TryParse(side, true, out OrderSide orderSide);
            Enum.TryParse(timeInForce, true, out TimeInForce tif);
            Enum.TryParse(orderType, true, out OrderType parsedOrderType);

            decimal? roundedPrice = Math.Round(price, 2);

            
            var qty = OrderQuantity.Fractional(quantity);
            var orderRequest = new NewOrderRequest(
                symbol, qty, orderSide,
                parsedOrderType,
                tif)
            {
                ClientOrderId = clientOrderId
            };
            if (orderType == "limit")
            {
                orderRequest.LimitPrice = roundedPrice;
            }

            
            var order = await _tradingClient.PostOrderAsync(orderRequest);
            return new OrderResponse
            {
                OrderId = order.OrderId.ToString(),
                Status = order.OrderStatus.ToString(),//todo create and convert to interface enum
                Symbol = order.Symbol,
                Quantity = order.Quantity ?? throw new Exception("unexpected null quantity returned from broker"),
                Side = orderSide.ToString(),
                OrderType = order.OrderType.ToString(),//todo create and convert to interface enum
                LimitPrice = order.LimitPrice,
                TimeInForce = order.TimeInForce.ToString(),
                BrokerResponseAssetClass = order.AssetClass.ToString(),
                BrokerResponseAssetId = order.AssetId.ToString(),
                ClientOrderId = clientOrderId,
                BrokerResponseFilledQty = order.FilledQuantity,
                BrokerResponseId = order.OrderId.ToString()
            };
        }

        public Task<OrderResponse> PlaceOptionOrderAsync(
            string symbol,
            decimal quantity,
            string side,
            string optionType,
            decimal strikePrice,
            string expirationDate,
            decimal price,
            string orderType,
            string timeInForce )
        {
            throw new NotImplementedException("Options trading is not supported");
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

        public async Task<OrderResponse> GetExistingOrderAsync(string responseId = null, string clientOrderId = null) 
        {
            if (responseId == null && clientOrderId == null)
            {
                throw new Exception("Must pass at least one of responseId or clientOrderId");
            }
 
 
            IOrder order;
            if (clientOrderId != null)
            {
                order = await _tradingClient.GetOrderAsync(clientOrderId);
            }
            else
            {
                order = await _tradingClient.GetOrderAsync(Guid.Parse(responseId));

            }
            return new OrderResponse
            {
                OrderId = order.OrderId.ToString(),
                Status = order.OrderStatus.ToString(),//todo create and convert to interface enum
                Symbol = order.Symbol,
                Quantity = order.Quantity ?? throw new Exception("unexpected null quantity returned from broker"),
                Side = order.OrderSide.ToString(),
                OrderType = order.OrderType.ToString(),//todo create and convert to interface enum
                LimitPrice = order.LimitPrice,
                TimeInForce = order.TimeInForce.ToString(),
                BrokerResponseAssetClass = order.AssetClass.ToString(),
                BrokerResponseAssetId = order.AssetId.ToString(),
                ClientOrderId = clientOrderId,
                BrokerResponseFilledQty = order.FilledQuantity,
                BrokerResponseId = order.OrderId.ToString()
            };
        }

        public async Task<List<OrderResponse>> GetOrdersAsync(string status = "open")
        {
            //var orderStatusFilter = (OrderStatusFilter)Enum.Parse(typeof(OrderStatusFilter), status);
            var orderStatusFilter = EnumExtensions.GetEnumValueFromEnumMember<OrderStatusFilter>(status);
            var ordersResponse = await _tradingClient.ListOrdersAsync(new ListOrdersRequest() { OrderStatusFilter= orderStatusFilter });
            var orders = new List<OrderResponse>();
            foreach (var order in ordersResponse)
            {
                orders.Add( new OrderResponse
                {
                    OrderId = order.OrderId.ToString(),
                    Status = order.OrderStatus.ToString(),//todo create and convert to interface enum
                    Symbol = order.Symbol,
                    Quantity = order.Quantity ?? throw new Exception("unexpected null quantity returned from broker"),
                    Side = order.OrderSide.ToString(),
                    OrderType = order.OrderType.ToString(),//todo create and convert to interface enum
                    LimitPrice = order.LimitPrice,
                    TimeInForce = order.TimeInForce.ToString(),
                    BrokerResponseAssetClass = order.AssetClass.ToString(),
                    BrokerResponseAssetId = order.AssetId.ToString(),
                    ClientOrderId = order.ClientOrderId,
                    BrokerResponseFilledQty = order.FilledQuantity,
                    BrokerResponseId = order.OrderId.ToString()
                }); 
            }
            return orders;
        }
    }
}
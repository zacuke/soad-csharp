using soad_csharp.brokers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace soad_csharp.Extensions
{
    public static class MarketExtensions
    {
        public static decimal TotalMarketValue(this List<BrokerPosition> brokerPositions)
        {
            var brokerTotalValue = 0M;
            foreach (var position in brokerPositions)
            {
                brokerTotalValue += position.MarketValue;
            }
            return brokerTotalValue;
        }
    }
}

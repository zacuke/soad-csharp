using soad_csharp.brokers;
using System;
using System.Collections.Generic;
namespace soad_csharp;

public static class TradingPairHelper
{

    public enum TradingPair
    {
        BTCUSD, // Bitcoin to USD
        ETHUSD, // Ethereum to USD
        LTCUSD  // Litecoin to USD (example of adding more pairs)
    }


    // Centralized mapping of TradingPair to their slashed format
    private static readonly Dictionary<TradingPair, string> TradingPairSlashedMap = new()
    {
        { TradingPair.BTCUSD, "BTC/USD" },
        { TradingPair.ETHUSD, "ETH/USD" },
        { TradingPair.LTCUSD, "LTC/USD" } // Add more mappings here
    };

    // Reverse mapping for slashed-to-enum translation
    private static readonly Dictionary<string, TradingPair> SlashedToEnumMap = CreateReverseMap();

    // Create the reverse mapping dynamically
    private static Dictionary<string, TradingPair> CreateReverseMap()
    {
        var reverseMap = new Dictionary<string, TradingPair>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in TradingPairSlashedMap)
        {
            reverseMap[kvp.Value] = kvp.Key;
        }
        return reverseMap;
    }

    // Translate a string (like "BTC/USD" or "BTCUSD") to a TradingPair enum
    public static string Translate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // First try the slashed map, then fallback to removing slashes
        if (SlashedToEnumMap.TryGetValue(input, out TradingPair pair))
        {
            return pair.ToString();
        }

        // Fallback: Remove slash and try matching
        string unslashedInput = input.Replace("/", "").ToUpperInvariant();
        if (Enum.TryParse<TradingPair>(unslashedInput, true, out pair))
        {
            return pair.ToString();
        }

        return input; // Not found
    }

    // Get the slashed format for a given TradingPair enum
    public static string GetSlashedFormat(TradingPair pair)
    {
        if (TradingPairSlashedMap.TryGetValue(pair, out string slashed))
        {
            return slashed;
        }

        throw new ArgumentOutOfRangeException(nameof(pair), $"The trading pair '{pair}' does not have a slashed mapping.");
    }

    public static BrokerPosition GetBrokerPositionsWhere(this List<BrokerPosition> brokerPositions, string symbol)
    {
        // Try to normalize the symbol only if it's part of the enum
        string normalizedSymbol = symbol;
        TradingPair tradingPair;

        if (Enum.TryParse<TradingPair>(symbol.Replace("/", "").ToUpperInvariant(), true, out tradingPair))
        {
            // If the symbol can be translated into a TradingPair enum:
            normalizedSymbol = tradingPair.ToString();
        }

        // Convert normalized symbol to both slashed and unslashed formats
        string slashedFormat = null;
        string unslashedFormat = normalizedSymbol;

        if (Enum.TryParse<TradingPair>(normalizedSymbol, true, out tradingPair))
        {
            slashedFormat = GetSlashedFormat(tradingPair);
        }

        // Search for matches in brokerPositions
        var result =  brokerPositions.FirstOrDefault(bp =>
            string.Equals(bp.Symbol, slashedFormat, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(bp.Symbol, unslashedFormat, StringComparison.OrdinalIgnoreCase));

        if (result == null)
            return new BrokerPosition()
            {
                Symbol = symbol,
                Quantity = 0
            };

        return result;
    }

}
using MyStockApp.Data.Models;

namespace MyStockApp.Services;

public interface IStockService
{
    // 基本資料查詢
    Task<Stock?> GetStockAsync(int stockId);
    Task<Stock?> GetStockBySymbolAsync(string symbol);
    Task<IReadOnlyList<Stock>> SearchStocksAsync(string? keyword = null, MarketType? market = null, string? industry = null);

    // 報價更新
    Task UpdateStockQuoteAsync(int stockId, StockQuote quote);

    // 歷史價格
    Task<IReadOnlyList<StockPriceHistory>> GetPriceHistoryAsync(int stockId, DateOnly fromDate, DateOnly toDate);
    Task<PriceStatistics> CalculatePriceStatisticsAsync(int stockId, DateOnly fromDate, DateOnly toDate);
    Task WriteDailyCloseAsync(int stockId, DateOnly date, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, long volume);

    // 股價更新事件
    event Action<int, decimal>? OnPriceUpdated;
}

public record StockQuote(
    decimal CurrentPrice,
    decimal OpenPrice,
    decimal HighPrice,
    decimal LowPrice,
    long Volume,
    DateTime UpdatedAt
);

public record PriceStatistics(
    decimal HighestPrice,
    decimal LowestPrice,
    decimal AveragePrice,
    decimal PriceChange,
    decimal ChangePercent
);

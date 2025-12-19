using MyStockApp.Data;
using MyStockApp.Data.Models;

namespace MyStockApp.Services;

/// <summary>
/// 持股部位服務介面
/// </summary>
public interface IPortfolioService
{
    /// <summary>
    /// 取得所有持股
    /// </summary>
    Task<IReadOnlyList<PortfolioItem>> GetPortfolioAsync();

    /// <summary>
    /// 取得投資組合摘要
    /// </summary>
    Task<PortfolioSummary> GetPortfolioSummaryAsync();

    /// <summary>
    /// 更新持股部位（支援傳入共用 DbContext 以參與交易）
    /// </summary>
    Task UpdatePortfolioAsync(int stockId, int quantityChange, decimal price, TradeSide side, decimal commission, AppDbContext? sharedContext = null);
}

/// <summary>
/// 持股項目
/// </summary>
public record PortfolioItem(
    int StockId,
    string StockSymbol,
    string StockName,
    int Quantity,
    decimal AverageCost,
    decimal CurrentPrice,
    decimal MarketValue,
    decimal UnrealizedPnL,
    decimal ReturnRate
);

/// <summary>
/// 投資組合摘要
/// </summary>
public record PortfolioSummary(
    decimal TotalMarketValue,
    decimal TotalCost,
    decimal TotalUnrealizedPnL,
    decimal TotalRealizedPnL,
    decimal TotalReturnRate
);

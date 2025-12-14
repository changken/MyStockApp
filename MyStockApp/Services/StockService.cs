using Microsoft.EntityFrameworkCore;
using MyStockApp.Data;
using MyStockApp.Data.Models;

namespace MyStockApp.Services;

public class StockService : IStockService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public event Action<int, decimal>? OnPriceUpdated;

    public StockService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // 3.1 基本資料查詢功能
    public async Task<Stock?> GetStockAsync(int stockId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Stocks.FindAsync(stockId);
    }

    public async Task<Stock?> GetStockBySymbolAsync(string symbol)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Stocks
            .FirstOrDefaultAsync(s => s.Symbol == symbol);
    }

    public async Task<IReadOnlyList<Stock>> SearchStocksAsync(
        string? keyword = null,
        MarketType? market = null,
        string? industry = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var query = context.Stocks.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lowerKeyword = keyword.ToLower();
            query = query.Where(s =>
                s.Symbol.ToLower().Contains(lowerKeyword) ||
                s.Name.ToLower().Contains(lowerKeyword));
        }

        if (market.HasValue)
        {
            query = query.Where(s => s.Market == market.Value);
        }

        if (!string.IsNullOrWhiteSpace(industry))
        {
            query = query.Where(s => s.Industry == industry);
        }

        return await query.OrderBy(s => s.Symbol).ToListAsync();
    }


    // 3.2 報價更新功能
    public async Task UpdateStockQuoteAsync(int stockId, StockQuote quote)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var stock = await context.Stocks.FindAsync(stockId);
        if (stock == null)
        {
            throw new InvalidOperationException($"Stock with ID {stockId} not found.");
        }

        stock.CurrentPrice = quote.CurrentPrice;
        stock.OpenPrice = quote.OpenPrice;
        stock.HighPrice = quote.HighPrice;
        stock.LowPrice = quote.LowPrice;
        stock.Volume = quote.Volume;
        stock.LastUpdated = quote.UpdatedAt;

        await context.SaveChangesAsync();

        // 觸發股價更新事件
        OnPriceUpdated?.Invoke(stockId, quote.CurrentPrice);
    }

    // 3.3 歷史價格資料管理
    public async Task<IReadOnlyList<StockPriceHistory>> GetPriceHistoryAsync(
        int stockId,
        DateOnly fromDate,
        DateOnly toDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.StockPriceHistories
            .Where(h => h.StockId == stockId && h.Date >= fromDate && h.Date <= toDate)
            .OrderByDescending(h => h.Date)
            .ToListAsync();
    }

    public async Task<PriceStatistics> CalculatePriceStatisticsAsync(
        int stockId,
        DateOnly fromDate,
        DateOnly toDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var histories = await context.StockPriceHistories
            .Where(h => h.StockId == stockId && h.Date >= fromDate && h.Date <= toDate)
            .OrderBy(h => h.Date)
            .ToListAsync();

        if (histories.Count == 0)
        {
            return new PriceStatistics(0, 0, 0, 0, 0);
        }

        var highestPrice = histories.Max(h => h.HighPrice);
        var lowestPrice = histories.Min(h => h.LowPrice);
        var averagePrice = histories.Average(h => h.ClosePrice);

        var firstClose = histories.First().ClosePrice;
        var lastClose = histories.Last().ClosePrice;
        var priceChange = lastClose - firstClose;
        var changePercent = firstClose != 0 ? (priceChange / firstClose) * 100 : 0;

        return new PriceStatistics(
            highestPrice,
            lowestPrice,
            Math.Round(averagePrice, 2),
            priceChange,
            Math.Round(changePercent, 2)
        );
    }

    // 3.4 每日收盤價寫入機制
    public async Task WriteDailyCloseAsync(
        int stockId,
        DateOnly date,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal closePrice,
        long volume)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // 檢查是否已存在該日資料，避免重複寫入
        var existing = await context.StockPriceHistories
            .FirstOrDefaultAsync(h => h.StockId == stockId && h.Date == date);

        if (existing != null)
        {
            return; // 已存在則不重複寫入
        }

        var history = new StockPriceHistory
        {
            StockId = stockId,
            Date = date,
            OpenPrice = openPrice,
            HighPrice = highPrice,
            LowPrice = lowPrice,
            ClosePrice = closePrice,
            Volume = volume
        };

        context.StockPriceHistories.Add(history);
        await context.SaveChangesAsync();
    }
}

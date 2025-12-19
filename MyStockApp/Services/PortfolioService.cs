using Microsoft.EntityFrameworkCore;
using MyStockApp.Data;
using MyStockApp.Data.Models;

namespace MyStockApp.Services;

/// <summary>
/// 持股部位服務實作
/// </summary>
public class PortfolioService : IPortfolioService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ITradingCostService _tradingCostService;
    private readonly IStockService _stockService;

    public PortfolioService(
        IDbContextFactory<AppDbContext> contextFactory,
        ITradingCostService tradingCostService,
        IStockService stockService)
    {
        _contextFactory = contextFactory;
        _tradingCostService = tradingCostService;
        _stockService = stockService;
    }

    public async Task<IReadOnlyList<PortfolioItem>> GetPortfolioAsync()
    {
        using var context = _contextFactory.CreateDbContext();

        var portfolios = await context.Portfolios
            .Include(p => p.Stock)
            .Where(p => p.Quantity > 0)
            .ToListAsync();

        var items = new List<PortfolioItem>();

        foreach (var portfolio in portfolios)
        {
            var currentPrice = portfolio.Stock.CurrentPrice;
            var marketValue = currentPrice * portfolio.Quantity;
            var cost = portfolio.TotalCost;

            // 計算未實現損益（含預估賣出成本）
            var estimatedSellCost = _tradingCostService.CalculateTotalCost(
                marketValue,
                TradeSide.Sell,
                0.6m
            );

            var unrealizedPnL = marketValue - cost - estimatedSellCost.TotalCost;
            var returnRate = cost > 0 ? (unrealizedPnL / cost) * 100 : 0;

            items.Add(new PortfolioItem(
                portfolio.StockId,
                portfolio.Stock.Symbol,
                portfolio.Stock.Name,
                portfolio.Quantity,
                portfolio.AverageCost,
                currentPrice,
                marketValue,
                unrealizedPnL,
                returnRate
            ));
        }

        return items;
    }

    public async Task<PortfolioSummary> GetPortfolioSummaryAsync()
    {
        var portfolios = await GetPortfolioAsync();

        var totalMarketValue = portfolios.Sum(p => p.MarketValue);
        var totalCost = portfolios.Sum(p => p.AverageCost * p.Quantity);
        var totalUnrealizedPnL = portfolios.Sum(p => p.UnrealizedPnL);

        using var context = _contextFactory.CreateDbContext();
        var totalRealizedPnL = await context.Portfolios.SumAsync(p => p.RealizedPnL);

        var totalReturnRate = totalCost > 0 ? (totalUnrealizedPnL / totalCost) * 100 : 0;

        return new PortfolioSummary(
            totalMarketValue,
            totalCost,
            totalUnrealizedPnL,
            totalRealizedPnL,
            totalReturnRate
        );
    }

    public async Task UpdatePortfolioAsync(
        int stockId,
        int quantityChange,
        decimal price,
        TradeSide side,
        decimal commission,
        AppDbContext? sharedContext = null)
    {
        var context = sharedContext ?? _contextFactory.CreateDbContext();
        var shouldDispose = sharedContext == null;

        try
        {
            var portfolio = await context.Portfolios
                .FirstOrDefaultAsync(p => p.StockId == stockId);

            if (portfolio == null)
            {
                // 新建持股
                portfolio = new Portfolio
                {
                    StockId = stockId,
                    Quantity = 0,
                    AverageCost = 0,
                    TotalCost = 0,
                    RealizedPnL = 0,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Portfolios.Add(portfolio);
            }

            if (side == TradeSide.Buy)
            {
                // 買入：更新平均成本（加權平均，含手續費）
                var totalCost = portfolio.TotalCost + (price * quantityChange) + commission;
                var totalQuantity = portfolio.Quantity + quantityChange;

                portfolio.AverageCost = totalQuantity > 0 ? totalCost / totalQuantity : 0;
                portfolio.TotalCost = totalCost;
                portfolio.Quantity = totalQuantity;
            }
            else // Sell
            {
                // 賣出：計算已實現損益
                var sellAmount = price * quantityChange;
                var costBasis = portfolio.AverageCost * quantityChange;
                var realizedPnL = sellAmount - costBasis - commission;

                portfolio.RealizedPnL += realizedPnL;
                portfolio.Quantity -= quantityChange;
                portfolio.TotalCost -= costBasis;

                // 當持股歸零時保留紀錄供歷史查詢
            }

            portfolio.UpdatedAt = DateTime.UtcNow;

            if (shouldDispose)
            {
                await context.SaveChangesAsync();
            }
        }
        finally
        {
            if (shouldDispose)
            {
                context.Dispose();
            }
        }
    }
}

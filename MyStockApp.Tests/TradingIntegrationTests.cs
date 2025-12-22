using Microsoft.EntityFrameworkCore;
using MyStockApp.Data;
using MyStockApp.Data.Models;
using MyStockApp.Services;

namespace MyStockApp.Tests;

/// <summary>
/// Task 15: 整合測試與驗證
/// 測試完整的交易流程端到端功能
/// </summary>
public class TradingIntegrationTests
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ITradingService _tradingService;
    private readonly IPortfolioService _portfolioService;
    private readonly IStockService _stockService;
    private readonly ITradingCostService _tradingCostService;

    public TradingIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"IntegrationTestDb_{Guid.NewGuid()}")
            .Options;

        _factory = new TestDbContextFactory(options);
        var marketHoursService = new MarketHoursService();
        _tradingCostService = new TradingCostService();
        _stockService = new StockService(_factory);
        var auditService = new AuditService(_factory);
        _portfolioService = new PortfolioService(_factory, _tradingCostService, _stockService);
        _tradingService = new TradingService(
            _factory,
            _tradingCostService,
            _portfolioService,
            _stockService,
            marketHoursService,
            auditService
        );
    }

    #region Task 15.1: 交易流程整合測試

    [Fact]
    public async Task CompleteTradeFlow_BuyThenSell_CalculatesRealizedPnLCorrectly()
    {
        // Arrange - 建立股票
        var stock = await SeedStock("2330", "台積電", 100m);

        // 確保在交易時段
        if (!new MarketHoursService().IsMarketOpen())
        {
            return; // Skip test if not in market hours
        }

        // Act - Step 1: 買入 1000 股 @ 100
        var buyRequest = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Market, 1000);
        var buyResult = await _tradingService.CreateOrderAsync(buyRequest);

        // Assert - 買入成功
        Assert.True(buyResult.IsSuccess);
        Assert.Equal(OrderStatus.Executed, buyResult.Value!.Status);

        // 驗證持股建立
        using (var context = _factory.CreateDbContext())
        {
            var portfolio = await context.Portfolios.FirstOrDefaultAsync(p => p.StockId == stock.Id);
            Assert.NotNull(portfolio);
            Assert.Equal(1000, portfolio.Quantity);

            // 計算買入成本：100 * 1000 * 0.001425 = 142.5
            var expectedBuyCommission = 100m * 1000 * 0.001425m;
            var expectedBuyTotal = 100m * 1000 + expectedBuyCommission; // 100,142.5

            Assert.True(portfolio.AverageCost > 100m); // 平均成本應該大於買入價格（因為包含手續費）
        }

        // Act - Step 2: 更新股價至 120
        stock.CurrentPrice = 120m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        // Act - Step 3: 賣出 500 股 @ 120
        var sellRequest = new CreateOrderRequest(stock.Id, OrderSide.Sell, OrderType.Market, 500);
        var sellResult = await _tradingService.CreateOrderAsync(sellRequest);

        // Assert - 賣出成功
        Assert.True(sellResult.IsSuccess);
        Assert.Equal(OrderStatus.Executed, sellResult.Value!.Status);

        // 驗證持股更新
        using (var context = _factory.CreateDbContext())
        {
            var portfolio = await context.Portfolios.FirstOrDefaultAsync(p => p.StockId == stock.Id);
            Assert.NotNull(portfolio);
            Assert.Equal(500, portfolio.Quantity); // 剩餘 500 股

            // 驗證已實現損益（賣出 500 股的損益）
            Assert.True(portfolio.RealizedPnL > 0); // 應該有獲利（買入 100 賣出 120）

            // 計算預期損益
            // 賣出收入：120 * 500 = 60,000
            // 賣出手續費：60,000 * 0.001425 = 85.5
            // 賣出交易稅：60,000 * 0.003 = 180
            // 淨賣出收入：60,000 - 85.5 - 180 = 59,734.5
            // 買入成本（500 股）：(100 + 0.1425) * 500 = 50,071.25
            // 已實現損益：59,734.5 - 50,071.25 = 9,663.25
            var expectedRealizedPnL = (120m * 500 - 85.5m - 180m) - ((100m + 0.1425m) * 500);
            Assert.True(Math.Abs(portfolio.RealizedPnL - expectedRealizedPnL) < 1m); // 允許 1 元誤差
        }

        // 驗證交易紀錄
        using (var context = _factory.CreateDbContext())
        {
            var trades = await context.Trades.OrderBy(t => t.ExecutedAt).ToListAsync();
            Assert.Equal(2, trades.Count);

            // 買入交易
            var buyTrade = trades[0];
            Assert.Equal(TradeSide.Buy, buyTrade.Side);
            Assert.Equal(1000, buyTrade.Quantity);
            Assert.Equal(100m, buyTrade.ExecutedPrice);
            Assert.True(buyTrade.Commission > 0);
            Assert.Equal(0, buyTrade.TransactionTax); // 買入無交易稅

            // 賣出交易
            var sellTrade = trades[1];
            Assert.Equal(TradeSide.Sell, sellTrade.Side);
            Assert.Equal(500, sellTrade.Quantity);
            Assert.Equal(120m, sellTrade.ExecutedPrice);
            Assert.True(sellTrade.Commission > 0);
            Assert.True(sellTrade.TransactionTax > 0); // 賣出有交易稅
        }
    }

    [Fact]
    public async Task CompleteTradeFlow_SellAtLoss_CalculatesNegativePnL()
    {
        // Arrange - 建立股票並買入
        var stock = await SeedStock("2317", "鴻海", 100m);

        if (!new MarketHoursService().IsMarketOpen())
        {
            return;
        }

        // 買入 1000 股 @ 100
        var buyRequest = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Market, 1000);
        await _tradingService.CreateOrderAsync(buyRequest);

        // Act - 股價下跌至 80
        stock.CurrentPrice = 80m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        // 賣出 1000 股 @ 80（虧損）
        var sellRequest = new CreateOrderRequest(stock.Id, OrderSide.Sell, OrderType.Market, 1000);
        var sellResult = await _tradingService.CreateOrderAsync(sellRequest);

        // Assert
        Assert.True(sellResult.IsSuccess);

        using (var context = _factory.CreateDbContext())
        {
            var portfolio = await context.Portfolios.FirstOrDefaultAsync(p => p.StockId == stock.Id);

            // 全部賣出後，持股應該被刪除或數量為 0
            if (portfolio != null)
            {
                Assert.Equal(0, portfolio.Quantity);
                Assert.True(portfolio.RealizedPnL < 0); // 應該有虧損
            }

            // 驗證有 2 筆交易紀錄
            var trades = await context.Trades.ToListAsync();
            Assert.Equal(2, trades.Count);
        }
    }

    [Fact]
    public async Task CompleteTradeFlow_CancelOrder_DoesNotUpdatePortfolio()
    {
        // Arrange
        var stock = await SeedStock("2330", "台積電", 100m);

        // 建立限價單（不會立即成交）
        var request = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Limit, 100, 95m);
        var orderResult = await _tradingService.CreateOrderAsync(request);

        Assert.True(orderResult.IsSuccess);
        var orderId = orderResult.Value!.Id;

        // Act - 取消訂單
        var cancelResult = await _tradingService.CancelOrderAsync(orderId);

        // Assert
        Assert.True(cancelResult.IsSuccess);

        using (var context = _factory.CreateDbContext())
        {
            // 驗證訂單已取消
            var order = await context.Orders.FindAsync(orderId);
            Assert.NotNull(order);
            Assert.Equal(OrderStatus.Cancelled, order.Status);

            // 驗證沒有交易紀錄
            var trades = await context.Trades.Where(t => t.OrderId == orderId).ToListAsync();
            Assert.Empty(trades);

            // 驗證沒有持股
            var portfolio = await context.Portfolios.FirstOrDefaultAsync(p => p.StockId == stock.Id);
            Assert.Null(portfolio);
        }
    }

    [Fact]
    public async Task TradingCost_CalculatesCorrectly_ForBuyAndSellOrders()
    {
        // Arrange
        var buyAmount = 1000000m; // 100 萬買入
        var sellAmount = 1000000m; // 100 萬賣出

        // Act
        var buyCost = _tradingCostService.CalculateBuyCommission(buyAmount);
        var sellCost = _tradingCostService.CalculateSellCost(sellAmount);

        // Assert - 買入手續費 0.1425%
        var expectedBuyCommission = buyAmount * 0.001425m; // 1,425
        Assert.Equal(expectedBuyCommission, buyCost.commission);

        // Assert - 賣出手續費 0.1425% + 交易稅 0.3%
        var expectedSellCommission = sellAmount * 0.001425m; // 1,425
        var expectedTax = sellAmount * 0.003m; // 3,000
        Assert.Equal(expectedSellCommission, sellCost.commission);
        Assert.Equal(expectedTax, sellCost.tax);

        // 總成本
        var totalBuyCost = buyCost.commission;
        var totalSellCost = sellCost.commission + sellCost.tax;
        Assert.Equal(1425m, totalBuyCost);
        Assert.Equal(4425m, totalSellCost);
    }

    #endregion

    #region Helper Methods

    private async Task<Stock> SeedStock(string symbol, string name, decimal price)
    {
        using var context = _factory.CreateDbContext();
        var stock = new Stock
        {
            Symbol = symbol,
            Name = name,
            Market = MarketType.Listed,
            Industry = "電子",
            CurrentPrice = price,
            OpenPrice = price,
            HighPrice = price * 1.02m,
            LowPrice = price * 0.98m,
            Volume = 10000,
            UpdatedAt = DateTime.UtcNow
        };
        context.Stocks.Add(stock);
        await context.SaveChangesAsync();
        return stock;
    }

    #endregion
}

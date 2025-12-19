using Microsoft.EntityFrameworkCore;
using MyStockApp.Data;
using MyStockApp.Data.Models;
using MyStockApp.Services;

namespace MyStockApp.Tests;

public class PortfolioServiceTests
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IPortfolioService _portfolioService;
    private readonly ITradingCostService _tradingCostService;
    private readonly IStockService _stockService;

    public PortfolioServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PortfolioTestDb_{Guid.NewGuid()}")
            .Options;

        _factory = new TestDbContextFactory(options);
        _tradingCostService = new TradingCostService();
        _stockService = new StockService(_factory);
        _portfolioService = new PortfolioService(_factory, _tradingCostService, _stockService);
    }

    #region Task 6.1: 持股計算與更新

    [Fact]
    public async Task UpdatePortfolioAsync_BuyFirstTime_CreatesNewPortfolio()
    {
        // Arrange
        var stock = await SeedStock();

        // Act
        await _portfolioService.UpdatePortfolioAsync(
            stock.Id,
            quantityChange: 100,
            price: 50m,
            side: TradeSide.Buy,
            commission: 71.25m
        );

        // Assert
        using var context = _factory.CreateDbContext();
        var portfolio = await context.Portfolios.FirstOrDefaultAsync(p => p.StockId == stock.Id);

        Assert.NotNull(portfolio);
        Assert.Equal(100, portfolio.Quantity);
        Assert.Equal(50.7125m, portfolio.AverageCost); // (50*100 + 71.25) / 100
        Assert.Equal(5071.25m, portfolio.TotalCost);
    }

    [Fact]
    public async Task UpdatePortfolioAsync_BuyAdditional_UpdatesAverageCost()
    {
        // Arrange
        var stock = await SeedStock();

        // 第一次買入 100 股 @ 50，手續費 71.25
        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 50m, TradeSide.Buy, 71.25m);

        // Act - 第二次買入 50 股 @ 60，手續費 42.75
        await _portfolioService.UpdatePortfolioAsync(stock.Id, 50, 60m, TradeSide.Buy, 42.75m);

        // Assert
        using var context = _factory.CreateDbContext();
        var portfolio = await context.Portfolios.FirstOrDefaultAsync(p => p.StockId == stock.Id);

        Assert.Equal(150, portfolio.Quantity);
        // (5071.25 + 3000 + 42.75) / 150 = 54.0933...
        Assert.Equal(54.09m, Math.Round(portfolio.AverageCost, 2));
        Assert.Equal(8114m, portfolio.TotalCost);
    }

    [Fact]
    public async Task UpdatePortfolioAsync_Sell_CalculatesRealizedPnL()
    {
        // Arrange
        var stock = await SeedStock();
        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 50m, TradeSide.Buy, 71.25m);

        // Act - 賣出 40 股 @ 60，手續費 34.20，交易稅 72（已包含在 commission 中）
        await _portfolioService.UpdatePortfolioAsync(stock.Id, 40, 60m, TradeSide.Sell, 34.20m);

        // Assert
        using var context = _factory.CreateDbContext();
        var portfolio = await context.Portfolios.FirstOrDefaultAsync(p => p.StockId == stock.Id);

        Assert.Equal(60, portfolio.Quantity);

        // 已實現損益 = 賣出金額 - 成本 - 手續費
        // = (60 * 40) - (50.7125 * 40) - 34.20
        // = 2400 - 2028.5 - 34.20 = 337.3
        Assert.Equal(337.3m, Math.Round(portfolio.RealizedPnL, 2));
    }

    [Fact]
    public async Task UpdatePortfolioAsync_SellAll_KeepsRecordForHistory()
    {
        // Arrange
        var stock = await SeedStock();
        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 50m, TradeSide.Buy, 71.25m);

        // Act - 賣出全部 100 股
        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 60m, TradeSide.Sell, 85.50m);

        // Assert - 持股歸零但紀錄仍保留
        using var context = _factory.CreateDbContext();
        var portfolio = await context.Portfolios.FirstOrDefaultAsync(p => p.StockId == stock.Id);

        Assert.NotNull(portfolio); // 紀錄仍存在
        Assert.Equal(0, portfolio.Quantity);
        Assert.True(portfolio.RealizedPnL > 0); // 保留已實現損益
    }

    [Fact]
    public async Task UpdatePortfolioAsync_WithSharedContext_ParticipatesInTransaction()
    {
        // Arrange
        var stock = await SeedStock();
        using var context = _factory.CreateDbContext();
        using var transaction = await context.Database.BeginTransactionAsync();

        // Act
        await _portfolioService.UpdatePortfolioAsync(
            stock.Id, 100, 50m, TradeSide.Buy, 71.25m, context);

        // 未提交交易，手動回滾
        await transaction.RollbackAsync();

        // Assert - 應該沒有儲存
        using var checkContext = _factory.CreateDbContext();
        var portfolio = await checkContext.Portfolios.FirstOrDefaultAsync(p => p.StockId == stock.Id);
        Assert.Null(portfolio);
    }

    #endregion

    #region Task 6.2: 損益計算功能

    [Fact]
    public async Task GetPortfolioAsync_CalculatesUnrealizedPnL()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 60m; // 當前市價 60
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        // 買入 100 股 @ 50，手續費 71.25，總成本 5071.25
        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 50m, TradeSide.Buy, 71.25m);

        // Act
        var portfolios = await _portfolioService.GetPortfolioAsync();

        // Assert
        var item = portfolios.Single();
        Assert.Equal(6000m, item.MarketValue); // 60 * 100

        // 未實現損益 = 市值 - 成本 - 預估賣出成本
        // 預估賣出成本 = 手續費 + 交易稅 = (6000 * 0.001425 * 0.6) + (6000 * 0.003) = 5.13 + 18 = 23.13
        // 實際最低手續費 20，所以是 20 + 18 = 38
        // 未實現損益 = 6000 - 5071.25 - 38 = 890.75
        Assert.Equal(890.75m, Math.Round(item.UnrealizedPnL, 2));
    }

    [Fact]
    public async Task GetPortfolioAsync_CalculatesReturnRate()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 60m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 50m, TradeSide.Buy, 71.25m);

        // Act
        var portfolios = await _portfolioService.GetPortfolioAsync();

        // Assert
        var item = portfolios.Single();
        // 報酬率 = (未實現損益 / 成本) * 100
        // = (890.75 / 5071.25) * 100 = 17.56%
        Assert.Equal(17.56m, Math.Round(item.ReturnRate, 2));
    }

    [Fact]
    public async Task GetPortfolioAsync_ExcludesZeroQuantityHoldings()
    {
        // Arrange
        var stock = await SeedStock();
        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 50m, TradeSide.Buy, 71.25m);
        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 60m, TradeSide.Sell, 85.50m);

        // Act
        var portfolios = await _portfolioService.GetPortfolioAsync();

        // Assert - 持股歸零後不應出現在清單中
        Assert.Empty(portfolios);
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_CalculatesTotalMarketValue()
    {
        // Arrange
        var stock1 = await SeedStock("2330", "台積電", 100m);
        var stock2 = await SeedStock("2317", "鴻海", 50m);

        await _portfolioService.UpdatePortfolioAsync(stock1.Id, 100, 90m, TradeSide.Buy, 128.25m);
        await _portfolioService.UpdatePortfolioAsync(stock2.Id, 200, 45m, TradeSide.Buy, 128.25m);

        // Act
        var summary = await _portfolioService.GetPortfolioSummaryAsync();

        // Assert
        // 台積電：100 * 100 = 10000
        // 鴻海：200 * 50 = 10000
        Assert.Equal(20000m, summary.TotalMarketValue);
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_SummarizesRealizedAndUnrealizedPnL()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 60m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        // 買入 200 股
        await _portfolioService.UpdatePortfolioAsync(stock.Id, 200, 50m, TradeSide.Buy, 142.5m);
        // 賣出 100 股（產生已實現損益）
        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 60m, TradeSide.Sell, 85.50m);

        // Act
        var summary = await _portfolioService.GetPortfolioSummaryAsync();

        // Assert
        Assert.True(summary.TotalRealizedPnL > 0); // 有已實現損益
        Assert.True(summary.TotalUnrealizedPnL > 0); // 有未實現損益（剩餘 100 股）
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_CalculatesTotalReturnRate()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 60m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 50m, TradeSide.Buy, 71.25m);

        // Act
        var summary = await _portfolioService.GetPortfolioSummaryAsync();

        // Assert - 總報酬率應該與單一持股報酬率一致
        Assert.True(summary.TotalReturnRate > 0);
    }

    #endregion

    #region Task 6.3: 即時損益更新

    [Fact]
    public async Task GetPortfolioAsync_UsesLatestStockPrice()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 50m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 50m, TradeSide.Buy, 71.25m);

        // 第一次查詢
        var portfolios1 = await _portfolioService.GetPortfolioAsync();
        var pnl1 = portfolios1.Single().UnrealizedPnL;

        // Act - 股價上漲
        stock.CurrentPrice = 60m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        // 第二次查詢（應該使用最新股價）
        var portfolios2 = await _portfolioService.GetPortfolioAsync();
        var pnl2 = portfolios2.Single().UnrealizedPnL;

        // Assert - 股價上漲，未實現損益應增加
        Assert.True(pnl2 > pnl1);
    }

    [Fact]
    public async Task GetPortfolioSummaryAsync_ReflectsLatestPrices()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 50m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        await _portfolioService.UpdatePortfolioAsync(stock.Id, 100, 50m, TradeSide.Buy, 71.25m);

        var summary1 = await _portfolioService.GetPortfolioSummaryAsync();

        // Act - 股價變動
        stock.CurrentPrice = 70m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        var summary2 = await _portfolioService.GetPortfolioSummaryAsync();

        // Assert - 總市值和損益應反映最新價格
        Assert.True(summary2.TotalMarketValue > summary1.TotalMarketValue);
        Assert.True(summary2.TotalUnrealizedPnL > summary1.TotalUnrealizedPnL);
    }

    [Fact]
    public async Task GetPortfolioAsync_MultipleStocks_CalculatesCorrectly()
    {
        // Arrange
        var stock1 = await SeedStock("2330", "台積電", 100m);
        var stock2 = await SeedStock("2317", "鴻海", 50m);
        var stock3 = await SeedStock("2454", "聯發科", 800m);

        await _portfolioService.UpdatePortfolioAsync(stock1.Id, 100, 90m, TradeSide.Buy, 128.25m);
        await _portfolioService.UpdatePortfolioAsync(stock2.Id, 200, 45m, TradeSide.Buy, 128.25m);
        await _portfolioService.UpdatePortfolioAsync(stock3.Id, 10, 750m, TradeSide.Buy, 106.88m);

        // Act
        var portfolios = await _portfolioService.GetPortfolioAsync();

        // Assert
        Assert.Equal(3, portfolios.Count);
        Assert.All(portfolios, p =>
        {
            Assert.True(p.Quantity > 0);
            Assert.True(p.MarketValue > 0);
            Assert.NotEqual(0, p.UnrealizedPnL); // 應該有損益（正或負）
        });
    }

    #endregion

    #region Helper Methods

    private async Task<Stock> SeedStock(string symbol = "2330", string name = "台積電", decimal currentPrice = 100m)
    {
        using var context = _factory.CreateDbContext();
        var stock = new Stock
        {
            Symbol = symbol,
            Name = name,
            Market = MarketType.Listed,
            Industry = "半導體",
            CurrentPrice = currentPrice,
            OpenPrice = currentPrice * 0.98m,
            HighPrice = currentPrice * 1.02m,
            LowPrice = currentPrice * 0.97m,
            Volume = 10000000,
            LastUpdated = DateTime.UtcNow
        };
        context.Stocks.Add(stock);
        await context.SaveChangesAsync();
        return stock;
    }

    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(_options);
        }
    }

    #endregion
}

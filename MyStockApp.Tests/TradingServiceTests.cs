using Microsoft.EntityFrameworkCore;
using MyStockApp.Data;
using MyStockApp.Data.Models;
using MyStockApp.Services;

namespace MyStockApp.Tests;

public class TradingServiceTests
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ITradingService _tradingService;
    private readonly IMarketHoursService _marketHoursService;
    private readonly ITradingCostService _tradingCostService;
    private readonly IStockService _stockService;
    private readonly IAuditService _auditService;
    private readonly IPortfolioService _portfolioService;

    public TradingServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"TradingTestDb_{Guid.NewGuid()}")
            .Options;

        _factory = new TestDbContextFactory(options);
        _marketHoursService = new MarketHoursService();
        _tradingCostService = new TradingCostService();
        _stockService = new StockService(_factory);
        _auditService = new AuditService(_factory);
        _portfolioService = new PortfolioService(_factory, _tradingCostService, _stockService);
        _tradingService = new TradingService(
            _factory,
            _tradingCostService,
            _portfolioService,
            _stockService,
            _marketHoursService,
            _auditService
        );
    }

    #region Task 5.1: 訂單建立與驗證邏輯

    [Fact]
    public async Task CreateOrderAsync_WithInvalidQuantity_ReturnsInvalidQuantityError()
    {
        // Arrange
        var stock = await SeedStock();
        var request = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Market, 0);

        // Act
        var result = await _tradingService.CreateOrderAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TradingError.InvalidQuantity, result.Error);
    }

    [Fact]
    public async Task CreateOrderAsync_WithNegativeQuantity_ReturnsInvalidQuantityError()
    {
        // Arrange
        var stock = await SeedStock();
        var request = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Market, -10);

        // Act
        var result = await _tradingService.CreateOrderAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TradingError.InvalidQuantity, result.Error);
    }

    [Fact]
    public async Task CreateOrderAsync_WithInvalidStock_ReturnsInvalidStockError()
    {
        // Arrange
        var request = new CreateOrderRequest(99999, OrderSide.Buy, OrderType.Market, 100);

        // Act
        var result = await _tradingService.CreateOrderAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TradingError.InvalidStock, result.Error);
    }

    [Fact]
    public async Task CreateOrderAsync_LimitOrderWithoutPrice_ReturnsInvalidLimitPriceError()
    {
        // Arrange
        var stock = await SeedStock();
        var request = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Limit, 100, null);

        // Act
        var result = await _tradingService.CreateOrderAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TradingError.InvalidLimitPrice, result.Error);
    }

    [Fact]
    public async Task CreateOrderAsync_SellExceedsHoldings_ReturnsInsufficientHoldingsError()
    {
        // Arrange
        var stock = await SeedStock();
        await SeedPortfolio(stock.Id, 50); // 持有 50 股
        var request = new CreateOrderRequest(stock.Id, OrderSide.Sell, OrderType.Market, 100);

        // Act
        var result = await _tradingService.CreateOrderAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TradingError.InsufficientHoldings, result.Error);
    }

    [Fact]
    public async Task CreateOrderAsync_DuplicateOrderWithinTimeWindow_ReturnsDuplicateOrderError()
    {
        // Arrange
        var stock = await SeedStock();
        var request = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Market, 100);

        // Act
        var result1 = await _tradingService.CreateOrderAsync(request);
        await Task.Delay(100); // 短時間內
        var result2 = await _tradingService.CreateOrderAsync(request);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.False(result2.IsSuccess);
        Assert.Equal(TradingError.DuplicateOrder, result2.Error);
    }

    #endregion

    #region Task 5.2: 市價單下單流程

    [Fact]
    public async Task CreateOrderAsync_MarketOrderDuringTradingHours_ExecutesImmediately()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 100m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        var request = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Market, 10);

        // Act - 僅在交易時段測試
        if (!_marketHoursService.IsMarketOpen())
        {
            return; // Skip test if not market hours
        }

        var result = await _tradingService.CreateOrderAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Executed, result.Value!.Status);

        // 驗證 Trade 已建立
        using var context2 = _factory.CreateDbContext();
        var trades = await context2.Trades.Where(t => t.OrderId == result.Value.Id).ToListAsync();
        Assert.Single(trades);
    }

    [Fact]
    public async Task CreateOrderAsync_MarketOrderOutsideTradingHours_StaysPending()
    {
        // Arrange
        var stock = await SeedStock();
        var request = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Market, 10);

        // Act - 僅在休市時段測試
        if (_marketHoursService.IsMarketOpen())
        {
            return; // Skip test if market is open
        }

        var result = await _tradingService.CreateOrderAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Pending, result.Value!.Status);

        // 驗證 Trade 未建立
        using var context = _factory.CreateDbContext();
        var trades = await context.Trades.Where(t => t.OrderId == result.Value.Id).ToListAsync();
        Assert.Empty(trades);
    }

    [Fact]
    public async Task CreateOrderAsync_BuyOrder_UpdatesPortfolio()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 100m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        var request = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Market, 10);

        // Act
        if (!_marketHoursService.IsMarketOpen())
        {
            return;
        }

        var result = await _tradingService.CreateOrderAsync(request);

        // Assert
        using var context2 = _factory.CreateDbContext();
        var portfolio = await context2.Portfolios.FirstOrDefaultAsync(p => p.StockId == stock.Id);
        Assert.NotNull(portfolio);
        Assert.Equal(10, portfolio.Quantity);
    }

    #endregion

    #region Task 5.3: 限價單下單流程

    [Fact]
    public async Task CreateOrderAsync_LimitOrder_StaysPending()
    {
        // Arrange
        var stock = await SeedStock();
        var request = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Limit, 10, 95m);

        // Act
        var result = await _tradingService.CreateOrderAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Pending, result.Value!.Status);
        Assert.Equal(95m, result.Value.Price);
    }

    [Fact]
    public async Task ExecuteMatchAsync_BuyLimitOrderTriggered_ExecutesTrade()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 100m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        var request = new CreateOrderRequest(stock.Id, OrderSide.Buy, OrderType.Limit, 10, 100m);
        var orderResult = await _tradingService.CreateOrderAsync(request);

        // Act - 市價 100，限價 100，觸發買入
        var matchResult = await _tradingService.ExecuteMatchAsync(orderResult.Value!.Id, 100m);

        // Assert
        Assert.True(matchResult.IsSuccess);

        using var context2 = _factory.CreateDbContext();
        var order = await context2.Orders.FindAsync(orderResult.Value.Id);
        Assert.Equal(OrderStatus.Executed, order!.Status);
    }

    [Fact]
    public async Task ExecuteMatchAsync_SellLimitOrderTriggered_ExecutesTrade()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 100m;
        await SeedPortfolio(stock.Id, 50);
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        var request = new CreateOrderRequest(stock.Id, OrderSide.Sell, OrderType.Limit, 10, 100m);
        var orderResult = await _tradingService.CreateOrderAsync(request);

        // Act - 市價 100，限價 100，觸發賣出
        var matchResult = await _tradingService.ExecuteMatchAsync(orderResult.Value!.Id, 100m);

        // Assert
        Assert.True(matchResult.IsSuccess);
    }

    #endregion

    #region Task 5.4: 訂單查詢與篩選

    [Fact]
    public async Task GetOrdersAsync_NoFilter_ReturnsAllOrders()
    {
        // Arrange
        var stock = await SeedStock();
        await SeedOrders(stock.Id, 5);

        // Act
        var orders = await _tradingService.GetOrdersAsync();

        // Assert
        Assert.Equal(5, orders.Count);
    }

    [Fact]
    public async Task GetOrdersAsync_FilterByStatus_ReturnsMatchingOrders()
    {
        // Arrange
        var stock = await SeedStock();
        await SeedOrders(stock.Id, 3, OrderStatus.Pending);
        await SeedOrders(stock.Id, 2, OrderStatus.Executed);

        // Act
        var orders = await _tradingService.GetOrdersAsync(new OrderFilter(Status: OrderStatus.Pending));

        // Assert
        Assert.Equal(3, orders.Count);
        Assert.All(orders, o => Assert.Equal(OrderStatus.Pending, o.Status));
    }

    [Fact]
    public async Task GetOrdersAsync_OrdersByCreatedAtDescending()
    {
        // Arrange
        var stock = await SeedStock();
        await SeedOrders(stock.Id, 5);

        // Act
        var orders = await _tradingService.GetOrdersAsync();

        // Assert
        for (int i = 0; i < orders.Count - 1; i++)
        {
            Assert.True(orders[i].CreatedAt >= orders[i + 1].CreatedAt);
        }
    }

    #endregion

    #region Task 5.5: 訂單取消功能

    [Fact]
    public async Task CancelOrderAsync_PendingOrder_CancelsSuccessfully()
    {
        // Arrange
        var stock = await SeedStock();
        var order = await SeedSingleOrder(stock.Id, OrderStatus.Pending);

        // Act
        var result = await _tradingService.CancelOrderAsync(order.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, result.Value!.Status);
    }

    [Fact]
    public async Task CancelOrderAsync_ExecutedOrder_ReturnsOrderNotCancellableError()
    {
        // Arrange
        var stock = await SeedStock();
        var order = await SeedSingleOrder(stock.Id, OrderStatus.Executed);

        // Act
        var result = await _tradingService.CancelOrderAsync(order.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TradingError.OrderNotCancellable, result.Error);
    }

    [Fact]
    public async Task CancelOrderAsync_CancelledOrder_ReturnsOrderNotCancellableError()
    {
        // Arrange
        var stock = await SeedStock();
        var order = await SeedSingleOrder(stock.Id, OrderStatus.Cancelled);

        // Act
        var result = await _tradingService.CancelOrderAsync(order.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TradingError.OrderNotCancellable, result.Error);
    }

    [Fact]
    public async Task CancelOrderAsync_NonExistentOrder_ReturnsOrderNotFoundError()
    {
        // Act
        var result = await _tradingService.CancelOrderAsync(99999);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TradingError.OrderNotFound, result.Error);
    }

    #endregion

    #region Task 5.6: 待成交訂單成交處理

    [Fact]
    public async Task ProcessPendingOrdersAsync_MarketOrdersDuringTradingHours_ExecutesAll()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 100m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);
            await context.SaveChangesAsync();
        }

        await SeedOrders(stock.Id, 3, OrderStatus.Pending, OrderType.Market);

        // Act
        if (!_marketHoursService.IsMarketOpen())
        {
            return;
        }

        await _tradingService.ProcessPendingOrdersAsync();

        // Assert
        using var context2 = _factory.CreateDbContext();
        var pendingOrders = await context2.Orders.Where(o => o.Status == OrderStatus.Pending).ToListAsync();
        Assert.Empty(pendingOrders);
    }

    [Fact]
    public async Task ProcessPendingOrdersAsync_LimitOrdersTriggered_ExecutesMatching()
    {
        // Arrange
        var stock = await SeedStock();
        stock.CurrentPrice = 100m;
        using (var context = _factory.CreateDbContext())
        {
            context.Stocks.Update(stock);

            // 建立限價單：買入限價 100（應觸發）、101（不觸發）
            context.Orders.Add(new Order
            {
                StockId = stock.Id,
                Side = OrderSide.Buy,
                Type = OrderType.Limit,
                Quantity = 10,
                Price = 100m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            context.Orders.Add(new Order
            {
                StockId = stock.Id,
                Side = OrderSide.Buy,
                Type = OrderType.Limit,
                Quantity = 10,
                Price = 101m,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }

        // Act
        if (!_marketHoursService.IsMarketOpen())
        {
            return;
        }

        await _tradingService.ProcessPendingOrdersAsync();

        // Assert
        using var context2 = _factory.CreateDbContext();
        var orders = await context2.Orders.ToListAsync();
        Assert.Single(orders.Where(o => o.Status == OrderStatus.Executed));
        Assert.Single(orders.Where(o => o.Status == OrderStatus.Pending));
    }

    #endregion

    #region Helper Methods

    private async Task<Stock> SeedStock(string symbol = "2330", string name = "台積電")
    {
        using var context = _factory.CreateDbContext();
        var stock = new Stock
        {
            Symbol = symbol,
            Name = name,
            Market = MarketType.Listed,
            Industry = "半導體",
            CurrentPrice = 100m,
            OpenPrice = 98m,
            HighPrice = 102m,
            LowPrice = 97m,
            Volume = 10000000,
            LastUpdated = DateTime.UtcNow
        };
        context.Stocks.Add(stock);
        await context.SaveChangesAsync();
        return stock;
    }

    private async Task SeedPortfolio(int stockId, int quantity)
    {
        using var context = _factory.CreateDbContext();
        context.Portfolios.Add(new Portfolio
        {
            StockId = stockId,
            Quantity = quantity,
            AverageCost = 50m,
            TotalCost = 50m * quantity,
            RealizedPnL = 0,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private async Task SeedOrders(int stockId, int count, OrderStatus status = OrderStatus.Pending, OrderType type = OrderType.Market)
    {
        using var context = _factory.CreateDbContext();
        for (int i = 0; i < count; i++)
        {
            context.Orders.Add(new Order
            {
                StockId = stockId,
                Side = OrderSide.Buy,
                Type = type,
                Quantity = 10,
                Price = type == OrderType.Limit ? 95m : 0m,
                Status = status,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await context.SaveChangesAsync();
    }

    private async Task<Order> SeedSingleOrder(int stockId, OrderStatus status)
    {
        using var context = _factory.CreateDbContext();
        var order = new Order
        {
            StockId = stockId,
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 10,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    #endregion

    #region Task 8.1: 交易紀錄查詢與篩選

    [Fact]
    public async Task GetTradesAsync_WithNoFilter_ReturnsAllTrades()
    {
        // Arrange
        var stock = await SeedStock();
        await SeedTradesForFiltering(stock);

        // Act
        var result = await _tradingService.GetTradesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 3); // 至少包含我們建立的 3 筆
    }

    [Fact]
    public async Task GetTradesAsync_FilterByStockSymbol_ReturnsMatchingTrades()
    {
        // Arrange
        var stock1 = await SeedStock("2330", "台積電");
        var stock2 = await SeedStock("2317", "鴻海");

        await SeedTradeForStock(stock1.Id, stock1.Symbol);
        await SeedTradeForStock(stock2.Id, stock2.Symbol);

        // Act
        var filter = new TradeFilter(StockSymbol: "2330");
        var result = await _tradingService.GetTradesAsync(filter);

        // Assert
        Assert.NotNull(result);
        Assert.All(result, t => Assert.Equal("2330", t.StockSymbol));
    }

    [Fact]
    public async Task GetTradesAsync_FilterByDateRange_ReturnsMatchingTrades()
    {
        // Arrange
        var stock = await SeedStock();
        var now = DateTime.UtcNow;

        // 建立不同時間的交易紀錄
        await SeedTradeWithDate(stock.Id, stock.Symbol, now.AddDays(-10));
        await SeedTradeWithDate(stock.Id, stock.Symbol, now.AddDays(-5));
        await SeedTradeWithDate(stock.Id, stock.Symbol, now.AddDays(-1));

        // Act - 查詢最近 7 天
        var filter = new TradeFilter(FromDate: now.AddDays(-7));
        var result = await _tradingService.GetTradesAsync(filter);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2); // 應該包含 -5 天和 -1 天的交易
        Assert.All(result, t => Assert.True(t.ExecutedAt >= now.AddDays(-7)));
    }

    [Fact]
    public async Task GetTradesAsync_FilterByFromAndToDate_ReturnsMatchingTrades()
    {
        // Arrange
        var stock = await SeedStock();
        var now = DateTime.UtcNow;

        await SeedTradeWithDate(stock.Id, stock.Symbol, now.AddDays(-15));
        await SeedTradeWithDate(stock.Id, stock.Symbol, now.AddDays(-10));
        await SeedTradeWithDate(stock.Id, stock.Symbol, now.AddDays(-5));
        await SeedTradeWithDate(stock.Id, stock.Symbol, now.AddDays(-1));

        // Act - 查詢 12 天前到 4 天前
        var filter = new TradeFilter(
            FromDate: now.AddDays(-12),
            ToDate: now.AddDays(-4)
        );
        var result = await _tradingService.GetTradesAsync(filter);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 2); // 應該包含 -10 天和 -5 天的交易
        Assert.All(result, t =>
        {
            Assert.True(t.ExecutedAt >= now.AddDays(-12));
            Assert.True(t.ExecutedAt <= now.AddDays(-4));
        });
    }

    [Fact]
    public async Task GetTradesAsync_OrderedByExecutedAtDescending()
    {
        // Arrange
        var stock = await SeedStock();
        var now = DateTime.UtcNow;

        await SeedTradeWithDate(stock.Id, stock.Symbol, now.AddDays(-10));
        await SeedTradeWithDate(stock.Id, stock.Symbol, now.AddDays(-5));
        await SeedTradeWithDate(stock.Id, stock.Symbol, now.AddDays(-1));

        // Act
        var result = await _tradingService.GetTradesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 3);

        // 驗證按照 ExecutedAt 降序排列（最新在前）
        for (int i = 0; i < result.Count - 1; i++)
        {
            Assert.True(result[i].ExecutedAt >= result[i + 1].ExecutedAt,
                $"Trade at index {i} should have ExecutedAt >= Trade at index {i + 1}");
        }
    }

    [Fact]
    public async Task GetTradesAsync_CombinedFilters_ReturnsMatchingTrades()
    {
        // Arrange
        var stock1 = await SeedStock("2330", "台積電");
        var stock2 = await SeedStock("2317", "鴻海");
        var now = DateTime.UtcNow;

        await SeedTradeWithDate(stock1.Id, stock1.Symbol, now.AddDays(-10));
        await SeedTradeWithDate(stock1.Id, stock1.Symbol, now.AddDays(-5));
        await SeedTradeWithDate(stock2.Id, stock2.Symbol, now.AddDays(-5));

        // Act - 組合條件：特定股票 + 日期範圍
        var filter = new TradeFilter(
            StockSymbol: "2330",
            FromDate: now.AddDays(-7)
        );
        var result = await _tradingService.GetTradesAsync(filter);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 1); // 應該只有 2330 在 7 天內的交易
        Assert.All(result, t =>
        {
            Assert.Equal("2330", t.StockSymbol);
            Assert.True(t.ExecutedAt >= now.AddDays(-7));
        });
    }

    [Fact]
    public async Task GetTradesAsync_IncludesOrderAndStockDetails()
    {
        // Arrange
        var stock = await SeedStock("2330", "台積電");
        await SeedTradeForStock(stock.Id, stock.Symbol);

        // Act
        var result = await _tradingService.GetTradesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        var trade = result.First(t => t.StockSymbol == "2330");
        Assert.NotNull(trade.Order);
        Assert.NotNull(trade.Order.Stock);
        Assert.Equal("2330", trade.Order.Stock.Symbol);
        Assert.Equal("台積電", trade.Order.Stock.Name);
    }

    #endregion

    #region Helper Methods for Task 8

    private async Task SeedTradesForFiltering(Stock stock)
    {
        using var context = _factory.CreateDbContext();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 3; i++)
        {
            var order = new Order
            {
                StockId = stock.Id,
                Side = OrderSide.Buy,
                Type = OrderType.Market,
                Quantity = 100,
                Price = 500m,
                Status = OrderStatus.Executed,
                CreatedAt = now.AddMinutes(-i * 10),
                UpdatedAt = now.AddMinutes(-i * 10)
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var trade = new Trade
            {
                OrderId = order.Id,
                StockSymbol = stock.Symbol,
                Side = TradeSide.Buy,
                Quantity = 100,
                ExecutedPrice = 500m,
                TotalAmount = 50000m,
                Commission = 85.5m,
                TransactionTax = 0m,
                NetAmount = 50085.5m,
                ExecutedAt = now.AddMinutes(-i * 10)
            };
            context.Trades.Add(trade);
        }
        await context.SaveChangesAsync();
    }

    private async Task SeedTradeForStock(int stockId, string stockSymbol)
    {
        using var context = _factory.CreateDbContext();

        var order = new Order
        {
            StockId = stockId,
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 100,
            Price = 500m,
            Status = OrderStatus.Executed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var trade = new Trade
        {
            OrderId = order.Id,
            StockSymbol = stockSymbol,
            Side = TradeSide.Buy,
            Quantity = 100,
            ExecutedPrice = 500m,
            TotalAmount = 50000m,
            Commission = 85.5m,
            TransactionTax = 0m,
            NetAmount = 50085.5m,
            ExecutedAt = DateTime.UtcNow
        };
        context.Trades.Add(trade);
        await context.SaveChangesAsync();
    }

    private async Task SeedTradeWithDate(int stockId, string stockSymbol, DateTime executedAt)
    {
        using var context = _factory.CreateDbContext();

        var order = new Order
        {
            StockId = stockId,
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 100,
            Price = 500m,
            Status = OrderStatus.Executed,
            CreatedAt = executedAt,
            UpdatedAt = executedAt
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var trade = new Trade
        {
            OrderId = order.Id,
            StockSymbol = stockSymbol,
            Side = TradeSide.Buy,
            Quantity = 100,
            ExecutedPrice = 500m,
            TotalAmount = 50000m,
            Commission = 85.5m,
            TransactionTax = 0m,
            NetAmount = 50085.5m,
            ExecutedAt = executedAt
        };
        context.Trades.Add(trade);
        await context.SaveChangesAsync();
    }

    #endregion

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

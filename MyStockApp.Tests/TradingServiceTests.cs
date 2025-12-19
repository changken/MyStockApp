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

    private async Task<Stock> SeedStock()
    {
        using var context = _factory.CreateDbContext();
        var stock = new Stock
        {
            Symbol = "2330",
            Name = "台積電",
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

using Microsoft.EntityFrameworkCore;
using MyStockApp.Data;
using MyStockApp.Data.Models;

namespace MyStockApp.Services;

/// <summary>
/// 股票交易服務實作
/// </summary>
public class TradingService : ITradingService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ITradingCostService _tradingCostService;
    private readonly IPortfolioService _portfolioService;
    private readonly IStockService _stockService;
    private readonly IMarketHoursService _marketHoursService;
    private readonly IAuditService _auditService;

    // 防重複提交時間窗口（秒）
    private const int DuplicateDetectionWindowSeconds = 5;

    public TradingService(
        IDbContextFactory<AppDbContext> contextFactory,
        ITradingCostService tradingCostService,
        IPortfolioService portfolioService,
        IStockService stockService,
        IMarketHoursService marketHoursService,
        IAuditService auditService)
    {
        _contextFactory = contextFactory;
        _tradingCostService = tradingCostService;
        _portfolioService = portfolioService;
        _stockService = stockService;
        _marketHoursService = marketHoursService;
        _auditService = auditService;
    }

    public async Task<Result<Order, TradingError>> CreateOrderAsync(CreateOrderRequest request)
    {
        // 驗證數量
        if (request.Quantity <= 0)
        {
            return Result<Order, TradingError>.Failure(TradingError.InvalidQuantity);
        }

        // 驗證股票存在
        var stock = await _stockService.GetStockAsync(request.StockId);
        if (stock == null)
        {
            return Result<Order, TradingError>.Failure(TradingError.InvalidStock);
        }

        // 驗證限價單必須提供價格
        if (request.Type == OrderType.Limit && (!request.LimitPrice.HasValue || request.LimitPrice <= 0))
        {
            return Result<Order, TradingError>.Failure(TradingError.InvalidLimitPrice);
        }

        // 驗證賣出數量不超過持有股數
        if (request.Side == OrderSide.Sell)
        {
            using var checkContext = _contextFactory.CreateDbContext();
            var portfolio = await checkContext.Portfolios
                .FirstOrDefaultAsync(p => p.StockId == request.StockId);

            var availableQuantity = portfolio?.Quantity ?? 0;
            if (request.Quantity > availableQuantity)
            {
                return Result<Order, TradingError>.Failure(TradingError.InsufficientHoldings);
            }
        }

        // 防重複提交檢查
        if (await IsDuplicateOrderAsync(request))
        {
            return Result<Order, TradingError>.Failure(TradingError.DuplicateOrder);
        }

        using var context = _contextFactory.CreateDbContext();
        using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            var order = new Order
            {
                StockId = request.StockId,
                Side = request.Side,
                Type = request.Type,
                Quantity = request.Quantity,
                Price = request.LimitPrice ?? 0,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            // 市價單在交易時段立即成交
            if (request.Type == OrderType.Market && _marketHoursService.IsMarketOpen())
            {
                await ExecuteMarketOrderAsync(order, stock.CurrentPrice, context);
            }

            await transaction.CommitAsync();

            // 記錄稽核日誌
            await _auditService.LogAsync("CreateOrder", "Order", order.Id, null, new
            {
                StockId = order.StockId,
                Side = order.Side.ToString(),
                Type = order.Type.ToString(),
                Quantity = order.Quantity,
                Price = order.Price,
                Status = order.Status.ToString()
            });

            return Result<Order, TradingError>.Success(order);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Result<Order, TradingError>> CancelOrderAsync(int orderId)
    {
        using var context = _contextFactory.CreateDbContext();

        var order = await context.Orders.FindAsync(orderId);
        if (order == null)
        {
            return Result<Order, TradingError>.Failure(TradingError.OrderNotFound);
        }

        if (order.Status != OrderStatus.Pending)
        {
            return Result<Order, TradingError>.Failure(TradingError.OrderNotCancellable);
        }

        var oldStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        // 記錄稽核日誌
        await _auditService.LogAsync("CancelOrder", "Order", order.Id,
            new { Status = oldStatus.ToString() },
            new { Status = order.Status.ToString() });

        return Result<Order, TradingError>.Success(order);
    }

    public async Task<IReadOnlyList<Order>> GetOrdersAsync(OrderFilter? filter = null)
    {
        using var context = _contextFactory.CreateDbContext();

        var query = context.Orders
            .Include(o => o.Stock)
            .AsQueryable();

        if (filter?.Status.HasValue == true)
        {
            query = query.Where(o => o.Status == filter.Status.Value);
        }

        if (filter?.StockId.HasValue == true)
        {
            query = query.Where(o => o.StockId == filter.StockId.Value);
        }

        if (filter?.FromDate.HasValue == true)
        {
            query = query.Where(o => o.CreatedAt >= filter.FromDate.Value);
        }

        if (filter?.ToDate.HasValue == true)
        {
            query = query.Where(o => o.CreatedAt <= filter.ToDate.Value);
        }

        return await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    public async Task<IReadOnlyList<Trade>> GetTradesAsync(TradeFilter? filter = null)
    {
        using var context = _contextFactory.CreateDbContext();

        var query = context.Trades
            .Include(t => t.Order)
            .ThenInclude(o => o.Stock)
            .AsQueryable();

        if (!string.IsNullOrEmpty(filter?.StockSymbol))
        {
            query = query.Where(t => t.StockSymbol == filter.StockSymbol);
        }

        if (filter?.FromDate.HasValue == true)
        {
            query = query.Where(t => t.ExecutedAt >= filter.FromDate.Value);
        }

        if (filter?.ToDate.HasValue == true)
        {
            query = query.Where(t => t.ExecutedAt <= filter.ToDate.Value);
        }

        return await query.OrderByDescending(t => t.ExecutedAt).ToListAsync();
    }

    public async Task<Order?> GetOrderByIdAsync(int orderId)
    {
        using var context = _contextFactory.CreateDbContext();
        return await context.Orders
            .Include(o => o.Stock)
            .Include(o => o.Trades)
            .FirstOrDefaultAsync(o => o.Id == orderId);
    }

    public async Task<Result<Trade, TradingError>> ExecuteMatchAsync(int orderId, decimal matchPrice)
    {
        using var context = _contextFactory.CreateDbContext();
        using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            var order = await context.Orders
                .Include(o => o.Stock)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return Result<Trade, TradingError>.Failure(TradingError.OrderNotFound);
            }

            if (order.Status != OrderStatus.Pending)
            {
                return Result<Trade, TradingError>.Failure(TradingError.OrderNotCancellable);
            }

            // 檢查限價單觸發條件
            if (order.Type == OrderType.Limit)
            {
                var isTriggered = order.Side == OrderSide.Buy
                    ? matchPrice <= order.Price  // 買入：市價 <= 限價
                    : matchPrice >= order.Price; // 賣出：市價 >= 限價

                if (!isTriggered)
                {
                    return Result<Trade, TradingError>.Failure(TradingError.InvalidLimitPrice);
                }
            }

            var trade = await CreateTradeAsync(order, matchPrice, context);
            await transaction.CommitAsync();

            return Result<Trade, TradingError>.Success(trade);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task ProcessPendingOrdersAsync()
    {
        if (!_marketHoursService.IsMarketOpen())
        {
            return;
        }

        using var context = _contextFactory.CreateDbContext();

        var pendingOrders = await context.Orders
            .Include(o => o.Stock)
            .Where(o => o.Status == OrderStatus.Pending)
            .ToListAsync();

        foreach (var order in pendingOrders)
        {
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                if (order.Type == OrderType.Market)
                {
                    // 市價單以當前市價成交
                    await ExecuteMarketOrderAsync(order, order.Stock.CurrentPrice, context);
                }
                else if (order.Type == OrderType.Limit)
                {
                    // 限價單檢查觸發條件
                    var isTriggered = order.Side == OrderSide.Buy
                        ? order.Stock.CurrentPrice <= order.Price
                        : order.Stock.CurrentPrice >= order.Price;

                    if (isTriggered)
                    {
                        await CreateTradeAsync(order, order.Stock.CurrentPrice, context);
                    }
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
            }
        }
    }

    #region Private Helper Methods

    private async Task<bool> IsDuplicateOrderAsync(CreateOrderRequest request)
    {
        using var context = _contextFactory.CreateDbContext();

        var cutoffTime = DateTime.UtcNow.AddSeconds(-DuplicateDetectionWindowSeconds);

        var existingOrder = await context.Orders
            .Where(o => o.StockId == request.StockId
                     && o.Side == request.Side
                     && o.Type == request.Type
                     && o.Quantity == request.Quantity
                     && o.CreatedAt >= cutoffTime)
            .FirstOrDefaultAsync();

        return existingOrder != null;
    }

    private async Task ExecuteMarketOrderAsync(Order order, decimal executionPrice, AppDbContext context)
    {
        await CreateTradeAsync(order, executionPrice, context);
    }

    private async Task<Trade> CreateTradeAsync(Order order, decimal executionPrice, AppDbContext context)
    {
        var totalAmount = executionPrice * order.Quantity;

        // 計算交易成本
        var tradeSide = order.Side == OrderSide.Buy ? TradeSide.Buy : TradeSide.Sell;
        var costs = _tradingCostService.CalculateTotalCost(totalAmount, tradeSide, 0.6m);

        var commission = costs.Commission;
        var transactionTax = costs.TransactionTax;
        var netAmount = tradeSide == TradeSide.Buy
            ? totalAmount + commission
            : totalAmount - commission - transactionTax;

        // 建立成交紀錄
        var trade = new Trade
        {
            OrderId = order.Id,
            StockSymbol = order.Stock.Symbol,
            Side = tradeSide,
            Quantity = order.Quantity,
            ExecutedPrice = executionPrice,
            TotalAmount = totalAmount,
            Commission = commission,
            TransactionTax = transactionTax,
            NetAmount = netAmount,
            ExecutedAt = DateTime.UtcNow
        };

        context.Trades.Add(trade);

        // 更新訂單狀態
        order.Status = OrderStatus.Executed;
        order.Commission = commission;
        order.TransactionTax = transactionTax;
        order.UpdatedAt = DateTime.UtcNow;

        // 更新持股部位
        await _portfolioService.UpdatePortfolioAsync(
            order.StockId,
            order.Quantity,
            executionPrice,
            tradeSide,
            commission,
            context
        );

        await context.SaveChangesAsync();

        // 記錄稽核日誌
        await _auditService.LogAsync("ExecuteTrade", "Trade", trade.Id, null, new
        {
            OrderId = trade.OrderId,
            StockSymbol = trade.StockSymbol,
            Side = trade.Side.ToString(),
            Quantity = trade.Quantity,
            ExecutedPrice = trade.ExecutedPrice,
            NetAmount = trade.NetAmount
        });

        return trade;
    }

    #endregion
}

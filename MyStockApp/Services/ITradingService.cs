using MyStockApp.Data.Models;

namespace MyStockApp.Services;

/// <summary>
/// 股票交易服務介面
/// </summary>
public interface ITradingService
{
    /// <summary>
    /// 建立訂單
    /// </summary>
    Task<Result<Order, TradingError>> CreateOrderAsync(CreateOrderRequest request);

    /// <summary>
    /// 取消訂單
    /// </summary>
    Task<Result<Order, TradingError>> CancelOrderAsync(int orderId);

    /// <summary>
    /// 查詢訂單清單
    /// </summary>
    Task<IReadOnlyList<Order>> GetOrdersAsync(OrderFilter? filter = null);

    /// <summary>
    /// 查詢交易紀錄
    /// </summary>
    Task<IReadOnlyList<Trade>> GetTradesAsync(TradeFilter? filter = null);

    /// <summary>
    /// 依 ID 查詢訂單
    /// </summary>
    Task<Order?> GetOrderByIdAsync(int orderId);

    /// <summary>
    /// 執行限價單撮合（供 LimitOrderMatcher 呼叫）
    /// </summary>
    Task<Result<Trade, TradingError>> ExecuteMatchAsync(int orderId, decimal matchPrice);

    /// <summary>
    /// 處理待成交訂單（手動觸發）
    /// </summary>
    Task ProcessPendingOrdersAsync();
}

/// <summary>
/// 建立訂單請求
/// </summary>
public record CreateOrderRequest(
    int StockId,
    OrderSide Side,
    OrderType Type,
    int Quantity,
    decimal? LimitPrice = null
);

/// <summary>
/// 訂單篩選條件
/// </summary>
public record OrderFilter(
    OrderStatus? Status = null,
    int? StockId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
);

/// <summary>
/// 交易紀錄篩選條件
/// </summary>
public record TradeFilter(
    string? StockSymbol = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
);

/// <summary>
/// 交易錯誤類型
/// </summary>
public enum TradingError
{
    InvalidQuantity,
    InsufficientHoldings,
    InvalidStock,
    InvalidLimitPrice,
    OrderNotFound,
    OrderNotCancellable,
    DuplicateOrder,
    ExceedsTradeLimit
}

/// <summary>
/// Result 型別用於錯誤處理
/// </summary>
public class Result<TValue, TError>
{
    public TValue? Value { get; }
    public TError? Error { get; }
    public bool IsSuccess { get; }

    private Result(TValue value)
    {
        Value = value;
        IsSuccess = true;
    }

    private Result(TError error)
    {
        Error = error;
        IsSuccess = false;
    }

    public static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error);
}

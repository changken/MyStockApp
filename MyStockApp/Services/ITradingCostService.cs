using MyStockApp.Data.Models;

namespace MyStockApp.Services;

public interface ITradingCostService
{
    /// <summary>計算手續費（已套用最低門檻）</summary>
    decimal CalculateCommission(decimal amount, decimal discountRate = 0.6m);

    /// <summary>計算證券交易稅（僅賣出適用）</summary>
    decimal CalculateTransactionTax(decimal amount);

    /// <summary>計算完整交易成本</summary>
    TradingCost CalculateTotalCost(decimal amount, TradeSide side, decimal discountRate = 0.6m);

    /// <summary>預估持股損益（含賣出成本）</summary>
    PnLEstimate EstimatePnL(decimal currentPrice, int quantity, decimal averageCost, decimal discountRate = 0.6m);
}

public record TradingCost(
    decimal Commission,
    decimal TransactionTax,
    decimal TotalCost
);

public record PnLEstimate(
    decimal MarketValue,
    decimal TotalCost,
    decimal UnrealizedPnL,
    decimal ReturnRate
);

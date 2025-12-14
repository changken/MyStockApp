namespace MyStockApp.Data.Models
{
    public enum OrderSide
    {
        Buy = 1,
        Sell = 2
    }

    public enum OrderType
    {
        Market = 1,
        Limit = 2
    }

    public enum OrderStatus
    {
        Pending = 1,
        Executed = 2,
        Cancelled = 3
    }

    public enum MarketType
    {
        Listed = 1,
        OverTheCounter = 2
    }

    public enum TradeSide
    {
        Buy = 1,
        Sell = 2
    }
}

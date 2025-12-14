namespace MyStockApp.Data.Models
{
    public class StockPriceHistory
    {
        public int Id { get; set; }

        public int StockId { get; set; }

        public Stock Stock { get; set; } = default!;

        public DateOnly Date { get; set; }

        public decimal OpenPrice { get; set; }

        public decimal HighPrice { get; set; }

        public decimal LowPrice { get; set; }

        public decimal ClosePrice { get; set; }

        public long Volume { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace MyStockApp.Data.Models
{
    public class Order
    {
        public int Id { get; set; }

        public int StockId { get; set; }

        public Stock Stock { get; set; } = default!;

        public OrderSide Side { get; set; }

        public OrderType Type { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "交易數量必須大於 0")]
        public int Quantity { get; set; }

        public decimal Price { get; set; }

        public OrderStatus Status { get; set; }

        public decimal Commission { get; set; }

        public decimal TransactionTax { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<Trade> Trades { get; set; } = new List<Trade>();
    }
}


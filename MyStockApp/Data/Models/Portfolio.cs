using System.ComponentModel.DataAnnotations;

namespace MyStockApp.Data.Models
{
    public class Portfolio
    {
        public int Id { get; set; }

        public int StockId { get; set; }

        public Stock Stock { get; set; } = default!;

        [Range(0, int.MaxValue, ErrorMessage = "持有數量不可為負數")]
        public int Quantity { get; set; }

        public decimal AverageCost { get; set; }

        public decimal TotalCost { get; set; }

        public decimal RealizedPnL { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}


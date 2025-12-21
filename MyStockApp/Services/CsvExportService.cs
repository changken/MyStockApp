using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.JSInterop;
using MyStockApp.Data.Models;
using System.Globalization;
using System.Text;

namespace MyStockApp.Services
{
    /// <summary>
    /// CSV 匯出服務實作
    /// </summary>
    public class CsvExportService : ICsvExportService
    {
        private readonly IJSRuntime _jsRuntime;

        public CsvExportService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// 匯出交易紀錄為 CSV 檔案
        /// </summary>
        public async Task ExportTradesAsync(IEnumerable<Trade> trades, string fileName = "trades.csv")
        {
            // 產生 CSV 內容
            var csvContent = GenerateCsvContent(trades);

            // 轉換為 Base64
            var base64 = Convert.ToBase64String(csvContent);

            // 透過 JavaScript Interop 觸發下載
            await _jsRuntime.InvokeVoidAsync(
                "downloadFileFromBase64",
                fileName,
                "text/csv",
                base64);
        }

        /// <summary>
        /// 產生 CSV 內容（含 UTF-8 BOM）
        /// </summary>
        private byte[] GenerateCsvContent(IEnumerable<Trade> trades)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream, new UTF8Encoding(true)); // UTF-8 with BOM
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            });

            // 註冊類別對應
            csv.Context.RegisterClassMap<TradeMap>();

            // 寫入資料
            csv.WriteRecords(trades);
            writer.Flush();

            return memoryStream.ToArray();
        }
    }

    /// <summary>
    /// Trade 實體的 CSV 欄位對應
    /// </summary>
    public class TradeMap : ClassMap<Trade>
    {
        public TradeMap()
        {
            Map(m => m.ExecutedAt).Name("交易日期").TypeConverterOption.Format("yyyy-MM-dd HH:mm:ss");
            Map(m => m.StockSymbol).Name("股票代號");
            Map(m => m.Side).Name("交易類型").TypeConverter<TradeSideConverter>();
            Map(m => m.Quantity).Name("數量");
            Map(m => m.ExecutedPrice).Name("成交價格");
            Map(m => m.TotalAmount).Name("成交金額");
            Map(m => m.Commission).Name("手續費");
            Map(m => m.TransactionTax).Name("交易稅");
            Map(m => m.NetAmount).Name("淨金額");
        }
    }

    /// <summary>
    /// TradeSide 轉換器（買入/賣出）
    /// </summary>
    public class TradeSideConverter : CsvHelper.TypeConversion.DefaultTypeConverter
    {
        public override string? ConvertToString(object? value, CsvHelper.IWriterRow row, CsvHelper.Configuration.MemberMapData memberMapData)
        {
            if (value is TradeSide side)
            {
                return side switch
                {
                    TradeSide.Buy => "買入",
                    TradeSide.Sell => "賣出",
                    _ => value.ToString()
                };
            }
            return base.ConvertToString(value, row, memberMapData);
        }
    }
}

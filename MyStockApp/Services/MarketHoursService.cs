namespace MyStockApp.Services;

/// <summary>
/// 台股市場交易時段判斷服務實作
/// </summary>
public class MarketHoursService : IMarketHoursService
{
    private static readonly TimeZoneInfo TaipeiTimeZone;
    private static readonly HashSet<DateOnly> Holidays2025;

    static MarketHoursService()
    {
        // 兼容 Windows/Linux 時區 ID
        try
        {
            TaipeiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        }
        catch
        {
            TaipeiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");
        }

        // 初始化 2025 年台灣股市假日清單
        Holidays2025 = new HashSet<DateOnly>
        {
            // 元旦
            new DateOnly(2025, 1, 1),

            // 農曆春節 (1/28 除夕調整放假, 1/29-1/30 春節, 1/31 春節補假, 2/1-2/3 假日)
            new DateOnly(2025, 1, 28),
            new DateOnly(2025, 1, 29),
            new DateOnly(2025, 1, 30),
            new DateOnly(2025, 1, 31),
            new DateOnly(2025, 2, 1),
            new DateOnly(2025, 2, 2),
            new DateOnly(2025, 2, 3),

            // 和平紀念日
            new DateOnly(2025, 2, 28),

            // 兒童節/清明節
            new DateOnly(2025, 4, 3),
            new DateOnly(2025, 4, 4),
            new DateOnly(2025, 4, 5),
            new DateOnly(2025, 4, 7), // 補假

            // 勞動節
            new DateOnly(2025, 5, 1),

            // 端午節
            new DateOnly(2025, 5, 31),
            new DateOnly(2025, 6, 2), // 補假

            // 中秋節
            new DateOnly(2025, 10, 6),
            new DateOnly(2025, 10, 7), // 補假

            // 國慶日
            new DateOnly(2025, 10, 10),
            new DateOnly(2025, 10, 11), // 補假
        };
    }

    public bool IsMarketOpen(DateTime? dateTime = null)
    {
        var utcNow = dateTime ?? DateTime.UtcNow;
        var taipeiTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TaipeiTimeZone);

        // 檢查是否為週末
        if (taipeiTime.DayOfWeek == DayOfWeek.Saturday || taipeiTime.DayOfWeek == DayOfWeek.Sunday)
        {
            return false;
        }

        // 檢查是否為假日
        var date = DateOnly.FromDateTime(taipeiTime);
        if (IsHoliday(date))
        {
            return false;
        }

        // 檢查交易時段（09:00-13:25）
        var timeOfDay = taipeiTime.TimeOfDay;
        var marketOpen = new TimeSpan(9, 0, 0);
        var marketClose = new TimeSpan(13, 25, 0);

        return timeOfDay >= marketOpen && timeOfDay <= marketClose;
    }

    public DateTime? GetNextMarketOpen(DateTime? from = null)
    {
        var utcFrom = from ?? DateTime.UtcNow;
        var taipeiTime = TimeZoneInfo.ConvertTimeFromUtc(utcFrom, TaipeiTimeZone);

        // 從隔天開始尋找
        var nextDay = taipeiTime.Date.AddDays(1);

        // 最多搜尋 30 天
        for (int i = 0; i < 30; i++)
        {
            var candidate = nextDay.AddDays(i);

            // 跳過週末
            if (candidate.DayOfWeek == DayOfWeek.Saturday || candidate.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            // 跳過假日
            var date = DateOnly.FromDateTime(candidate);
            if (IsHoliday(date))
            {
                continue;
            }

            // 找到下一個開盤日，返回該日 09:00 台北時間
            var nextOpenTaipei = candidate.AddHours(9);
            return TimeZoneInfo.ConvertTimeToUtc(nextOpenTaipei, TaipeiTimeZone);
        }

        return null;
    }

    public bool IsHoliday(DateOnly date)
    {
        return Holidays2025.Contains(date);
    }
}

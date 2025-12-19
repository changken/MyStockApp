using MyStockApp.Services;

namespace MyStockApp.Tests;

public class MarketHoursServiceTests
{
    private readonly IMarketHoursService _service;

    public MarketHoursServiceTests()
    {
        _service = new MarketHoursService();
    }

    [Fact]
    public void IsMarketOpen_DuringTradingHours_ReturnsTrue()
    {
        // 週三 10:00 台北時間
        var tradingTime = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);
        var taipeiTime = TimeZoneInfo.ConvertTimeToUtc(tradingTime, GetTaipeiTimeZone());

        var result = _service.IsMarketOpen(taipeiTime);

        Assert.True(result);
    }

    [Fact]
    public void IsMarketOpen_BeforeMarketOpen_ReturnsFalse()
    {
        // 週三 08:30 台北時間（開盤前）
        var beforeOpen = new DateTime(2025, 1, 15, 8, 30, 0, DateTimeKind.Unspecified);
        var taipeiTime = TimeZoneInfo.ConvertTimeToUtc(beforeOpen, GetTaipeiTimeZone());

        var result = _service.IsMarketOpen(taipeiTime);

        Assert.False(result);
    }

    [Fact]
    public void IsMarketOpen_AfterMarketClose_ReturnsFalse()
    {
        // 週三 13:30 台北時間（收盤後）
        var afterClose = new DateTime(2025, 1, 15, 13, 30, 0, DateTimeKind.Unspecified);
        var taipeiTime = TimeZoneInfo.ConvertTimeToUtc(afterClose, GetTaipeiTimeZone());

        var result = _service.IsMarketOpen(taipeiTime);

        Assert.False(result);
    }

    [Fact]
    public void IsMarketOpen_OnWeekend_ReturnsFalse()
    {
        // 週六 10:00 台北時間
        var saturday = new DateTime(2025, 1, 18, 10, 0, 0, DateTimeKind.Unspecified);
        var taipeiTime = TimeZoneInfo.ConvertTimeToUtc(saturday, GetTaipeiTimeZone());

        var result = _service.IsMarketOpen(taipeiTime);

        Assert.False(result);
    }

    [Fact]
    public void IsMarketOpen_OnHoliday_ReturnsFalse()
    {
        // 2025/1/1 元旦（週三）10:00 台北時間
        var holiday = new DateTime(2025, 1, 1, 10, 0, 0, DateTimeKind.Unspecified);
        var taipeiTime = TimeZoneInfo.ConvertTimeToUtc(holiday, GetTaipeiTimeZone());

        var result = _service.IsMarketOpen(taipeiTime);

        Assert.False(result);
    }

    [Fact]
    public void IsMarketOpen_AtMarketOpen_ReturnsTrue()
    {
        // 週三 09:00 台北時間（開盤瞬間）
        var marketOpen = new DateTime(2025, 1, 15, 9, 0, 0, DateTimeKind.Unspecified);
        var taipeiTime = TimeZoneInfo.ConvertTimeToUtc(marketOpen, GetTaipeiTimeZone());

        var result = _service.IsMarketOpen(taipeiTime);

        Assert.True(result);
    }

    [Fact]
    public void IsMarketOpen_AtMarketClose_ReturnsFalse()
    {
        // 週三 13:25 台北時間（收盤瞬間，13:25 後關閉）
        var marketClose = new DateTime(2025, 1, 15, 13, 26, 0, DateTimeKind.Unspecified);
        var taipeiTime = TimeZoneInfo.ConvertTimeToUtc(marketClose, GetTaipeiTimeZone());

        var result = _service.IsMarketOpen(taipeiTime);

        Assert.False(result);
    }

    [Fact]
    public void IsHoliday_NewYearDay_ReturnsTrue()
    {
        var newYear = new DateOnly(2025, 1, 1);

        var result = _service.IsHoliday(newYear);

        Assert.True(result);
    }

    [Fact]
    public void IsHoliday_LunarNewYear_ReturnsTrue()
    {
        // 2025 農曆春節：1/28-1/30
        var lunarNewYear = new DateOnly(2025, 1, 29);

        var result = _service.IsHoliday(lunarNewYear);

        Assert.True(result);
    }

    [Fact]
    public void IsHoliday_RegularWeekday_ReturnsFalse()
    {
        var regularDay = new DateOnly(2025, 1, 15);

        var result = _service.IsHoliday(regularDay);

        Assert.False(result);
    }

    [Fact]
    public void GetNextMarketOpen_FromWeekday_ReturnsNextDay()
    {
        // 週三 15:00
        var wednesday = new DateTime(2025, 1, 15, 15, 0, 0, DateTimeKind.Unspecified);
        var taipeiTime = TimeZoneInfo.ConvertTimeToUtc(wednesday, GetTaipeiTimeZone());

        var nextOpen = _service.GetNextMarketOpen(taipeiTime);

        Assert.NotNull(nextOpen);
        // 應該返回隔天週四 09:00 台北時間
        var expected = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2025, 1, 16, 9, 0, 0, DateTimeKind.Unspecified),
            GetTaipeiTimeZone());
        Assert.Equal(expected, nextOpen.Value);
    }

    [Fact]
    public void GetNextMarketOpen_FromFriday_ReturnsMonday()
    {
        // 週五 15:00
        var friday = new DateTime(2025, 1, 17, 15, 0, 0, DateTimeKind.Unspecified);
        var taipeiTime = TimeZoneInfo.ConvertTimeToUtc(friday, GetTaipeiTimeZone());

        var nextOpen = _service.GetNextMarketOpen(taipeiTime);

        Assert.NotNull(nextOpen);
        // 應該跳過週末，返回下週一 09:00
        var expected = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2025, 1, 20, 9, 0, 0, DateTimeKind.Unspecified),
            GetTaipeiTimeZone());
        Assert.Equal(expected, nextOpen.Value);
    }

    [Fact]
    public void GetNextMarketOpen_BeforeHoliday_SkipsHoliday()
    {
        // 12/31 (週三) 15:00，1/1 元旦假日
        var beforeHoliday = new DateTime(2024, 12, 31, 15, 0, 0, DateTimeKind.Unspecified);
        var taipeiTime = TimeZoneInfo.ConvertTimeToUtc(beforeHoliday, GetTaipeiTimeZone());

        var nextOpen = _service.GetNextMarketOpen(taipeiTime);

        Assert.NotNull(nextOpen);
        // 應該跳過 1/1 假日，返回 1/2 (週四) 09:00
        var expected = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(2025, 1, 2, 9, 0, 0, DateTimeKind.Unspecified),
            GetTaipeiTimeZone());
        Assert.Equal(expected, nextOpen.Value);
    }

    private static TimeZoneInfo GetTaipeiTimeZone()
    {
        try
        {
            // Linux/macOS: Asia/Taipei
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        }
        catch
        {
            // Windows: Taipei Standard Time
            return TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");
        }
    }
}

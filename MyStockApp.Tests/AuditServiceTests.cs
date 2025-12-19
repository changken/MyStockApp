using Microsoft.EntityFrameworkCore;
using MyStockApp.Services;
using MyStockApp.Data;
using MyStockApp.Data.Models;

namespace MyStockApp.Tests;

public class AuditServiceTests
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IAuditService _service;

    public AuditServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"AuditTestDb_{Guid.NewGuid()}")
            .Options;

        _factory = new TestDbContextFactory(options);
        _service = new AuditService(_factory);
    }

    [Fact]
    public async Task LogAsync_CreatesAuditLogEntry()
    {
        // Arrange
        var action = "Create";
        var entityType = "Order";
        var entityId = 123;

        // Act
        await _service.LogAsync(action, entityType, entityId);

        // Assert
        using var context = _factory.CreateDbContext();
        var log = await context.AuditLogs.FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.Equal(action, log.Action);
        Assert.Equal(entityType, log.EntityType);
        Assert.Equal(entityId, log.EntityId);
        Assert.Null(log.OldValue);
        Assert.Null(log.NewValue);
    }

    [Fact]
    public async Task LogAsync_WithOldAndNewValues_SerializesToJson()
    {
        // Arrange
        var action = "Update";
        var entityType = "Portfolio";
        var entityId = 456;
        var oldValue = new { Quantity = 100, AverageCost = 50.5m };
        var newValue = new { Quantity = 150, AverageCost = 52.3m };

        // Act
        await _service.LogAsync(action, entityType, entityId, oldValue, newValue);

        // Assert
        using var context = _factory.CreateDbContext();
        var log = await context.AuditLogs.FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.NotNull(log.OldValue);
        Assert.NotNull(log.NewValue);
        Assert.Contains("\"Quantity\":100", log.OldValue);
        Assert.Contains("\"AverageCost\":50.5", log.OldValue);
        Assert.Contains("\"Quantity\":150", log.NewValue);
        Assert.Contains("\"AverageCost\":52.3", log.NewValue);
    }

    [Fact]
    public async Task LogAsync_SetsCreatedAtTimestamp()
    {
        // Arrange
        var beforeLog = DateTime.UtcNow;
        await Task.Delay(10); // 確保時間差異

        // Act
        await _service.LogAsync("Delete", "Trade", 789);

        // Assert
        using var context = _factory.CreateDbContext();
        var log = await context.AuditLogs.FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.True(log.CreatedAt >= beforeLog);
        Assert.True(log.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task LogAsync_MultipleEntries_AllPersisted()
    {
        // Arrange & Act
        await _service.LogAsync("Create", "Order", 1);
        await _service.LogAsync("Update", "Order", 1);
        await _service.LogAsync("Cancel", "Order", 1);

        // Assert
        using var context = _factory.CreateDbContext();
        var logs = await context.AuditLogs.ToListAsync();

        Assert.Equal(3, logs.Count);
        Assert.Equal("Create", logs[0].Action);
        Assert.Equal("Update", logs[1].Action);
        Assert.Equal("Cancel", logs[2].Action);
    }

    [Fact]
    public async Task LogAsync_WithNullValues_HandlesCorrectly()
    {
        // Act
        await _service.LogAsync("Delete", "Stock", 100, null, null);

        // Assert
        using var context = _factory.CreateDbContext();
        var log = await context.AuditLogs.FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.Null(log.OldValue);
        Assert.Null(log.NewValue);
    }

    [Fact]
    public async Task LogAsync_WithComplexObject_SerializesCorrectly()
    {
        // Arrange
        var complexObject = new
        {
            Id = 1,
            Symbol = "2330",
            Name = "台積電",
            Market = "Listed",
            Prices = new[] { 100.5m, 101.0m, 102.5m },
            Metadata = new { Industry = "半導體", IsActive = true }
        };

        // Act
        await _service.LogAsync("Create", "Stock", 1, null, complexObject);

        // Assert
        using var context = _factory.CreateDbContext();
        var log = await context.AuditLogs.FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.NotNull(log.NewValue);
        Assert.Contains("\"Symbol\":\"2330\"", log.NewValue);
        Assert.Contains("\"Name\":\"台積電\"", log.NewValue);
        Assert.Contains("\"Industry\":\"半導體\"", log.NewValue);
    }

    // Helper class for test DbContext factory
    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(_options);
        }
    }
}

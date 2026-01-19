using AlphaSqueeze.Core.Entities;
using FluentAssertions;

namespace AlphaSqueeze.Tests.Entities;

/// <summary>
/// DailyStockMetric 實體測試
/// </summary>
public class DailyStockMetricTests
{
    [Fact]
    public void DailyStockMetric_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var metric = new DailyStockMetric();

        // Assert
        metric.Id.Should().Be(0);
        metric.Ticker.Should().BeEmpty();
        metric.ClosePrice.Should().BeNull();
        metric.BorrowingBalanceChange.Should().BeNull();
        metric.MarginRatio.Should().BeNull();
    }

    [Fact]
    public void DailyStockMetric_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var tradeDate = new DateTime(2024, 1, 15);

        // Act
        var metric = new DailyStockMetric
        {
            Id = 1,
            Ticker = "2330",
            TradeDate = tradeDate,
            ClosePrice = 600.00m,
            OpenPrice = 595.00m,
            HighPrice = 605.00m,
            LowPrice = 590.00m,
            BorrowingBalance = 1000000,
            BorrowingBalanceChange = -50000,
            MarginBalance = 5000000,
            ShortBalance = 800000,
            MarginRatio = 16.00m,
            HistoricalVolatility20D = 0.25m,
            Volume = 50000000,
            Turnover = 30000000000
        };

        // Assert
        metric.Id.Should().Be(1);
        metric.Ticker.Should().Be("2330");
        metric.TradeDate.Should().Be(tradeDate);
        metric.ClosePrice.Should().Be(600.00m);
        metric.BorrowingBalanceChange.Should().Be(-50000);
        metric.MarginRatio.Should().Be(16.00m);
    }

    [Fact]
    public void DailyStockMetric_MarginRatio_ShouldAllowHighValues()
    {
        // Arrange & Act
        var metric = new DailyStockMetric
        {
            MarginRatio = 35.5m // 極端擁擠的空單
        };

        // Assert
        metric.MarginRatio.Should().Be(35.5m);
        metric.MarginRatio.Should().BeGreaterThan(30m); // 超過 30% 為極度擁擠
    }
}

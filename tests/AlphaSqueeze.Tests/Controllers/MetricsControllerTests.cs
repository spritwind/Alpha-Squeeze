using AlphaSqueeze.Api.Controllers;
using AlphaSqueeze.Api.Models;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AlphaSqueeze.Tests.Controllers;

/// <summary>
/// MetricsController 單元測試
/// </summary>
public class MetricsControllerTests
{
    private readonly Mock<IStockMetricsRepository> _repoMock;
    private readonly Mock<ILogger<MetricsController>> _loggerMock;
    private readonly MetricsController _controller;

    public MetricsControllerTests()
    {
        _repoMock = new Mock<IStockMetricsRepository>();
        _loggerMock = new Mock<ILogger<MetricsController>>();
        _controller = new MetricsController(_repoMock.Object, _loggerMock.Object);
    }

    #region GetByDate Tests

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetByDate_ReturnsMetricsForToday()
    {
        // Arrange
        var metrics = new List<DailyStockMetric>
        {
            new()
            {
                Ticker = "2330",
                TradeDate = DateTime.Today,
                ClosePrice = 500m,
                MarginRatio = 15.5m,
                Volume = 10000000
            },
            new()
            {
                Ticker = "2454",
                TradeDate = DateTime.Today,
                ClosePrice = 800m,
                MarginRatio = 12.3m,
                Volume = 5000000
            }
        };

        _repoMock
            .Setup(x => x.GetByDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetByDate();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<StockMetricDto>>().Subject;
        dtos.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetByDate_WithSpecificDate_ReturnsMetricsForThatDate()
    {
        // Arrange
        var targetDate = new DateTime(2025, 1, 15);
        var metrics = new List<DailyStockMetric>
        {
            new()
            {
                Ticker = "2330",
                TradeDate = targetDate,
                ClosePrice = 500m
            }
        };

        _repoMock
            .Setup(x => x.GetByDateAsync(targetDate))
            .ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetByDate(date: targetDate);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<StockMetricDto>>().Subject;
        dtos.Should().HaveCount(1);
        dtos.First().TradeDate.Should().Be(targetDate);
    }

    #endregion

    #region GetByTicker Tests

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetByTicker_WhenFound_ReturnsMetric()
    {
        // Arrange
        var metric = new DailyStockMetric
        {
            Ticker = "2330",
            TradeDate = DateTime.Today,
            ClosePrice = 500m,
            OpenPrice = 495m,
            HighPrice = 510m,
            LowPrice = 490m,
            MarginRatio = 15.5m,
            BorrowingBalanceChange = -1000,
            Volume = 10000000
        };

        _repoMock
            .Setup(x => x.GetByTickerAndDateAsync("2330", It.IsAny<DateTime>()))
            .ReturnsAsync(metric);

        // Act
        var result = await _controller.GetByTicker("2330");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<StockMetricDto>().Subject;
        dto.Ticker.Should().Be("2330");
        dto.ClosePrice.Should().Be(500m);
        dto.MarginRatio.Should().Be(15.5m);
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetByTicker_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        _repoMock
            .Setup(x => x.GetByTickerAndDateAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((DailyStockMetric?)null);

        // Act
        var result = await _controller.GetByTicker("INVALID");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetByTicker_ConvertsToUppercase()
    {
        // Arrange
        var metric = new DailyStockMetric { Ticker = "2330", TradeDate = DateTime.Today };

        _repoMock
            .Setup(x => x.GetByTickerAndDateAsync("2330", It.IsAny<DateTime>()))
            .ReturnsAsync(metric);

        // Act - 使用小寫
        var result = await _controller.GetByTicker("2330");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _repoMock.Verify(x => x.GetByTickerAndDateAsync("2330", It.IsAny<DateTime>()), Times.Once);
    }

    #endregion

    #region GetHistory Tests

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetHistory_ReturnsHistoricalData()
    {
        // Arrange
        var history = new List<DailyStockMetric>
        {
            new() { Ticker = "2330", TradeDate = DateTime.Today },
            new() { Ticker = "2330", TradeDate = DateTime.Today.AddDays(-1) },
            new() { Ticker = "2330", TradeDate = DateTime.Today.AddDays(-2) }
        };

        _repoMock
            .Setup(x => x.GetHistoryAsync("2330", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(history);

        // Act
        var result = await _controller.GetHistory("2330", days: 30);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<StockMetricDto>>().Subject;
        dtos.Should().HaveCount(3);
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetHistory_WithInvalidDays_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetHistory("2330", days: 0);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetHistory_WithTooManyDays_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetHistory("2330", days: 400);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region GetHighMarginRatio Tests

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetHighMarginRatio_FiltersAndSortsByMarginRatio()
    {
        // Arrange
        var metrics = new List<DailyStockMetric>
        {
            new() { Ticker = "AAA", MarginRatio = 25m },
            new() { Ticker = "BBB", MarginRatio = 15m },
            new() { Ticker = "CCC", MarginRatio = 5m },  // 低於門檻
            new() { Ticker = "DDD", MarginRatio = 20m }
        };

        _repoMock
            .Setup(x => x.GetByDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetHighMarginRatio(minRatio: 10m, limit: 20);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<StockMetricDto>>().Subject.ToList();
        dtos.Should().HaveCount(3);
        dtos[0].Ticker.Should().Be("AAA"); // 25% 最高
        dtos[1].Ticker.Should().Be("DDD"); // 20%
        dtos[2].Ticker.Should().Be("BBB"); // 15%
    }

    #endregion

    #region GetShortCovering Tests

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetShortCovering_FiltersByNegativeBorrowingChange()
    {
        // Arrange
        var metrics = new List<DailyStockMetric>
        {
            new() { Ticker = "AAA", BorrowingBalanceChange = -5000 },
            new() { Ticker = "BBB", BorrowingBalanceChange = 1000 },  // 正值，不包含
            new() { Ticker = "CCC", BorrowingBalanceChange = -3000 },
            new() { Ticker = "DDD", BorrowingBalanceChange = -8000 }
        };

        _repoMock
            .Setup(x => x.GetByDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetShortCovering(limit: 20);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = okResult.Value.Should().BeAssignableTo<IEnumerable<StockMetricDto>>().Subject.ToList();
        dtos.Should().HaveCount(3);
        dtos[0].Ticker.Should().Be("DDD"); // -8000 最負
        dtos[1].Ticker.Should().Be("AAA"); // -5000
        dtos[2].Ticker.Should().Be("CCC"); // -3000
    }

    #endregion
}

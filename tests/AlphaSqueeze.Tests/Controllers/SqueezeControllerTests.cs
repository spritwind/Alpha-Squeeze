using AlphaSqueeze.Api.Controllers;
using AlphaSqueeze.Api.Models;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;
using AlphaSqueeze.Shared.Grpc;
using AlphaSqueeze.Shared.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AlphaSqueeze.Tests.Controllers;

/// <summary>
/// SqueezeController 單元測試
/// </summary>
public class SqueezeControllerTests
{
    private readonly Mock<ISqueezeEngineClient> _engineClientMock;
    private readonly Mock<IStockMetricsRepository> _metricsRepoMock;
    private readonly Mock<ILogger<SqueezeController>> _loggerMock;
    private readonly SqueezeController _controller;

    public SqueezeControllerTests()
    {
        _engineClientMock = new Mock<ISqueezeEngineClient>();
        _metricsRepoMock = new Mock<IStockMetricsRepository>();
        _loggerMock = new Mock<ILogger<SqueezeController>>();

        _controller = new SqueezeController(
            _engineClientMock.Object,
            _metricsRepoMock.Object,
            _loggerMock.Object);
    }

    #region GetTopCandidates Tests

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetTopCandidates_WhenEngineAvailable_ReturnsOkWithCandidates()
    {
        // Arrange
        _engineClientMock.Setup(x => x.IsAvailable).Returns(true);

        var response = new TopCandidatesResponse
        {
            AnalysisDate = DateTime.Today.ToString("yyyy-MM-dd"),
            GeneratedAt = DateTime.Now.ToString("o")
        };
        response.Candidates.Add(new SqueezeResponse
        {
            Ticker = "2330",
            Score = 75,
            Trend = "BULLISH",
            Comment = "高軋空潛力"
        });

        _engineClientMock
            .Setup(x => x.GetTopCandidatesAsync(It.IsAny<TopCandidatesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetTopCandidates(limit: 10, minScore: 60);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<TopCandidatesDto>().Subject;
        dto.Candidates.Should().HaveCount(1);
        dto.Candidates[0].Ticker.Should().Be("2330");
        dto.Candidates[0].Score.Should().Be(75);
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetTopCandidates_WhenEngineUnavailable_ReturnsDegradedResponse()
    {
        // Arrange
        _engineClientMock.Setup(x => x.IsAvailable).Returns(false);

        var metrics = new List<DailyStockMetric>
        {
            new()
            {
                Ticker = "2330",
                TradeDate = DateTime.Today,
                MarginRatio = 15.5m
            },
            new()
            {
                Ticker = "2454",
                TradeDate = DateTime.Today,
                MarginRatio = 12.3m
            }
        };

        _metricsRepoMock
            .Setup(x => x.GetByDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetTopCandidates(limit: 10, minScore: 60);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<TopCandidatesDto>().Subject;
        dto.Candidates.Should().HaveCount(2);
        dto.Candidates.Should().AllSatisfy(c => c.Trend.Should().Be("DEGRADED"));
    }

    #endregion

    #region GetSqueezeSignal Tests

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetSqueezeSignal_WhenTickerNotFound_ReturnsNotFound()
    {
        // Arrange
        _metricsRepoMock
            .Setup(x => x.GetByTickerAndDateAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((DailyStockMetric?)null);

        // Act
        var result = await _controller.GetSqueezeSignal("INVALID");

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetSqueezeSignal_WhenEngineAvailable_ReturnsOkWithSignal()
    {
        // Arrange
        var metric = new DailyStockMetric
        {
            Ticker = "2330",
            TradeDate = DateTime.Today,
            ClosePrice = 500m,
            MarginRatio = 15.5m,
            BorrowingBalanceChange = -1000,
            HistoricalVolatility20D = 25m,
            Volume = 10000000
        };

        _metricsRepoMock
            .Setup(x => x.GetByTickerAndDateAsync("2330", It.IsAny<DateTime>()))
            .ReturnsAsync(metric);

        _engineClientMock.Setup(x => x.IsAvailable).Returns(true);

        var response = new SqueezeResponse
        {
            Ticker = "2330",
            Score = 80,
            Trend = "BULLISH",
            Comment = "軋空潛力高",
            Factors = new FactorScores
            {
                BorrowScore = 85,
                GammaScore = 70,
                MarginScore = 80,
                MomentumScore = 75
            }
        };

        _engineClientMock
            .Setup(x => x.GetSqueezeSignalAsync(It.IsAny<SqueezeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetSqueezeSignal("2330");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<SqueezeSignalDto>().Subject;
        dto.Ticker.Should().Be("2330");
        dto.Score.Should().Be(80);
        dto.Trend.Should().Be("BULLISH");
        dto.Factors.Should().NotBeNull();
        dto.Factors!.BorrowScore.Should().Be(85);
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetSqueezeSignal_WhenEngineUnavailable_ReturnsDegradedSignal()
    {
        // Arrange
        var metric = new DailyStockMetric
        {
            Ticker = "2330",
            TradeDate = DateTime.Today,
            MarginRatio = 15.5m,
            BorrowingBalanceChange = -1000
        };

        _metricsRepoMock
            .Setup(x => x.GetByTickerAndDateAsync("2330", It.IsAny<DateTime>()))
            .ReturnsAsync(metric);

        _engineClientMock.Setup(x => x.IsAvailable).Returns(false);

        // Act
        var result = await _controller.GetSqueezeSignal("2330");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<SqueezeSignalDto>().Subject;
        dto.Ticker.Should().Be("2330");
        dto.Score.Should().Be(0);
        dto.Trend.Should().Be("DEGRADED");
    }

    #endregion

    #region GetBatchSignals Tests

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetBatchSignals_WithEmptyTickers_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetBatchSignals(tickers: "");

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetBatchSignals_WithTooManyTickers_ReturnsBadRequest()
    {
        // Arrange
        var tickers = string.Join(",", Enumerable.Range(1, 60).Select(i => $"T{i}"));

        // Act
        var result = await _controller.GetBatchSignals(tickers: tickers);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetBatchSignals_WithValidTickers_ReturnsResults()
    {
        // Arrange
        var metric1 = new DailyStockMetric { Ticker = "2330", TradeDate = DateTime.Today, MarginRatio = 15m };
        var metric2 = new DailyStockMetric { Ticker = "2454", TradeDate = DateTime.Today, MarginRatio = 12m };

        _metricsRepoMock
            .Setup(x => x.GetByTickerAndDateAsync("2330", It.IsAny<DateTime>()))
            .ReturnsAsync(metric1);
        _metricsRepoMock
            .Setup(x => x.GetByTickerAndDateAsync("2454", It.IsAny<DateTime>()))
            .ReturnsAsync(metric2);

        _engineClientMock.Setup(x => x.IsAvailable).Returns(false);

        // Act
        var result = await _controller.GetBatchSignals("2330,2454");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = okResult.Value.Should().BeAssignableTo<IEnumerable<SqueezeSignalDto>>().Subject;
        list.Should().HaveCount(2);
    }

    #endregion

    #region GetHealth Tests

    [Fact]
    [Trait("Category", "Api")]
    public void GetHealth_WhenEngineAvailable_ReturnsHealthy()
    {
        // Arrange
        _engineClientMock.Setup(x => x.IsAvailable).Returns(true);

        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var health = okResult.Value;
        health.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Api")]
    public void GetHealth_WhenEngineUnavailable_ReturnsDegraded()
    {
        // Arrange
        _engineClientMock.Setup(x => x.IsAvailable).Returns(false);

        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    #endregion
}

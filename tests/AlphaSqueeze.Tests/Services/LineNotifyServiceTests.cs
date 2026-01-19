using AlphaSqueeze.Api.Models;
using AlphaSqueeze.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;

namespace AlphaSqueeze.Tests.Services;

/// <summary>
/// LineNotifyService 單元測試
/// </summary>
public class LineNotifyServiceTests
{
    private readonly Mock<ILogger<LineNotifyService>> _loggerMock;

    public LineNotifyServiceTests()
    {
        _loggerMock = new Mock<ILogger<LineNotifyService>>();
    }

    private IConfiguration CreateConfiguration(string? accessToken)
    {
        var config = new Dictionary<string, string?>
        {
            ["LineNotify:AccessToken"] = accessToken
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
    }

    private HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content = "")
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });

        return new HttpClient(handlerMock.Object);
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task SendSqueezeAlertAsync_WhenNotConfigured_LogsWarningAndSkips()
    {
        // Arrange
        var config = CreateConfiguration(null);
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var service = new LineNotifyService(httpClient, config, _loggerMock.Object);

        var candidates = new List<SqueezeSignalDto>
        {
            new() { Ticker = "2330", Score = 80, Trend = "BULLISH" }
        };

        // Act
        await service.SendSqueezeAlertAsync(candidates);

        // Assert - 不應該拋出例外，只記錄警告
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task SendSqueezeAlertAsync_WithEmptyList_LogsAndSkips()
    {
        // Arrange
        var config = CreateConfiguration("test-token");
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var service = new LineNotifyService(httpClient, config, _loggerMock.Object);

        // Act
        await service.SendSqueezeAlertAsync(Enumerable.Empty<SqueezeSignalDto>());

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("No candidates")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task SendSqueezeAlertAsync_OnSuccess_LogsSuccess()
    {
        // Arrange
        var config = CreateConfiguration("test-token");
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var service = new LineNotifyService(httpClient, config, _loggerMock.Object);

        var candidates = new List<SqueezeSignalDto>
        {
            new()
            {
                Ticker = "2330",
                Score = 80,
                Trend = "BULLISH",
                Comment = "高軋空潛力",
                Factors = new FactorScoresDto
                {
                    BorrowScore = 85,
                    GammaScore = 70,
                    MarginScore = 80,
                    MomentumScore = 75
                }
            }
        };

        // Act
        await service.SendSqueezeAlertAsync(candidates);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("successfully")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task SendSqueezeAlertAsync_OnFailure_ThrowsAndLogsError()
    {
        // Arrange
        var config = CreateConfiguration("test-token");
        var httpClient = CreateMockHttpClient(HttpStatusCode.Unauthorized, "Invalid token");
        var service = new LineNotifyService(httpClient, config, _loggerMock.Object);

        var candidates = new List<SqueezeSignalDto>
        {
            new() { Ticker = "2330", Score = 80, Trend = "BULLISH" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.SendSqueezeAlertAsync(candidates));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task SendMessageAsync_WithEmptyMessage_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateConfiguration("test-token");
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var service = new LineNotifyService(httpClient, config, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SendMessageAsync(""));
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task SendMessageAsync_WithWhitespaceMessage_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateConfiguration("test-token");
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var service = new LineNotifyService(httpClient, config, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SendMessageAsync("   "));
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task SendMessageAsync_WhenDisabled_SkipsWithoutError()
    {
        // Arrange
        var config = CreateConfiguration("");
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK);
        var service = new LineNotifyService(httpClient, config, _loggerMock.Object);

        // Act - 不應該拋出例外
        await service.SendMessageAsync("Test message");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("disabled")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}

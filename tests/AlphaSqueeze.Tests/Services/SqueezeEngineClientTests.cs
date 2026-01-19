using AlphaSqueeze.Shared.Grpc;
using AlphaSqueeze.Shared.Services;
using FluentAssertions;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Moq;

namespace AlphaSqueeze.Tests.Services;

/// <summary>
/// SqueezeEngineClient 單元測試
/// </summary>
public class SqueezeEngineClientTests
{
    private readonly Mock<SqueezeEngine.SqueezeEngineClient> _grpcClientMock;
    private readonly Mock<ILogger<SqueezeEngineClient>> _loggerMock;
    private readonly SqueezeEngineClient _client;

    public SqueezeEngineClientTests()
    {
        _grpcClientMock = new Mock<SqueezeEngine.SqueezeEngineClient>();
        _loggerMock = new Mock<ILogger<SqueezeEngineClient>>();
        _client = new SqueezeEngineClient(_grpcClientMock.Object, _loggerMock.Object);
    }

    [Fact]
    [Trait("Category", "Api")]
    public void IsAvailable_InitiallyTrue()
    {
        // Assert
        _client.IsAvailable.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetSqueezeSignalAsync_OnSuccess_ReturnsResponse()
    {
        // Arrange
        var request = new SqueezeRequest
        {
            Ticker = "2330",
            BorrowChange = -1000,
            MarginRatio = 15.5,
            Hv20D = 25.0,
            ClosePrice = 500,
            Volume = 10000000
        };

        var expectedResponse = new SqueezeResponse
        {
            Ticker = "2330",
            Score = 75,
            Trend = "BULLISH",
            Comment = "高軋空潛力"
        };

        var asyncUnaryCall = CreateAsyncUnaryCall(expectedResponse);

        _grpcClientMock
            .Setup(x => x.GetSqueezeSignalAsync(
                It.IsAny<SqueezeRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(asyncUnaryCall);

        // Act
        var result = await _client.GetSqueezeSignalAsync(request);

        // Assert
        result.Ticker.Should().Be("2330");
        result.Score.Should().Be(75);
        result.Trend.Should().Be("BULLISH");
        _client.IsAvailable.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetSqueezeSignalAsync_OnUnavailable_SetsIsAvailableFalse()
    {
        // Arrange
        var request = new SqueezeRequest { Ticker = "2330" };

        _grpcClientMock
            .Setup(x => x.GetSqueezeSignalAsync(
                It.IsAny<SqueezeRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateFailedAsyncUnaryCall<SqueezeResponse>(StatusCode.Unavailable));

        // Act & Assert
        await Assert.ThrowsAsync<RpcException>(
            () => _client.GetSqueezeSignalAsync(request));

        _client.IsAvailable.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetSqueezeSignalAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _client.GetSqueezeSignalAsync(null!));
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetBatchSignalsAsync_OnSuccess_ReturnsResponse()
    {
        // Arrange
        var request = new BatchSqueezeRequest();
        request.Requests.Add(new SqueezeRequest { Ticker = "2330" });

        var expectedResponse = new BatchSqueezeResponse();
        expectedResponse.Responses.Add(new SqueezeResponse
        {
            Ticker = "2330",
            Score = 75
        });

        var asyncUnaryCall = CreateAsyncUnaryCall(expectedResponse);

        _grpcClientMock
            .Setup(x => x.GetBatchSignalsAsync(
                It.IsAny<BatchSqueezeRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(asyncUnaryCall);

        // Act
        var result = await _client.GetBatchSignalsAsync(request);

        // Assert
        result.Responses.Should().HaveCount(1);
        result.Responses[0].Ticker.Should().Be("2330");
    }

    [Fact]
    [Trait("Category", "Api")]
    public async Task GetTopCandidatesAsync_OnSuccess_ReturnsResponse()
    {
        // Arrange
        var request = new TopCandidatesRequest
        {
            Limit = 10,
            MinScore = 60,
            Date = DateTime.Today.ToString("yyyy-MM-dd")
        };

        var expectedResponse = new TopCandidatesResponse
        {
            AnalysisDate = DateTime.Today.ToString("yyyy-MM-dd"),
            GeneratedAt = DateTime.Now.ToString("o")
        };
        expectedResponse.Candidates.Add(new SqueezeResponse
        {
            Ticker = "2330",
            Score = 80
        });

        var asyncUnaryCall = CreateAsyncUnaryCall(expectedResponse);

        _grpcClientMock
            .Setup(x => x.GetTopCandidatesAsync(
                It.IsAny<TopCandidatesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(asyncUnaryCall);

        // Act
        var result = await _client.GetTopCandidatesAsync(request);

        // Assert
        result.Candidates.Should().HaveCount(1);
        result.Candidates[0].Score.Should().Be(80);
        _client.IsAvailable.Should().BeTrue();
    }

    #region Helper Methods

    private static AsyncUnaryCall<T> CreateAsyncUnaryCall<T>(T response)
    {
        return new AsyncUnaryCall<T>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    private static AsyncUnaryCall<T> CreateFailedAsyncUnaryCall<T>(StatusCode statusCode)
    {
        var status = new Status(statusCode, "Test error");
        var exception = new RpcException(status);

        return new AsyncUnaryCall<T>(
            Task.FromException<T>(exception),
            Task.FromResult(new Metadata()),
            () => status,
            () => new Metadata(),
            () => { });
    }

    #endregion
}

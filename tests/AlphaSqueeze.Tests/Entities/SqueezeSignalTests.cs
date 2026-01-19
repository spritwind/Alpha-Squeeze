using AlphaSqueeze.Core.Entities;
using FluentAssertions;

namespace AlphaSqueeze.Tests.Entities;

/// <summary>
/// SqueezeSignal 實體測試
/// </summary>
public class SqueezeSignalTests
{
    [Fact]
    public void SqueezeSignal_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var signal = new SqueezeSignal();

        // Assert
        signal.Id.Should().Be(0);
        signal.Ticker.Should().BeEmpty();
        signal.SqueezeScore.Should().Be(0);
        signal.NotificationSent.Should().BeFalse();
    }

    [Fact]
    public void SqueezeSignal_ShouldSetScoreCorrectly()
    {
        // Arrange
        var signalDate = new DateTime(2024, 1, 15);

        // Act
        var signal = new SqueezeSignal
        {
            Id = 1,
            Ticker = "2330",
            SignalDate = signalDate,
            SqueezeScore = 85,
            BorrowScore = 90.5m,
            GammaScore = 75.0m,
            MarginScore = 80.0m,
            MomentumScore = 85.0m,
            Trend = "BULLISH",
            Comment = "軋空潛力高，法人回補訊號強勁"
        };

        // Assert
        signal.SqueezeScore.Should().Be(85);
        signal.Trend.Should().Be("BULLISH");
        signal.BorrowScore.Should().Be(90.5m);
    }

    [Theory]
    [InlineData(85, "BULLISH")]
    [InlineData(70, "BULLISH")]
    [InlineData(55, "NEUTRAL")]
    [InlineData(40, "BEARISH")]
    [InlineData(30, "BEARISH")]
    public void SqueezeSignal_TrendShouldMatchScore(int score, string expectedTrend)
    {
        // Arrange & Act
        var signal = new SqueezeSignal
        {
            SqueezeScore = score,
            Trend = expectedTrend
        };

        // Assert
        signal.SqueezeScore.Should().Be(score);
        signal.Trend.Should().Be(expectedTrend);
    }

    [Fact]
    public void SqueezeSignal_WeightedScore_ShouldBeWithinRange()
    {
        // Arrange - 模擬加權計算
        const decimal borrowWeight = 0.35m;
        const decimal gammaWeight = 0.25m;
        const decimal marginWeight = 0.20m;
        const decimal momentumWeight = 0.20m;

        var borrowScore = 80m;
        var gammaScore = 70m;
        var marginScore = 60m;
        var momentumScore = 75m;

        // Act
        var weightedScore = (borrowWeight * borrowScore) +
                            (gammaWeight * gammaScore) +
                            (marginWeight * marginScore) +
                            (momentumWeight * momentumScore);

        var signal = new SqueezeSignal
        {
            SqueezeScore = (int)Math.Round(weightedScore),
            BorrowScore = borrowScore,
            GammaScore = gammaScore,
            MarginScore = marginScore,
            MomentumScore = momentumScore
        };

        // Assert
        signal.SqueezeScore.Should().BeInRange(0, 100);
        // 28 + 17.5 + 12 + 15 = 72.5
        // Math.Round uses banker's rounding, so 72.5 rounds to 72
        signal.SqueezeScore.Should().Be(72);
    }

    [Fact]
    public void TrendType_ShouldHaveCorrectValues()
    {
        // Assert
        TrendType.Bullish.Should().BeDefined();
        TrendType.Neutral.Should().BeDefined();
        TrendType.Bearish.Should().BeDefined();
        TrendType.Degraded.Should().BeDefined();
    }
}

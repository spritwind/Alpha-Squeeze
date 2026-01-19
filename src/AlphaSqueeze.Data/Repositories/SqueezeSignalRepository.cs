using System.Data;
using Dapper;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;

namespace AlphaSqueeze.Data.Repositories;

/// <summary>
/// 軋空訊號 Repository 實作
/// 使用 Dapper 進行資料存取
/// </summary>
public class SqueezeSignalRepository : ISqueezeSignalRepository
{
    private readonly IDbConnection _connection;

    public SqueezeSignalRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public async Task<SqueezeSignal?> GetByTickerAndDateAsync(string ticker, DateTime date)
    {
        const string sql = @"
            SELECT Id, Ticker, SignalDate, SqueezeScore, BorrowScore, GammaScore,
                   MarginScore, MomentumScore, Trend, Comment, NotificationSent, CreatedAt
            FROM SqueezeSignals
            WHERE Ticker = @Ticker AND SignalDate = @SignalDate";

        return await _connection.QuerySingleOrDefaultAsync<SqueezeSignal>(
            sql, new { Ticker = ticker, SignalDate = date });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SqueezeSignal>> GetTopCandidatesAsync(
        DateTime date, int minScore = 60, int limit = 10)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                   Id, Ticker, SignalDate, SqueezeScore, BorrowScore, GammaScore,
                   MarginScore, MomentumScore, Trend, Comment, NotificationSent, CreatedAt
            FROM SqueezeSignals
            WHERE SignalDate = @SignalDate AND SqueezeScore >= @MinScore
            ORDER BY SqueezeScore DESC";

        return await _connection.QueryAsync<SqueezeSignal>(
            sql, new { SignalDate = date, MinScore = minScore, Limit = limit });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SqueezeSignal>> GetByDateAsync(DateTime date)
    {
        const string sql = @"
            SELECT Id, Ticker, SignalDate, SqueezeScore, BorrowScore, GammaScore,
                   MarginScore, MomentumScore, Trend, Comment, NotificationSent, CreatedAt
            FROM SqueezeSignals
            WHERE SignalDate = @SignalDate
            ORDER BY SqueezeScore DESC";

        return await _connection.QueryAsync<SqueezeSignal>(sql, new { SignalDate = date });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SqueezeSignal>> GetHistoryAsync(
        string ticker, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT Id, Ticker, SignalDate, SqueezeScore, BorrowScore, GammaScore,
                   MarginScore, MomentumScore, Trend, Comment, NotificationSent, CreatedAt
            FROM SqueezeSignals
            WHERE Ticker = @Ticker
              AND SignalDate >= @StartDate
              AND SignalDate <= @EndDate
            ORDER BY SignalDate";

        return await _connection.QueryAsync<SqueezeSignal>(
            sql, new { Ticker = ticker, StartDate = startDate, EndDate = endDate });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<SqueezeSignal>> GetPendingNotificationsAsync(
        DateTime date, int minScore = 60)
    {
        const string sql = @"
            SELECT Id, Ticker, SignalDate, SqueezeScore, BorrowScore, GammaScore,
                   MarginScore, MomentumScore, Trend, Comment, NotificationSent, CreatedAt
            FROM SqueezeSignals
            WHERE SignalDate = @SignalDate
              AND SqueezeScore >= @MinScore
              AND NotificationSent = 0
            ORDER BY SqueezeScore DESC";

        return await _connection.QueryAsync<SqueezeSignal>(
            sql, new { SignalDate = date, MinScore = minScore });
    }

    /// <inheritdoc />
    public async Task<int> UpsertAsync(SqueezeSignal signal)
    {
        const string sql = @"
            MERGE INTO SqueezeSignals AS target
            USING (SELECT @Ticker AS Ticker, @SignalDate AS SignalDate) AS source
            ON target.Ticker = source.Ticker AND target.SignalDate = source.SignalDate
            WHEN MATCHED THEN
                UPDATE SET
                    SqueezeScore = @SqueezeScore,
                    BorrowScore = @BorrowScore,
                    GammaScore = @GammaScore,
                    MarginScore = @MarginScore,
                    MomentumScore = @MomentumScore,
                    Trend = @Trend,
                    Comment = @Comment
            WHEN NOT MATCHED THEN
                INSERT (Ticker, SignalDate, SqueezeScore, BorrowScore, GammaScore,
                        MarginScore, MomentumScore, Trend, Comment, NotificationSent, CreatedAt)
                VALUES (@Ticker, @SignalDate, @SqueezeScore, @BorrowScore, @GammaScore,
                        @MarginScore, @MomentumScore, @Trend, @Comment, 0, GETDATE());";

        return await _connection.ExecuteAsync(sql, signal);
    }

    /// <inheritdoc />
    public async Task<int> BulkUpsertAsync(IEnumerable<SqueezeSignal> signals)
    {
        var signalsList = signals.ToList();
        if (signalsList.Count == 0)
            return 0;

        var count = 0;
        foreach (var signal in signalsList)
        {
            count += await UpsertAsync(signal);
        }
        return count;
    }

    /// <inheritdoc />
    public async Task<int> MarkNotificationSentAsync(IEnumerable<int> signalIds)
    {
        var idsList = signalIds.ToList();
        if (idsList.Count == 0)
            return 0;

        const string sql = @"
            UPDATE SqueezeSignals
            SET NotificationSent = 1
            WHERE Id IN @Ids";

        return await _connection.ExecuteAsync(sql, new { Ids = idsList });
    }
}

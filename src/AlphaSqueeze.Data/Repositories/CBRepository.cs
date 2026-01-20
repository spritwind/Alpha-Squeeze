using System.Data;
using Dapper;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;

namespace AlphaSqueeze.Data.Repositories;

/// <summary>
/// CB 可轉債 Repository 實作
/// 使用 Dapper 進行資料存取
/// </summary>
public class CBRepository : ICBRepository
{
    private readonly IDbConnection _connection;

    public CBRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    #region CBIssuance 相關

    /// <inheritdoc />
    public async Task<IEnumerable<CBIssuance>> GetActiveCBsAsync()
    {
        const string sql = @"
            SELECT Id, CBTicker, UnderlyingTicker, CBName, IssueDate, MaturityDate,
                   InitialConversionPrice, CurrentConversionPrice, TotalIssueAmount,
                   OutstandingAmount, RedemptionTriggerPct, RedemptionTriggerDays,
                   IsActive, CreatedAt, UpdatedAt
            FROM CBIssuance
            WHERE IsActive = 1 AND MaturityDate > GETDATE()
            ORDER BY UnderlyingTicker, CBTicker";

        return await _connection.QueryAsync<CBIssuance>(sql);
    }

    /// <inheritdoc />
    public async Task<CBIssuance?> GetCBByTickerAsync(string cbTicker)
    {
        const string sql = @"
            SELECT Id, CBTicker, UnderlyingTicker, CBName, IssueDate, MaturityDate,
                   InitialConversionPrice, CurrentConversionPrice, TotalIssueAmount,
                   OutstandingAmount, RedemptionTriggerPct, RedemptionTriggerDays,
                   IsActive, CreatedAt, UpdatedAt
            FROM CBIssuance
            WHERE CBTicker = @CBTicker";

        return await _connection.QuerySingleOrDefaultAsync<CBIssuance>(sql, new { CBTicker = cbTicker });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CBIssuance>> GetCBsByUnderlyingAsync(string underlyingTicker)
    {
        const string sql = @"
            SELECT Id, CBTicker, UnderlyingTicker, CBName, IssueDate, MaturityDate,
                   InitialConversionPrice, CurrentConversionPrice, TotalIssueAmount,
                   OutstandingAmount, RedemptionTriggerPct, RedemptionTriggerDays,
                   IsActive, CreatedAt, UpdatedAt
            FROM CBIssuance
            WHERE UnderlyingTicker = @UnderlyingTicker AND IsActive = 1
            ORDER BY CBTicker";

        return await _connection.QueryAsync<CBIssuance>(sql, new { UnderlyingTicker = underlyingTicker });
    }

    /// <inheritdoc />
    public async Task<int> UpsertCBIssuanceAsync(CBIssuance cb)
    {
        const string sql = @"
            MERGE INTO CBIssuance AS target
            USING (SELECT @CBTicker AS CBTicker) AS source
            ON target.CBTicker = source.CBTicker
            WHEN MATCHED THEN
                UPDATE SET
                    UnderlyingTicker = @UnderlyingTicker,
                    CBName = @CBName,
                    IssueDate = @IssueDate,
                    MaturityDate = @MaturityDate,
                    InitialConversionPrice = @InitialConversionPrice,
                    CurrentConversionPrice = @CurrentConversionPrice,
                    TotalIssueAmount = @TotalIssueAmount,
                    OutstandingAmount = @OutstandingAmount,
                    RedemptionTriggerPct = @RedemptionTriggerPct,
                    RedemptionTriggerDays = @RedemptionTriggerDays,
                    IsActive = @IsActive,
                    UpdatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (CBTicker, UnderlyingTicker, CBName, IssueDate, MaturityDate,
                        InitialConversionPrice, CurrentConversionPrice, TotalIssueAmount,
                        OutstandingAmount, RedemptionTriggerPct, RedemptionTriggerDays,
                        IsActive, CreatedAt, UpdatedAt)
                VALUES (@CBTicker, @UnderlyingTicker, @CBName, @IssueDate, @MaturityDate,
                        @InitialConversionPrice, @CurrentConversionPrice, @TotalIssueAmount,
                        @OutstandingAmount, @RedemptionTriggerPct, @RedemptionTriggerDays,
                        @IsActive, GETDATE(), GETDATE());";

        return await _connection.ExecuteAsync(sql, cb);
    }

    #endregion

    #region CBDailyTracking 相關

    /// <inheritdoc />
    public async Task<IEnumerable<CBDailyTracking>> GetDailyTrackingAsync(DateTime tradeDate, string? minWarningLevel = null)
    {
        var sql = @"
            SELECT Id, CBTicker, TradeDate, UnderlyingClosePrice, ConversionPrice,
                   PriceToConversionRatio, IsAboveTrigger, ConsecutiveDaysAbove,
                   OutstandingBalance, BalanceChangePercent, WarningLevel, CreatedAt
            FROM CBDailyTracking
            WHERE TradeDate = @TradeDate";

        if (!string.IsNullOrEmpty(minWarningLevel))
        {
            sql += @"
              AND (
                  @MinLevel = 'SAFE' OR
                  (@MinLevel = 'CAUTION' AND WarningLevel IN ('CAUTION', 'WARNING', 'CRITICAL')) OR
                  (@MinLevel = 'WARNING' AND WarningLevel IN ('WARNING', 'CRITICAL')) OR
                  (@MinLevel = 'CRITICAL' AND WarningLevel = 'CRITICAL')
              )";
        }

        sql += " ORDER BY ConsecutiveDaysAbove DESC";

        return await _connection.QueryAsync<CBDailyTracking>(
            sql, new { TradeDate = tradeDate, MinLevel = minWarningLevel ?? "SAFE" });
    }

    /// <inheritdoc />
    public async Task<CBDailyTracking?> GetDailyTrackingByTickerAsync(string cbTicker, DateTime tradeDate)
    {
        const string sql = @"
            SELECT Id, CBTicker, TradeDate, UnderlyingClosePrice, ConversionPrice,
                   PriceToConversionRatio, IsAboveTrigger, ConsecutiveDaysAbove,
                   OutstandingBalance, BalanceChangePercent, WarningLevel, CreatedAt
            FROM CBDailyTracking
            WHERE CBTicker = @CBTicker AND TradeDate = @TradeDate";

        return await _connection.QuerySingleOrDefaultAsync<CBDailyTracking>(
            sql, new { CBTicker = cbTicker, TradeDate = tradeDate });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CBDailyTracking>> GetTrackingHistoryAsync(string cbTicker, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT Id, CBTicker, TradeDate, UnderlyingClosePrice, ConversionPrice,
                   PriceToConversionRatio, IsAboveTrigger, ConsecutiveDaysAbove,
                   OutstandingBalance, BalanceChangePercent, WarningLevel, CreatedAt
            FROM CBDailyTracking
            WHERE CBTicker = @CBTicker AND TradeDate >= @StartDate AND TradeDate <= @EndDate
            ORDER BY TradeDate";

        return await _connection.QueryAsync<CBDailyTracking>(
            sql, new { CBTicker = cbTicker, StartDate = startDate, EndDate = endDate });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CBDailyTracking>> GetCriticalCBsAsync(DateTime tradeDate, int minDaysAbove, int limit)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                   Id, CBTicker, TradeDate, UnderlyingClosePrice, ConversionPrice,
                   PriceToConversionRatio, IsAboveTrigger, ConsecutiveDaysAbove,
                   OutstandingBalance, BalanceChangePercent, WarningLevel, CreatedAt
            FROM CBDailyTracking
            WHERE TradeDate = @TradeDate AND ConsecutiveDaysAbove >= @MinDaysAbove
            ORDER BY ConsecutiveDaysAbove DESC, OutstandingBalance DESC";

        return await _connection.QueryAsync<CBDailyTracking>(
            sql, new { TradeDate = tradeDate, MinDaysAbove = minDaysAbove, Limit = limit });
    }

    /// <inheritdoc />
    public async Task<(int ConsecutiveDays, string WarningLevel)> UpsertDailyTrackingAsync(
        string cbTicker,
        DateTime tradeDate,
        decimal underlyingClosePrice,
        decimal conversionPrice,
        decimal outstandingBalance)
    {
        // 使用存儲程序進行 Upsert (自動計算連續天數與預警等級)
        var parameters = new DynamicParameters();
        parameters.Add("@CBTicker", cbTicker);
        parameters.Add("@TradeDate", tradeDate);
        parameters.Add("@UnderlyingClosePrice", underlyingClosePrice);
        parameters.Add("@ConversionPrice", conversionPrice);
        parameters.Add("@OutstandingBalance", outstandingBalance);

        var result = await _connection.QuerySingleOrDefaultAsync<dynamic>(
            "sp_UpsertCBDailyTracking",
            parameters,
            commandType: CommandType.StoredProcedure);

        if (result == null)
        {
            return (0, "SAFE");
        }

        return ((int)result.ConsecutiveDays, (string)result.WarningLevel);
    }

    #endregion

    #region 聚合查詢

    /// <inheritdoc />
    public async Task<(int Total, int Critical, int Warning, int Caution, int Safe)> GetWarningSummaryAsync(DateTime tradeDate)
    {
        const string sql = @"
            SELECT
                COUNT(*) AS Total,
                SUM(CASE WHEN WarningLevel = 'CRITICAL' THEN 1 ELSE 0 END) AS CriticalCount,
                SUM(CASE WHEN WarningLevel = 'WARNING' THEN 1 ELSE 0 END) AS WarningCount,
                SUM(CASE WHEN WarningLevel = 'CAUTION' THEN 1 ELSE 0 END) AS CautionCount,
                SUM(CASE WHEN WarningLevel = 'SAFE' THEN 1 ELSE 0 END) AS SafeCount
            FROM CBDailyTracking
            WHERE TradeDate = @TradeDate";

        var result = await _connection.QuerySingleOrDefaultAsync<dynamic>(sql, new { TradeDate = tradeDate });

        if (result == null)
        {
            return (0, 0, 0, 0, 0);
        }

        return (
            (int)(result.Total ?? 0),
            (int)(result.CriticalCount ?? 0),
            (int)(result.WarningCount ?? 0),
            (int)(result.CautionCount ?? 0),
            (int)(result.SafeCount ?? 0)
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CBWarningData>> GetCBWarningsWithDetailsAsync(DateTime tradeDate, string minWarningLevel = "SAFE")
    {
        var sql = @"
            SELECT
                i.CBTicker,
                i.UnderlyingTicker,
                i.CBName,
                i.TotalIssueAmount,
                i.MaturityDate,
                i.RedemptionTriggerPct,
                i.RedemptionTriggerDays,
                t.TradeDate,
                t.UnderlyingClosePrice,
                t.ConversionPrice,
                t.PriceToConversionRatio,
                t.IsAboveTrigger,
                t.ConsecutiveDaysAbove,
                t.OutstandingBalance,
                t.BalanceChangePercent,
                t.WarningLevel
            FROM CBDailyTracking t
            INNER JOIN CBIssuance i ON t.CBTicker = i.CBTicker
            WHERE t.TradeDate = @TradeDate";

        if (minWarningLevel != "SAFE")
        {
            sql += @"
              AND (
                  (@MinLevel = 'CAUTION' AND t.WarningLevel IN ('CAUTION', 'WARNING', 'CRITICAL')) OR
                  (@MinLevel = 'WARNING' AND t.WarningLevel IN ('WARNING', 'CRITICAL')) OR
                  (@MinLevel = 'CRITICAL' AND t.WarningLevel = 'CRITICAL')
              )";
        }

        sql += " ORDER BY t.ConsecutiveDaysAbove DESC, t.OutstandingBalance DESC";

        return await _connection.QueryAsync<CBWarningData>(
            sql, new { TradeDate = tradeDate, MinLevel = minWarningLevel });
    }

    #endregion
}

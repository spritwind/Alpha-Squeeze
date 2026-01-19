using System.Data;
using Dapper;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;

namespace AlphaSqueeze.Data.Repositories;

/// <summary>
/// 權證資料 Repository 實作
/// 使用 Dapper 進行資料存取
/// </summary>
public class WarrantRepository : IWarrantRepository
{
    private readonly IDbConnection _connection;

    public WarrantRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public async Task<WarrantMarketData?> GetByTickerAndDateAsync(string warrantTicker, DateTime date)
    {
        const string sql = @"
            SELECT Id, UnderlyingTicker, WarrantTicker, WarrantName, Issuer, WarrantType,
                   ImpliedVolatility, EffectiveLeverage, SpreadRatio, StrikePrice,
                   ExpiryDate, DaysToExpiry, Delta, Gamma, Theta, Vega,
                   TradeDate, LastUpdate
            FROM WarrantMarketData
            WHERE WarrantTicker = @WarrantTicker AND TradeDate = @TradeDate";

        return await _connection.QuerySingleOrDefaultAsync<WarrantMarketData>(
            sql, new { WarrantTicker = warrantTicker, TradeDate = date });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WarrantMarketData>> GetByUnderlyingAsync(
        string underlyingTicker, DateTime date)
    {
        const string sql = @"
            SELECT Id, UnderlyingTicker, WarrantTicker, WarrantName, Issuer, WarrantType,
                   ImpliedVolatility, EffectiveLeverage, SpreadRatio, StrikePrice,
                   ExpiryDate, DaysToExpiry, Delta, Gamma, Theta, Vega,
                   TradeDate, LastUpdate
            FROM WarrantMarketData
            WHERE UnderlyingTicker = @UnderlyingTicker AND TradeDate = @TradeDate
            ORDER BY WarrantTicker";

        return await _connection.QueryAsync<WarrantMarketData>(
            sql, new { UnderlyingTicker = underlyingTicker, TradeDate = date });
    }

    /// <inheritdoc />
    public async Task<decimal?> GetAverageIVAsync(
        string underlyingTicker, DateTime date, string? warrantType = null)
    {
        var sql = @"
            SELECT AVG(ImpliedVolatility)
            FROM WarrantMarketData
            WHERE UnderlyingTicker = @UnderlyingTicker
              AND TradeDate = @TradeDate
              AND ImpliedVolatility IS NOT NULL";

        if (!string.IsNullOrEmpty(warrantType))
        {
            sql += " AND WarrantType = @WarrantType";
        }

        return await _connection.QuerySingleOrDefaultAsync<decimal?>(
            sql, new { UnderlyingTicker = underlyingTicker, TradeDate = date, WarrantType = warrantType });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WarrantMarketData>> GetByDateAsync(DateTime date)
    {
        const string sql = @"
            SELECT Id, UnderlyingTicker, WarrantTicker, WarrantName, Issuer, WarrantType,
                   ImpliedVolatility, EffectiveLeverage, SpreadRatio, StrikePrice,
                   ExpiryDate, DaysToExpiry, Delta, Gamma, Theta, Vega,
                   TradeDate, LastUpdate
            FROM WarrantMarketData
            WHERE TradeDate = @TradeDate
            ORDER BY UnderlyingTicker, WarrantTicker";

        return await _connection.QueryAsync<WarrantMarketData>(sql, new { TradeDate = date });
    }

    /// <inheritdoc />
    public async Task<int> UpsertAsync(WarrantMarketData warrant)
    {
        const string sql = @"
            MERGE INTO WarrantMarketData AS target
            USING (SELECT @WarrantTicker AS WarrantTicker, @TradeDate AS TradeDate) AS source
            ON target.WarrantTicker = source.WarrantTicker AND target.TradeDate = source.TradeDate
            WHEN MATCHED THEN
                UPDATE SET
                    UnderlyingTicker = @UnderlyingTicker,
                    WarrantName = @WarrantName,
                    Issuer = @Issuer,
                    WarrantType = @WarrantType,
                    ImpliedVolatility = @ImpliedVolatility,
                    EffectiveLeverage = @EffectiveLeverage,
                    SpreadRatio = @SpreadRatio,
                    StrikePrice = @StrikePrice,
                    ExpiryDate = @ExpiryDate,
                    DaysToExpiry = @DaysToExpiry,
                    Delta = @Delta,
                    Gamma = @Gamma,
                    Theta = @Theta,
                    Vega = @Vega,
                    LastUpdate = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (UnderlyingTicker, WarrantTicker, WarrantName, Issuer, WarrantType,
                        ImpliedVolatility, EffectiveLeverage, SpreadRatio, StrikePrice,
                        ExpiryDate, DaysToExpiry, Delta, Gamma, Theta, Vega,
                        TradeDate, LastUpdate)
                VALUES (@UnderlyingTicker, @WarrantTicker, @WarrantName, @Issuer, @WarrantType,
                        @ImpliedVolatility, @EffectiveLeverage, @SpreadRatio, @StrikePrice,
                        @ExpiryDate, @DaysToExpiry, @Delta, @Gamma, @Theta, @Vega,
                        @TradeDate, GETDATE());";

        return await _connection.ExecuteAsync(sql, warrant);
    }

    /// <inheritdoc />
    public async Task<int> BulkUpsertAsync(IEnumerable<WarrantMarketData> warrants)
    {
        var warrantsList = warrants.ToList();
        if (warrantsList.Count == 0)
            return 0;

        var count = 0;
        foreach (var warrant in warrantsList)
        {
            count += await UpsertAsync(warrant);
        }
        return count;
    }
}

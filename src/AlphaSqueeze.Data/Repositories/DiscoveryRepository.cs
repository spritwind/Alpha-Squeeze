using System.Data;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;
using Dapper;

namespace AlphaSqueeze.Data.Repositories;

public class DiscoveryRepository : IDiscoveryRepository
{
    private readonly IDbConnection _connection;

    public DiscoveryRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<DiscoveryPool>> GetByDateAsync(DateTime scanDate)
    {
        return await _connection.QueryAsync<DiscoveryPool>(@"
            SELECT * FROM DiscoveryPool
            WHERE ScanDate = @ScanDate
            ORDER BY SqueezeScore DESC",
            new { ScanDate = scanDate.Date });
    }

    public async Task<IEnumerable<DiscoveryPool>> GetLatestAsync(int limit = 100)
    {
        return await _connection.QueryAsync<DiscoveryPool>(@"
            SELECT TOP (@Limit) * FROM DiscoveryPool
            WHERE ScanDate = (SELECT MAX(ScanDate) FROM DiscoveryPool)
            ORDER BY SqueezeScore DESC",
            new { Limit = limit });
    }

    public async Task<IEnumerable<DiscoveryPool>> FilterAsync(
        DateTime? scanDate = null,
        decimal? minShortRatio = null,
        decimal? minVolMultiplier = null,
        decimal? minPrice = null,
        long? minVolume = null,
        bool? hasCB = null,
        int? minScore = null,
        int limit = 100)
    {
        var sql = @"
            SELECT TOP (@Limit) * FROM DiscoveryPool
            WHERE 1=1";

        if (scanDate.HasValue)
            sql += " AND ScanDate = @ScanDate";
        else
            sql += " AND ScanDate = (SELECT MAX(ScanDate) FROM DiscoveryPool)";

        if (minShortRatio.HasValue)
            sql += " AND ShortRatio >= @MinShortRatio";

        if (minVolMultiplier.HasValue)
            sql += " AND VolMultiplier >= @MinVolMultiplier";

        if (minPrice.HasValue)
            sql += " AND ClosePrice >= @MinPrice";

        if (minVolume.HasValue)
            sql += " AND Volume >= @MinVolume";

        if (hasCB.HasValue)
            sql += " AND HasCB = @HasCB";

        if (minScore.HasValue)
            sql += " AND SqueezeScore >= @MinScore";

        sql += " ORDER BY SqueezeScore DESC";

        return await _connection.QueryAsync<DiscoveryPool>(sql, new
        {
            Limit = limit,
            ScanDate = scanDate?.Date,
            MinShortRatio = minShortRatio,
            MinVolMultiplier = minVolMultiplier,
            MinPrice = minPrice,
            MinVolume = minVolume,
            HasCB = hasCB,
            MinScore = minScore
        });
    }

    public async Task<int> AddAsync(DiscoveryPool item)
    {
        return await _connection.ExecuteAsync(@"
            INSERT INTO DiscoveryPool
                (Ticker, TickerName, Industry, ClosePrice, Volume, AvgVolume5D,
                 VolMultiplier, ShortSellingBalance, SharesOutstanding, ShortRatio,
                 MarginRatio, HasCB, CBTicker, CBPriceRatio, SqueezeScore, ScanDate)
            VALUES
                (@Ticker, @TickerName, @Industry, @ClosePrice, @Volume, @AvgVolume5D,
                 @VolMultiplier, @ShortSellingBalance, @SharesOutstanding, @ShortRatio,
                 @MarginRatio, @HasCB, @CBTicker, @CBPriceRatio, @SqueezeScore, @ScanDate)",
            item);
    }

    public async Task<int> BulkAddAsync(IEnumerable<DiscoveryPool> items)
    {
        var itemList = items.ToList();
        if (!itemList.Any()) return 0;

        // Use transaction for bulk insert
        if (_connection.State != ConnectionState.Open)
            _connection.Open();

        using var transaction = _connection.BeginTransaction();
        try
        {
            var count = 0;
            foreach (var item in itemList)
            {
                count += await _connection.ExecuteAsync(@"
                    MERGE INTO DiscoveryPool AS target
                    USING (SELECT @Ticker AS Ticker, @ScanDate AS ScanDate) AS source
                    ON target.Ticker = source.Ticker AND target.ScanDate = source.ScanDate
                    WHEN MATCHED THEN
                        UPDATE SET
                            TickerName = @TickerName,
                            Industry = @Industry,
                            ClosePrice = @ClosePrice,
                            Volume = @Volume,
                            AvgVolume5D = @AvgVolume5D,
                            VolMultiplier = @VolMultiplier,
                            ShortSellingBalance = @ShortSellingBalance,
                            SharesOutstanding = @SharesOutstanding,
                            ShortRatio = @ShortRatio,
                            MarginRatio = @MarginRatio,
                            HasCB = @HasCB,
                            CBTicker = @CBTicker,
                            CBPriceRatio = @CBPriceRatio,
                            SqueezeScore = @SqueezeScore
                    WHEN NOT MATCHED THEN
                        INSERT (Ticker, TickerName, Industry, ClosePrice, Volume, AvgVolume5D,
                                VolMultiplier, ShortSellingBalance, SharesOutstanding, ShortRatio,
                                MarginRatio, HasCB, CBTicker, CBPriceRatio, SqueezeScore, ScanDate)
                        VALUES (@Ticker, @TickerName, @Industry, @ClosePrice, @Volume, @AvgVolume5D,
                                @VolMultiplier, @ShortSellingBalance, @SharesOutstanding, @ShortRatio,
                                @MarginRatio, @HasCB, @CBTicker, @CBPriceRatio, @SqueezeScore, @ScanDate);",
                    item, transaction);
            }
            transaction.Commit();
            return count;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<int> ClearByDateAsync(DateTime scanDate)
    {
        return await _connection.ExecuteAsync(
            "DELETE FROM DiscoveryPool WHERE ScanDate = @ScanDate",
            new { ScanDate = scanDate.Date });
    }

    public async Task<Dictionary<string, string>> GetConfigAsync()
    {
        var results = await _connection.QueryAsync<(string Key, string Value)>(@"
            SELECT ConfigKey AS [Key], ConfigValue AS Value FROM DiscoveryConfig");

        return results.ToDictionary(x => x.Key, x => x.Value);
    }

    public async Task UpdateConfigAsync(string key, string value)
    {
        await _connection.ExecuteAsync(@"
            UPDATE DiscoveryConfig
            SET ConfigValue = @Value, UpdatedAt = GETDATE()
            WHERE ConfigKey = @Key",
            new { Key = key, Value = value });
    }
}

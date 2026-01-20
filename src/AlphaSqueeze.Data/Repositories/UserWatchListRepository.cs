using System.Data;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;
using Dapper;

namespace AlphaSqueeze.Data.Repositories;

public class UserWatchListRepository : IUserWatchListRepository
{
    private readonly IDbConnection _connection;

    public UserWatchListRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<UserWatchList>> GetAllAsync()
    {
        return await _connection.QueryAsync<UserWatchList>(@"
            SELECT * FROM UserWatchList
            ORDER BY Priority, AddedTime DESC");
    }

    public async Task<IEnumerable<UserWatchList>> GetActiveAsync()
    {
        return await _connection.QueryAsync<UserWatchList>(@"
            SELECT * FROM UserWatchList
            WHERE IsActive = 1
            ORDER BY Priority, AddedTime DESC");
    }

    public async Task<IEnumerable<string>> GetActiveTickersAsync()
    {
        return await _connection.QueryAsync<string>(@"
            SELECT Ticker FROM UserWatchList
            WHERE IsActive = 1
            ORDER BY Priority");
    }

    public async Task<UserWatchList?> GetByTickerAsync(string ticker)
    {
        return await _connection.QueryFirstOrDefaultAsync<UserWatchList>(@"
            SELECT * FROM UserWatchList WHERE Ticker = @Ticker",
            new { Ticker = ticker.ToUpperInvariant() });
    }

    public async Task<bool> AddAsync(UserWatchList item)
    {
        try
        {
            var result = await _connection.ExecuteAsync(@"
                INSERT INTO UserWatchList
                    (Ticker, TickerName, AddedBy, IsActive, Priority, Notes)
                VALUES
                    (@Ticker, @TickerName, @AddedBy, @IsActive, @Priority, @Notes)",
                new
                {
                    Ticker = item.Ticker.ToUpperInvariant(),
                    item.TickerName,
                    item.AddedBy,
                    item.IsActive,
                    item.Priority,
                    item.Notes
                });
            return result > 0;
        }
        catch
        {
            return false; // Likely duplicate
        }
    }

    public async Task<int> BulkAddAsync(IEnumerable<string> tickers, string addedBy = "WebUI")
    {
        var tickerList = tickers.Select(t => t.ToUpperInvariant()).ToList();
        if (!tickerList.Any()) return 0;

        var count = 0;
        foreach (var ticker in tickerList)
        {
            try
            {
                count += await _connection.ExecuteAsync(@"
                    IF NOT EXISTS (SELECT 1 FROM UserWatchList WHERE Ticker = @Ticker)
                    INSERT INTO UserWatchList (Ticker, AddedBy, IsActive, Priority)
                    VALUES (@Ticker, @AddedBy, 1, 100)",
                    new { Ticker = ticker, AddedBy = addedBy });
            }
            catch
            {
                // Skip duplicates
            }
        }
        return count;
    }

    public async Task<bool> UpdateAsync(UserWatchList item)
    {
        var result = await _connection.ExecuteAsync(@"
            UPDATE UserWatchList SET
                TickerName = @TickerName,
                IsActive = @IsActive,
                Priority = @Priority,
                Notes = @Notes,
                UpdatedAt = GETDATE()
            WHERE Ticker = @Ticker",
            new
            {
                Ticker = item.Ticker.ToUpperInvariant(),
                item.TickerName,
                item.IsActive,
                item.Priority,
                item.Notes
            });
        return result > 0;
    }

    public async Task<bool> SetActiveAsync(string ticker, bool isActive)
    {
        var result = await _connection.ExecuteAsync(@"
            UPDATE UserWatchList SET
                IsActive = @IsActive,
                UpdatedAt = GETDATE()
            WHERE Ticker = @Ticker",
            new { Ticker = ticker.ToUpperInvariant(), IsActive = isActive });
        return result > 0;
    }

    public async Task<bool> RemoveAsync(string ticker)
    {
        var result = await _connection.ExecuteAsync(
            "DELETE FROM UserWatchList WHERE Ticker = @Ticker",
            new { Ticker = ticker.ToUpperInvariant() });
        return result > 0;
    }

    public async Task<bool> UpdateScrapedTimeAsync(string ticker, DateTime scrapedTime, int? squeezeScore = null)
    {
        var sql = @"
            UPDATE UserWatchList SET
                LastDeepScrapedTime = @ScrapedTime,
                UpdatedAt = GETDATE()";

        if (squeezeScore.HasValue)
            sql += ", LastSqueezeScore = @SqueezeScore";

        sql += " WHERE Ticker = @Ticker";

        var result = await _connection.ExecuteAsync(sql,
            new
            {
                Ticker = ticker.ToUpperInvariant(),
                ScrapedTime = scrapedTime,
                SqueezeScore = squeezeScore
            });
        return result > 0;
    }
}

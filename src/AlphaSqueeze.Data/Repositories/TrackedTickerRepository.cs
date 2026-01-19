using System.Data;
using Dapper;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;

namespace AlphaSqueeze.Data.Repositories;

/// <summary>
/// 追蹤股票 Repository 實作
/// 使用 Dapper 進行資料存取
/// </summary>
public class TrackedTickerRepository : ITrackedTickerRepository
{
    private readonly IDbConnection _connection;

    public TrackedTickerRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedTicker>> GetAllAsync()
    {
        const string sql = @"
            SELECT Ticker, TickerName, Category, IsActive, Priority, AddedAt, Notes
            FROM TrackedTickers
            ORDER BY Priority, Ticker";

        return await _connection.QueryAsync<TrackedTicker>(sql);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetActiveTickersAsync()
    {
        const string sql = @"
            SELECT Ticker
            FROM TrackedTickers
            WHERE IsActive = 1
            ORDER BY Priority, Ticker";

        return await _connection.QueryAsync<string>(sql);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TrackedTicker>> GetByCategoryAsync(string category)
    {
        const string sql = @"
            SELECT Ticker, TickerName, Category, IsActive, Priority, AddedAt, Notes
            FROM TrackedTickers
            WHERE Category = @Category
            ORDER BY Priority, Ticker";

        return await _connection.QueryAsync<TrackedTicker>(sql, new { Category = category });
    }

    /// <inheritdoc />
    public async Task<TrackedTicker?> GetByTickerAsync(string ticker)
    {
        const string sql = @"
            SELECT Ticker, TickerName, Category, IsActive, Priority, AddedAt, Notes
            FROM TrackedTickers
            WHERE Ticker = @Ticker";

        return await _connection.QuerySingleOrDefaultAsync<TrackedTicker>(sql, new { Ticker = ticker });
    }

    /// <inheritdoc />
    public async Task<bool> AddAsync(TrackedTicker ticker)
    {
        const string sql = @"
            INSERT INTO TrackedTickers (Ticker, TickerName, Category, IsActive, Priority, Notes)
            VALUES (@Ticker, @TickerName, @Category, @IsActive, @Priority, @Notes)";

        try
        {
            var affected = await _connection.ExecuteAsync(sql, ticker);
            return affected > 0;
        }
        catch (Exception)
        {
            // Primary key violation - ticker already exists
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(TrackedTicker ticker)
    {
        const string sql = @"
            UPDATE TrackedTickers
            SET TickerName = @TickerName,
                Category = @Category,
                IsActive = @IsActive,
                Priority = @Priority,
                Notes = @Notes
            WHERE Ticker = @Ticker";

        var affected = await _connection.ExecuteAsync(sql, ticker);
        return affected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> SetActiveAsync(string ticker, bool isActive)
    {
        const string sql = @"
            UPDATE TrackedTickers
            SET IsActive = @IsActive
            WHERE Ticker = @Ticker";

        var affected = await _connection.ExecuteAsync(sql, new { Ticker = ticker, IsActive = isActive });
        return affected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string ticker)
    {
        const string sql = "DELETE FROM TrackedTickers WHERE Ticker = @Ticker";
        var affected = await _connection.ExecuteAsync(sql, new { Ticker = ticker });
        return affected > 0;
    }
}

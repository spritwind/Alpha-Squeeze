using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using AlphaSqueeze.Core.Entities;
using AlphaSqueeze.Core.Interfaces;

namespace AlphaSqueeze.Data.Repositories;

/// <summary>
/// 股票日指標 Repository 實作
/// 使用 Dapper 進行資料存取
/// </summary>
public class StockMetricsRepository : IStockMetricsRepository
{
    private readonly IDbConnection _connection;

    public StockMetricsRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    /// <inheritdoc />
    public async Task<DailyStockMetric?> GetByTickerAndDateAsync(string ticker, DateTime date)
    {
        const string sql = @"
            SELECT Id, Ticker, TradeDate, ClosePrice, OpenPrice, HighPrice, LowPrice,
                   BorrowingBalance, BorrowingBalanceChange, MarginBalance, ShortBalance,
                   MarginRatio, HistoricalVolatility20D, Volume, Turnover,
                   CreatedAt, UpdatedAt
            FROM DailyStockMetrics
            WHERE Ticker = @Ticker AND TradeDate = @TradeDate";

        return await _connection.QuerySingleOrDefaultAsync<DailyStockMetric>(
            sql, new { Ticker = ticker, TradeDate = date });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DailyStockMetric>> GetByDateAsync(DateTime date)
    {
        const string sql = @"
            SELECT Id, Ticker, TradeDate, ClosePrice, OpenPrice, HighPrice, LowPrice,
                   BorrowingBalance, BorrowingBalanceChange, MarginBalance, ShortBalance,
                   MarginRatio, HistoricalVolatility20D, Volume, Turnover,
                   CreatedAt, UpdatedAt
            FROM DailyStockMetrics
            WHERE TradeDate = @TradeDate
            ORDER BY Ticker";

        return await _connection.QueryAsync<DailyStockMetric>(sql, new { TradeDate = date });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DailyStockMetric>> GetHistoryAsync(
        string ticker, DateTime startDate, DateTime endDate)
    {
        const string sql = @"
            SELECT Id, Ticker, TradeDate, ClosePrice, OpenPrice, HighPrice, LowPrice,
                   BorrowingBalance, BorrowingBalanceChange, MarginBalance, ShortBalance,
                   MarginRatio, HistoricalVolatility20D, Volume, Turnover,
                   CreatedAt, UpdatedAt
            FROM DailyStockMetrics
            WHERE Ticker = @Ticker
              AND TradeDate >= @StartDate
              AND TradeDate <= @EndDate
            ORDER BY TradeDate";

        return await _connection.QueryAsync<DailyStockMetric>(
            sql, new { Ticker = ticker, StartDate = startDate, EndDate = endDate });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DailyStockMetric>> GetTopByMarginRatioAsync(
        DateTime date, decimal minMarginRatio, int limit)
    {
        const string sql = @"
            SELECT TOP (@Limit)
                   Id, Ticker, TradeDate, ClosePrice, OpenPrice, HighPrice, LowPrice,
                   BorrowingBalance, BorrowingBalanceChange, MarginBalance, ShortBalance,
                   MarginRatio, HistoricalVolatility20D, Volume, Turnover,
                   CreatedAt, UpdatedAt
            FROM DailyStockMetrics
            WHERE TradeDate = @TradeDate AND MarginRatio >= @MinMarginRatio
            ORDER BY MarginRatio DESC";

        return await _connection.QueryAsync<DailyStockMetric>(
            sql, new { TradeDate = date, MinMarginRatio = minMarginRatio, Limit = limit });
    }

    /// <inheritdoc />
    public async Task<int> UpsertAsync(DailyStockMetric metric)
    {
        const string sql = @"
            MERGE INTO DailyStockMetrics AS target
            USING (SELECT @Ticker AS Ticker, @TradeDate AS TradeDate) AS source
            ON target.Ticker = source.Ticker AND target.TradeDate = source.TradeDate
            WHEN MATCHED THEN
                UPDATE SET
                    ClosePrice = @ClosePrice,
                    OpenPrice = @OpenPrice,
                    HighPrice = @HighPrice,
                    LowPrice = @LowPrice,
                    BorrowingBalance = @BorrowingBalance,
                    BorrowingBalanceChange = @BorrowingBalanceChange,
                    MarginBalance = @MarginBalance,
                    ShortBalance = @ShortBalance,
                    MarginRatio = @MarginRatio,
                    HistoricalVolatility20D = @HistoricalVolatility20D,
                    Volume = @Volume,
                    Turnover = @Turnover,
                    UpdatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (Ticker, TradeDate, ClosePrice, OpenPrice, HighPrice, LowPrice,
                        BorrowingBalance, BorrowingBalanceChange, MarginBalance, ShortBalance,
                        MarginRatio, HistoricalVolatility20D, Volume, Turnover,
                        CreatedAt, UpdatedAt)
                VALUES (@Ticker, @TradeDate, @ClosePrice, @OpenPrice, @HighPrice, @LowPrice,
                        @BorrowingBalance, @BorrowingBalanceChange, @MarginBalance, @ShortBalance,
                        @MarginRatio, @HistoricalVolatility20D, @Volume, @Turnover,
                        GETDATE(), GETDATE());";

        return await _connection.ExecuteAsync(sql, metric);
    }

    /// <inheritdoc />
    public async Task<int> BulkUpsertAsync(IEnumerable<DailyStockMetric> metrics)
    {
        var metricsList = metrics.ToList();
        if (metricsList.Count == 0)
            return 0;

        // 對於大量資料，使用 SqlBulkCopy 搭配臨時表
        if (metricsList.Count > 100)
        {
            return await BulkUpsertWithSqlBulkCopyAsync(metricsList);
        }

        // 少量資料直接批次執行
        var count = 0;
        foreach (var metric in metricsList)
        {
            count += await UpsertAsync(metric);
        }
        return count;
    }

    private async Task<int> BulkUpsertWithSqlBulkCopyAsync(List<DailyStockMetric> metrics)
    {
        var sqlConnection = _connection as SqlConnection;
        if (sqlConnection == null)
        {
            throw new InvalidOperationException("BulkCopy requires SqlConnection");
        }

        if (sqlConnection.State != ConnectionState.Open)
        {
            await sqlConnection.OpenAsync();
        }

        // 建立臨時表
        const string createTempTable = @"
            CREATE TABLE #TempMetrics (
                Ticker NVARCHAR(10),
                TradeDate DATE,
                ClosePrice DECIMAL(18, 2),
                OpenPrice DECIMAL(18, 2),
                HighPrice DECIMAL(18, 2),
                LowPrice DECIMAL(18, 2),
                BorrowingBalance BIGINT,
                BorrowingBalanceChange INT,
                MarginBalance BIGINT,
                ShortBalance BIGINT,
                MarginRatio DECIMAL(18, 4),
                HistoricalVolatility20D DECIMAL(18, 6),
                Volume BIGINT,
                Turnover BIGINT
            )";

        await _connection.ExecuteAsync(createTempTable);

        // 使用 SqlBulkCopy 寫入臨時表
        using var bulkCopy = new SqlBulkCopy(sqlConnection)
        {
            DestinationTableName = "#TempMetrics"
        };

        var dataTable = CreateDataTable(metrics);
        await bulkCopy.WriteToServerAsync(dataTable);

        // 從臨時表 MERGE 到正式表
        const string mergeSql = @"
            MERGE INTO DailyStockMetrics AS target
            USING #TempMetrics AS source
            ON target.Ticker = source.Ticker AND target.TradeDate = source.TradeDate
            WHEN MATCHED THEN
                UPDATE SET
                    ClosePrice = source.ClosePrice,
                    OpenPrice = source.OpenPrice,
                    HighPrice = source.HighPrice,
                    LowPrice = source.LowPrice,
                    BorrowingBalance = source.BorrowingBalance,
                    BorrowingBalanceChange = source.BorrowingBalanceChange,
                    MarginBalance = source.MarginBalance,
                    ShortBalance = source.ShortBalance,
                    MarginRatio = source.MarginRatio,
                    HistoricalVolatility20D = source.HistoricalVolatility20D,
                    Volume = source.Volume,
                    Turnover = source.Turnover,
                    UpdatedAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (Ticker, TradeDate, ClosePrice, OpenPrice, HighPrice, LowPrice,
                        BorrowingBalance, BorrowingBalanceChange, MarginBalance, ShortBalance,
                        MarginRatio, HistoricalVolatility20D, Volume, Turnover,
                        CreatedAt, UpdatedAt)
                VALUES (source.Ticker, source.TradeDate, source.ClosePrice, source.OpenPrice,
                        source.HighPrice, source.LowPrice, source.BorrowingBalance,
                        source.BorrowingBalanceChange, source.MarginBalance, source.ShortBalance,
                        source.MarginRatio, source.HistoricalVolatility20D, source.Volume,
                        source.Turnover, GETDATE(), GETDATE());

            DROP TABLE #TempMetrics;";

        return await _connection.ExecuteAsync(mergeSql);
    }

    private static DataTable CreateDataTable(IEnumerable<DailyStockMetric> metrics)
    {
        var table = new DataTable();
        table.Columns.Add("Ticker", typeof(string));
        table.Columns.Add("TradeDate", typeof(DateTime));
        table.Columns.Add("ClosePrice", typeof(decimal));
        table.Columns.Add("OpenPrice", typeof(decimal));
        table.Columns.Add("HighPrice", typeof(decimal));
        table.Columns.Add("LowPrice", typeof(decimal));
        table.Columns.Add("BorrowingBalance", typeof(long));
        table.Columns.Add("BorrowingBalanceChange", typeof(int));
        table.Columns.Add("MarginBalance", typeof(long));
        table.Columns.Add("ShortBalance", typeof(long));
        table.Columns.Add("MarginRatio", typeof(decimal));
        table.Columns.Add("HistoricalVolatility20D", typeof(decimal));
        table.Columns.Add("Volume", typeof(long));
        table.Columns.Add("Turnover", typeof(long));

        foreach (var m in metrics)
        {
            table.Rows.Add(
                m.Ticker,
                m.TradeDate,
                m.ClosePrice ?? (object)DBNull.Value,
                m.OpenPrice ?? (object)DBNull.Value,
                m.HighPrice ?? (object)DBNull.Value,
                m.LowPrice ?? (object)DBNull.Value,
                m.BorrowingBalance ?? (object)DBNull.Value,
                m.BorrowingBalanceChange ?? (object)DBNull.Value,
                m.MarginBalance ?? (object)DBNull.Value,
                m.ShortBalance ?? (object)DBNull.Value,
                m.MarginRatio ?? (object)DBNull.Value,
                m.HistoricalVolatility20D ?? (object)DBNull.Value,
                m.Volume ?? (object)DBNull.Value,
                m.Turnover ?? (object)DBNull.Value
            );
        }

        return table;
    }
}

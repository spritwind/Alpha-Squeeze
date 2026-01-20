using System.Data;
using AlphaSqueeze.Api.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace AlphaSqueeze.Api.Controllers;

/// <summary>
/// 系統監控 API
/// 提供資料狀態、系統日誌等監控功能
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MonitoringController : ControllerBase
{
    private readonly IDbConnection _connection;
    private readonly ILogger<MonitoringController> _logger;
    private static readonly List<SystemLogEntry> _systemLogs = new();
    private static readonly object _logLock = new();

    public MonitoringController(IDbConnection connection, ILogger<MonitoringController> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    /// <summary>
    /// 取得完整系統監控資料
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SystemMonitoringData), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemStatus()
    {
        var result = new SystemMonitoringData
        {
            Timestamp = DateTime.Now,
            DataSources = new List<DataSourceStatus>()
        };

        // 1. DailyStockMetrics
        try
        {
            var stockMetrics = await _connection.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT
                    COUNT(*) AS TotalRecords,
                    MAX(TradeDate) AS LastUpdate,
                    MIN(TradeDate) AS FirstDate,
                    COUNT(DISTINCT Ticker) AS UniqueTickers,
                    SUM(CASE WHEN BorrowingBalance IS NOT NULL THEN 1 ELSE 0 END) AS WithBorrowingData,
                    SUM(CASE WHEN MarginRatio IS NOT NULL THEN 1 ELSE 0 END) AS WithMarginData
                FROM DailyStockMetrics
            ");

            result.DataSources.Add(new DataSourceStatus
            {
                Name = "股票日指標",
                TableName = "DailyStockMetrics",
                TotalRecords = (int)(stockMetrics?.TotalRecords ?? 0),
                LastUpdate = stockMetrics?.LastUpdate,
                FirstDate = stockMetrics?.FirstDate,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["uniqueTickers"] = stockMetrics?.UniqueTickers ?? 0,
                    ["withBorrowingData"] = stockMetrics?.WithBorrowingData ?? 0,
                    ["withMarginData"] = stockMetrics?.WithMarginData ?? 0
                },
                Status = (stockMetrics?.TotalRecords ?? 0) > 0 ? "OK" : "EMPTY"
            });
        }
        catch (Exception ex)
        {
            result.DataSources.Add(new DataSourceStatus
            {
                Name = "股票日指標",
                TableName = "DailyStockMetrics",
                Status = "ERROR",
                ErrorMessage = ex.Message
            });
        }

        // 2. WarrantMarketData (IV)
        try
        {
            var tableExists = await _connection.QuerySingleOrDefaultAsync<int>(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME = 'WarrantMarketData'
            ");

            if (tableExists > 0)
            {
                var warrantData = await _connection.QuerySingleOrDefaultAsync<dynamic>(@"
                    SELECT
                        COUNT(*) AS TotalRecords,
                        MAX(TradeDate) AS LastUpdate,
                        MIN(TradeDate) AS FirstDate,
                        COUNT(DISTINCT UnderlyingTicker) AS UniqueUnderlyings
                    FROM WarrantMarketData
                ");

                result.DataSources.Add(new DataSourceStatus
                {
                    Name = "權證市場資料 (IV)",
                    TableName = "WarrantMarketData",
                    TotalRecords = (int)(warrantData?.TotalRecords ?? 0),
                    LastUpdate = warrantData?.LastUpdate,
                    FirstDate = warrantData?.FirstDate,
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["uniqueUnderlyings"] = warrantData?.UniqueUnderlyings ?? 0
                    },
                    Status = (warrantData?.TotalRecords ?? 0) > 0 ? "OK" : "EMPTY"
                });
            }
            else
            {
                result.DataSources.Add(new DataSourceStatus
                {
                    Name = "權證市場資料 (IV)",
                    TableName = "WarrantMarketData",
                    Status = "NOT_EXISTS",
                    ErrorMessage = "資料表不存在"
                });
            }
        }
        catch (Exception ex)
        {
            result.DataSources.Add(new DataSourceStatus
            {
                Name = "權證市場資料 (IV)",
                TableName = "WarrantMarketData",
                Status = "ERROR",
                ErrorMessage = ex.Message
            });
        }

        // 3. CBIssuance
        try
        {
            var cbIssuance = await _connection.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT
                    COUNT(*) AS TotalRecords,
                    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS ActiveCount,
                    MAX(UpdatedAt) AS LastUpdate
                FROM CBIssuance
            ");

            result.DataSources.Add(new DataSourceStatus
            {
                Name = "可轉債發行",
                TableName = "CBIssuance",
                TotalRecords = (int)(cbIssuance?.TotalRecords ?? 0),
                LastUpdate = cbIssuance?.LastUpdate,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["activeCount"] = cbIssuance?.ActiveCount ?? 0
                },
                Status = (cbIssuance?.TotalRecords ?? 0) > 0 ? "OK" : "EMPTY"
            });
        }
        catch (Exception ex)
        {
            result.DataSources.Add(new DataSourceStatus
            {
                Name = "可轉債發行",
                TableName = "CBIssuance",
                Status = "ERROR",
                ErrorMessage = ex.Message
            });
        }

        // 4. CBDailyTracking
        try
        {
            var cbTracking = await _connection.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT
                    COUNT(*) AS TotalRecords,
                    MAX(TradeDate) AS LastUpdate,
                    MIN(TradeDate) AS FirstDate,
                    COUNT(DISTINCT CBTicker) AS UniqueCBs
                FROM CBDailyTracking
            ");

            result.DataSources.Add(new DataSourceStatus
            {
                Name = "可轉債追蹤",
                TableName = "CBDailyTracking",
                TotalRecords = (int)(cbTracking?.TotalRecords ?? 0),
                LastUpdate = cbTracking?.LastUpdate,
                FirstDate = cbTracking?.FirstDate,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["uniqueCBs"] = cbTracking?.UniqueCBs ?? 0
                },
                Status = (cbTracking?.TotalRecords ?? 0) > 0 ? "OK" : "EMPTY"
            });
        }
        catch (Exception ex)
        {
            result.DataSources.Add(new DataSourceStatus
            {
                Name = "可轉債追蹤",
                TableName = "CBDailyTracking",
                Status = "ERROR",
                ErrorMessage = ex.Message
            });
        }

        // 5. TrackedTickers
        try
        {
            var tickers = await _connection.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT
                    COUNT(*) AS TotalRecords,
                    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS ActiveCount
                FROM TrackedTickers
            ");

            result.DataSources.Add(new DataSourceStatus
            {
                Name = "追蹤股票",
                TableName = "TrackedTickers",
                TotalRecords = (int)(tickers?.TotalRecords ?? 0),
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["activeCount"] = tickers?.ActiveCount ?? 0
                },
                Status = (tickers?.TotalRecords ?? 0) > 0 ? "OK" : "EMPTY"
            });
        }
        catch (Exception ex)
        {
            result.DataSources.Add(new DataSourceStatus
            {
                Name = "追蹤股票",
                TableName = "TrackedTickers",
                Status = "ERROR",
                ErrorMessage = ex.Message
            });
        }

        // 6. BackfillJobs
        try
        {
            var jobs = await _connection.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT
                    COUNT(*) AS TotalRecords,
                    SUM(CASE WHEN Status = 'RUNNING' THEN 1 ELSE 0 END) AS RunningCount,
                    SUM(CASE WHEN Status = 'COMPLETED' THEN 1 ELSE 0 END) AS CompletedCount,
                    SUM(CASE WHEN Status = 'FAILED' THEN 1 ELSE 0 END) AS FailedCount,
                    MAX(CompletedAt) AS LastCompleted
                FROM BackfillJobs
            ");

            result.DataSources.Add(new DataSourceStatus
            {
                Name = "回補任務",
                TableName = "BackfillJobs",
                TotalRecords = (int)(jobs?.TotalRecords ?? 0),
                LastUpdate = jobs?.LastCompleted,
                AdditionalInfo = new Dictionary<string, object>
                {
                    ["runningCount"] = jobs?.RunningCount ?? 0,
                    ["completedCount"] = jobs?.CompletedCount ?? 0,
                    ["failedCount"] = jobs?.FailedCount ?? 0
                },
                Status = "OK"
            });
        }
        catch (Exception ex)
        {
            result.DataSources.Add(new DataSourceStatus
            {
                Name = "回補任務",
                TableName = "BackfillJobs",
                Status = "ERROR",
                ErrorMessage = ex.Message
            });
        }

        // 計算整體狀態
        var hasError = result.DataSources.Any(d => d.Status == "ERROR");
        var hasEmpty = result.DataSources.Any(d => d.Status == "EMPTY" || d.Status == "NOT_EXISTS");

        result.OverallStatus = hasError ? "ERROR" : (hasEmpty ? "WARNING" : "OK");

        return Ok(result);
    }

    /// <summary>
    /// 取得系統日誌
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(SystemLogsResponse), StatusCodes.Status200OK)]
    public IActionResult GetSystemLogs([FromQuery] int limit = 100, [FromQuery] string? level = null)
    {
        lock (_logLock)
        {
            var logs = _systemLogs.AsEnumerable();

            if (!string.IsNullOrEmpty(level))
            {
                logs = logs.Where(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
            }

            return Ok(new SystemLogsResponse
            {
                Logs = logs.OrderByDescending(l => l.Timestamp).Take(limit).ToList(),
                TotalCount = _systemLogs.Count
            });
        }
    }

    /// <summary>
    /// 新增系統日誌 (供內部使用)
    /// </summary>
    [HttpPost("logs")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public IActionResult AddLog([FromBody] AddLogRequest request)
    {
        var entry = new SystemLogEntry
        {
            Timestamp = DateTime.Now,
            Level = request.Level ?? "INFO",
            Source = request.Source ?? "API",
            Message = request.Message,
            Details = request.Details
        };

        lock (_logLock)
        {
            _systemLogs.Add(entry);

            // 保留最近 1000 筆
            while (_systemLogs.Count > 1000)
            {
                _systemLogs.RemoveAt(0);
            }
        }

        _logger.LogInformation("[{Source}] {Message}", entry.Source, entry.Message);

        return Created("", entry);
    }

    /// <summary>
    /// 清除系統日誌
    /// </summary>
    [HttpDelete("logs")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult ClearLogs()
    {
        lock (_logLock)
        {
            _systemLogs.Clear();
        }
        return NoContent();
    }

    /// <summary>
    /// 取得資料庫連線狀態
    /// </summary>
    [HttpGet("health/database")]
    [ProducesResponseType(typeof(DatabaseHealthResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDatabaseHealth()
    {
        try
        {
            var result = await _connection.QuerySingleOrDefaultAsync<int>("SELECT 1");
            return Ok(new DatabaseHealthResponse
            {
                Status = "Healthy",
                Timestamp = DateTime.Now,
                Message = "資料庫連線正常"
            });
        }
        catch (Exception ex)
        {
            return Ok(new DatabaseHealthResponse
            {
                Status = "Unhealthy",
                Timestamp = DateTime.Now,
                Message = $"資料庫連線失敗: {ex.Message}"
            });
        }
    }
}

#region DTOs

public class SystemMonitoringData
{
    public DateTime Timestamp { get; set; }
    public string OverallStatus { get; set; } = "OK";
    public List<DataSourceStatus> DataSources { get; set; } = new();
}

public class DataSourceStatus
{
    public string Name { get; set; } = "";
    public string TableName { get; set; } = "";
    public int TotalRecords { get; set; }
    public DateTime? LastUpdate { get; set; }
    public DateTime? FirstDate { get; set; }
    public string Status { get; set; } = "OK";
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? AdditionalInfo { get; set; }
}

public class SystemLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "INFO";
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Details { get; set; }
}

public class SystemLogsResponse
{
    public List<SystemLogEntry> Logs { get; set; } = new();
    public int TotalCount { get; set; }
}

public class AddLogRequest
{
    public string? Level { get; set; }
    public string? Source { get; set; }
    public string Message { get; set; } = "";
    public string? Details { get; set; }
}

public class DatabaseHealthResponse
{
    public string Status { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = "";
}

#endregion
